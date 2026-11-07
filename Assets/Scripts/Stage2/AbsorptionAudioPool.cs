using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using YG;

namespace Vodovorot.Stage2
{
    /// <summary>A bounded, quiet series of physical plops. AudioSource allocation occurs once.</summary>
    public sealed class AbsorptionAudioPool : MonoBehaviour
    {
        private struct Request
        {
            public int value, skin;
            public Vector3 size;
            public double arrived;
        }

        private readonly Request[] pending = new Request[Stage2AudioPolicy.MaxQueuedAbsorptions];
        private readonly AudioSource[] voices = new AudioSource[Stage2AudioPolicy.MaxAbsorptionVoices];
        private readonly float[] gains = new float[Stage2AudioPolicy.MaxAbsorptionVoices];
        private readonly AbsorptionAudioLimiter limiter = new AbsorptionAudioLimiter();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(7);
        private readonly HashSet<string> rankConfirmations = new HashSet<string>();
        private readonly Queue<string> rankConfirmationOrder = new Queue<string>(32);
        private int queued;
        private AudioSource rankVoice;
        private AudioManager manager;
        private AudioMixerGroup sfxGroup;
        private double nextMixerProbe, nextRankAccent;
        private bool focused = true, applicationPaused;
        private uint variationState = 0x715D3A29u;

        /// <summary>Read-only DSP deadline; explicit auditions wait for the same ordinary cooldown.</summary>
        public double NextRankAccentAt => nextRankAccent;

        private void Awake()
        {
            for (int i = 0; i < voices.Length; i++) voices[i] = CreateVoice("Absorption " + (i + 1), 96);
            rankVoice = CreateVoice("Confirmed rank accent", 80);
            SceneManager.activeSceneChanged += OnSceneChanged;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            RefreshRouting(true);
        }

        private AudioSource CreateVoice(string label, int priority)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            source.reverbZoneMix = 0f;
            source.ignoreListenerPause = false;
            return source;
        }

        public void EnqueueAbsorption(int value, Vector3 physicalSize)
        {
            if (!CanPlay() || Time.timeScale <= 0f || SoundVolume <= 0f) return;
            var request = new Request { value = value, size = physicalSize, skin = YG2.saves != null ? YG2.saves.equipedMaterial : 0,
                arrived = AudioSettings.dspTime };
            if (queued < pending.Length) pending[queued++] = request;
            else
            {
                // Coalesce overflow into the heaviest remaining hit, never sum scores into a fake large object.
                int last = queued - 1;
                float oldWeight = Stage2AudioPolicy.AbsorptionNote(pending[last].value, pending[last].size, pending[last].skin, 0, 0).weight;
                float newWeight = Stage2AudioPolicy.AbsorptionNote(value, physicalSize, request.skin, 0, 0).weight;
                if (newWeight > oldWeight) pending[last] = request;
            }
            TryPlayNext(AudioSettings.dspTime);
        }

        public void PlayRankAccent(string confirmationId)
        {
            if (!rankConfirmations.Add(confirmationId)) return;
            rankConfirmationOrder.Enqueue(confirmationId);
            if (rankConfirmationOrder.Count > 32) rankConfirmations.Remove(rankConfirmationOrder.Dequeue());
            if (!CanPlay() || SoundVolume <= 0f || AudioSettings.dspTime < nextRankAccent) return;
            AudioClip clip = Clip("Stage2Audio/RankImproved");
            if (clip == null) return;
            RefreshRouting(true);
            rankVoice.pitch = 1f;
            rankVoice.clip = clip;
            rankVoice.volume = Stage2AudioPolicy.OutputGain(Stage2AudioPolicy.RankAccentGain, SoundVolume, sfxGroup != null);
            rankVoice.Play();
            Vodovorot.Stage3.BackgroundMusicPlayer.DuckForRankAccent();
            nextRankAccent = AudioSettings.dspTime + 2.5;
        }

        private void Update()
        {
            if (!CanPlay()) { StopAll(); return; }
            RefreshRouting(false);
            for (int i = 0; i < voices.Length; i++)
                voices[i].volume = Stage2AudioPolicy.OutputGain(gains[i], SoundVolume, sfxGroup != null);
            rankVoice.volume = Stage2AudioPolicy.OutputGain(Stage2AudioPolicy.RankAccentGain, SoundVolume, sfxGroup != null);
            if (Time.timeScale <= 0f || SoundVolume <= 0f) StopAbsorptions();
            else TryPlayNext(AudioSettings.dspTime);
            if (SoundVolume <= 0f) rankVoice.Stop();
        }

        private void TryPlayNext(double now)
        {
            while (queued > 0 && now - pending[0].arrived > Stage2AudioPolicy.MaximumQueueAge) RemoveFirst();
            if (queued == 0) return;
            Request request = pending[0];
            int series = limiter.NextSeriesIndex(now);
            Stage2AudioPolicy.Note note = Stage2AudioPolicy.AbsorptionNote(request.value, request.size, request.skin, series, NextVariation());
            AudioClip clip = Clip(note.resource);
            if (clip == null) { RemoveFirst(); return; }
            if (!limiter.TryAcquire(now, clip.length / note.pitch, out int voice)) return;
            RemoveFirst();
            RefreshRouting(false);
            voices[voice].pitch = note.pitch;
            voices[voice].clip = clip;
            gains[voice] = note.gain;
            voices[voice].volume = Stage2AudioPolicy.OutputGain(note.gain, SoundVolume, sfxGroup != null);
            voices[voice].Play();
        }

        private float NextVariation()
        {
            variationState ^= variationState << 13;
            variationState ^= variationState >> 17;
            variationState ^= variationState << 5;
            return (variationState & 65535u) / 32767.5f - 1f;
        }

        private void RemoveFirst()
        {
            queued--;
            for (int i = 0; i < queued; i++) pending[i] = pending[i + 1];
        }

        private AudioClip Clip(string resource)
        {
            if (!clips.TryGetValue(resource, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(resource);
                clips.Add(resource, clip);
            }
            return clip;
        }

        private void RefreshRouting(bool force)
        {
            double now = AudioSettings.dspTime;
            if (!force && now < nextMixerProbe) return;
            nextMixerProbe = now + 1;
            AudioManager current = VodovorotGameManager.Instance != null ? VodovorotGameManager.Instance.AudioManager : null;
            if (current == null) current = FindAnyObjectByType<AudioManager>();
            if (!force && current == manager && (current == null || sfxGroup != null)) return;
            manager = current;
            sfxGroup = Group(manager != null ? manager.mixer : null, "SFX");
            foreach (AudioSource voice in voices) voice.outputAudioMixerGroup = sfxGroup;
            rankVoice.outputAudioMixerGroup = sfxGroup;
        }

        private static AudioMixerGroup Group(AudioMixer mixer, string name)
        {
            if (mixer == null) return null;
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(name))
                if (group.name == name) return group;
            return null;
        }

        private static float SoundVolume => YG2.saves != null ? Stage2AudioPolicy.SavedVolume(YG2.saves.soundValue) : 0f;
        private bool CanPlay() => isActiveAndEnabled && focused && !applicationPaused && !YG2.nowAdsShow;

        private void StopAbsorptions()
        {
            queued = 0;
            limiter.Reset();
            foreach (AudioSource source in voices) if (source != null) source.Stop();
        }

        private void StopAll()
        {
            StopAbsorptions();
            if (rankVoice != null) rankVoice.Stop();
        }

        private void OnSceneChanged(Scene before, Scene after) { StopAbsorptions(); RefreshRouting(true); }
        private void OnAudioConfigurationChanged(bool deviceChanged) { StopAll(); nextMixerProbe = nextRankAccent = 0; }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) StopAll(); }
        private void OnApplicationPause(bool value) { applicationPaused = value; if (value) StopAll(); }
        private void OnDisable() => StopAll();
        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }
    }
}
