using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using YG;

namespace Vodovorot.Stage3
{
    /// <summary>Persistent scene soundtrack with two bounded voices and explicit review previews.</summary>
    public sealed class BackgroundMusicPlayer : MonoBehaviour
    {
        private const string ObjectName = "Stage3BackgroundMusic";
        private const float NominalGain = .28f;
        private const float CrossfadeSeconds = .7f;
        private const float DuckGain = .32f;
        private static BackgroundMusicPlayer instance;
        private readonly AudioSource[] voices = new AudioSource[2];
        private readonly float[] levels = new float[2];
        private readonly bool[] started = new bool[2];
        private AudioManager manager;
        private AudioMixerGroup musicGroup;
        private AudioMixer settingsMixer;
        private int incoming;
        private float outgoingStart, crossfadeElapsed, duckHold, duck = 1f;
        private float lastAppliedSetting = float.NaN;
        private double nextRoutingProbe, nextClipProbe;
        private bool crossfading, fadingToSilence, suspended = true, applicationPaused, focused = true;
        private bool platformFocused = true, adOpen, subscribed, activePreview, automaticPlayback = true;
        private bool shuttingDown;
        private string themeId, sceneTheme, previewTheme;
        private string warnedResource;
        private int browserState = 15;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void MusicStage3_Install(string receiver);
        [DllImport("__Internal")] private static extern int MusicStage3_GetState();
        [DllImport("__Internal")] private static extern void MusicStage3_Uninstall();
#endif

        public static string PreviewThemeId => instance != null ? instance.previewTheme : null;
        public static string CurrentThemeId => instance != null ? instance.themeId : null;
        public static string ProductionThemeId => instance != null ? instance.sceneTheme : null;
        public static bool IsPlaybackSuspended => instance != null && instance.suspended;
        public static bool AutomaticPlaybackEnabled => instance != null && instance.automaticPlayback;
        public static bool HasActiveMusicVoice => instance != null && Array.Exists(instance.voices,
            source => source != null && source.isPlaying);
        public static bool IsPreviewPlaying => instance != null && instance.previewTheme != null && instance.activePreview &&
            !instance.crossfading && !instance.suspended && instance.voices[instance.incoming].isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAutomatically() => EnsureInstalled();

        private static BackgroundMusicPlayer EnsureInstalled()
        {
            if (instance != null) return instance;
            GameObject root = new GameObject(ObjectName);
            DontDestroyOnLoad(root);
            return root.AddComponent<BackgroundMusicPlayer>();
        }

        /// <summary>Explicit one-shot Demo* audition, independent of the looping Full* scene selection.</summary>
        public static void PlayPreviewTheme(string requested)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isPlaying || ResourceForTheme(requested) == null) return;
            var player = EnsureInstalled();
            player.previewTheme = requested;
            player.nextClipProbe = 0;
            player.ApplyDesiredTheme();
#endif
        }

        /// <summary>Ends the explicit audition. The scene soundtrack returns when automatic playback is enabled.</summary>
        public static void StopPreview()
        {
            if (instance == null || instance.previewTheme == null) return;
            instance.previewTheme = null;
            instance.StopVoices();
            instance.nextClipProbe = 0;
            // Resume auto on the next Update, so an immediate next audition can replace it cleanly.
        }

        /// <summary>Review-only isolation; production is automatic by default and cannot call this override.</summary>
        public static void SetAutomaticPlaybackForReview(bool enabled)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isPlaying || instance == null) return;
            var player = instance;
            player.automaticPlayback = enabled;
            player.nextClipProbe = 0;
            player.ApplyDesiredTheme();
#endif
        }

        /// <summary>Called only after the real confirmed-rank SFX voice starts.</summary>
        public static void DuckForRankAccent()
        {
            if (instance != null && instance.themeId != null) instance.duckHold = .95f;
        }

        public static string ThemeForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "MainMenu": return "menu";
                case "Low Poly City": return "city";
                case "Garden": return "garden";
                case "MedievalCastle": return "medieval";
                default: return null;
            }
        }

        // Preserve the first-milestone audition contract and Editor WAV controls.
        public static string ResourceForTheme(string id)
        {
            switch (id)
            {
                case "menu": return "Stage3Music/DemoMenu";
                case "city": return "Stage3Music/DemoCity";
                case "garden": return "Stage3Music/DemoGarden";
                case "medieval": return "Stage3Music/DemoMedieval";
                default: return null;
            }
        }

        public static string ProductionResourceForTheme(string id)
        {
            switch (id)
            {
                case "menu": return "Stage3Music/FullMenu";
                case "city": return "Stage3Music/FullCity";
                case "garden": return "Stage3Music/FullGarden";
                case "medieval": return "Stage3Music/FullMedieval";
                default: return null;
            }
        }

        /// <summary>Saved Music attenuation is applied at the mixer OR once on an unrouted source.</summary>
        public static float SourceGain(float level, float savedMusicVolume, bool routedThroughSettingsMixer)
        {
            float volume = SafeVolume(savedMusicVolume);
            if (volume <= 0f || float.IsNaN(level) || float.IsInfinity(level)) return 0f;
            return Mathf.Clamp01(level) * NominalGain * (routedThroughSettingsMixer ? 1f : volume);
        }

        private static float SafeVolume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        private static float SavedMusicVolume => YG2.saves != null ? SafeVolume(YG2.saves.musicValue) : 0f;
        private string DesiredTheme => previewTheme ?? (automaticPlayback ? sceneTheme : null);

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            gameObject.name = ObjectName;
            DontDestroyOnLoad(gameObject);
            focused = Application.isFocused;
            platformFocused = YG2.isFocusWindowGame;
            adOpen = YG2.nowAdsShow;
            for (int i = 0; i < voices.Length; i++)
            {
                GameObject child = new GameObject("Music crossfade " + (i + 1));
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.reverbZoneMix = 0f;
                source.priority = 160;
                source.ignoreListenerPause = false;
                source.volume = 0f;
                voices[i] = source;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            browserState = 0;
            MusicStage3_Install(ObjectName);
            browserState = MusicStage3_GetState();
#endif
            SceneManager.activeSceneChanged += OnSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            BindPlatformEvents();
            sceneTheme = ThemeForScene(SceneManager.GetActiveScene().name);
            RefreshRouting(true);
            ApplyDesiredTheme();
        }

        private void BindPlatformEvents()
        {
            // SDK initialization can reset Action fields; binding is idempotent at its data-ready event.
            UnbindPlatformEvents();
            YG2.onGetSDKData += OnSdkReady;
            YG2.onPauseGame += OnPlatformPause;
            YG2.onFocusWindowGame += OnPlatformFocus;
            YG2.onOpenAnyAdv += OnAdOpened;
            YG2.onCloseAnyAdv += OnAdClosed;
            YG2.onErrorAnyAdv += OnAdClosed;
            subscribed = true;
        }

        private void UnbindPlatformEvents()
        {
            if (!subscribed) return;
            YG2.onGetSDKData -= OnSdkReady;
            YG2.onPauseGame -= OnPlatformPause;
            YG2.onFocusWindowGame -= OnPlatformFocus;
            YG2.onOpenAnyAdv -= OnAdOpened;
            YG2.onCloseAnyAdv -= OnAdClosed;
            YG2.onErrorAnyAdv -= OnAdClosed;
            subscribed = false;
        }

        private void ApplyDesiredTheme()
        {
            if (shuttingDown || !isActiveAndEnabled || crossfading) return; // Finish the smooth fade; latest request wins next.
            string desired = DesiredTheme;
            bool preview = previewTheme != null;
            if (desired == themeId && preview == activePreview) return;
            if (desired == null) { BeginFadeToSilence(); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextClipProbe) return;
            string resource = preview ? ResourceForTheme(desired) : ProductionResourceForTheme(desired);
            AudioClip clip = resource != null ? Resources.Load<AudioClip>(resource) : null;
            if (clip == null || clip.loadState == AudioDataLoadState.Failed)
            {
                nextClipProbe = now + 2;
                if (warnedResource != resource)
                {
                    Debug.LogWarning("Missing Stage 3 music: " + resource);
                    warnedResource = resource;
                }
                BeginFadeToSilence();
                return;
            }
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                nextClipProbe = now + .1;
                return;
            }
            warnedResource = null;
            RefreshRouting(true);
            int outgoing = levels[0] >= levels[1] ? 0 : 1;
            incoming = 1 - outgoing;
            voices[incoming].Stop();
            started[incoming] = false;
            voices[incoming].clip = clip;
            voices[incoming].loop = !preview;
            voices[incoming].volume = 0f;
            levels[incoming] = 0f;
            outgoingStart = levels[outgoing];
            crossfadeElapsed = 0f;
            crossfading = true;
            fadingToSilence = false;
            themeId = desired;
            activePreview = preview;
            UpdateSuspension();
            if (!suspended && !started[incoming]) StartVoice(incoming);
            ApplyVolumes();
        }

        private void BeginFadeToSilence()
        {
            if (themeId == null) return;
            incoming = levels[0] >= levels[1] ? 0 : 1;
            outgoingStart = levels[incoming];
            crossfadeElapsed = 0f;
            crossfading = fadingToSilence = true;
        }

        private void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            browserState = MusicStage3_GetState();
#endif
            RefreshRouting(false);
            UpdateSuspension();
            if (!suspended)
            {
                float delta = Mathf.Min(Time.unscaledDeltaTime, .1f);
                if (crossfading)
                {
                    crossfadeElapsed += delta;
                    float phase = Mathf.Clamp01(crossfadeElapsed / CrossfadeSeconds) * Mathf.PI * .5f;
                    if (fadingToSilence) levels[incoming] = outgoingStart * Mathf.Cos(phase);
                    else
                    {
                        levels[incoming] = Mathf.Sin(phase);
                        levels[1 - incoming] = outgoingStart * Mathf.Cos(phase);
                    }
                    if (crossfadeElapsed >= CrossfadeSeconds)
                    {
                        if (fadingToSilence) StopVoices();
                        else
                        {
                            StopVoice(1 - incoming);
                            levels[incoming] = 1f;
                            crossfading = false;
                        }
                    }
                }
                duckHold = Mathf.Max(0f, duckHold - delta);
                float target = duckHold > 0f ? DuckGain : 1f;
                duck = Mathf.MoveTowards(duck, target, delta / (target < duck ? .045f : .55f));
                if (!crossfading && themeId != null && started[incoming] && !voices[incoming].isPlaying)
                {
                    if (activePreview)
                    {
                        previewTheme = null;
                        StopVoices();
                    }
                    else StartVoice(incoming); // Recover a platform/device audio interruption without allocating a voice.
                }
            }
            ApplyDesiredTheme();
            ApplyVolumes();
        }

        private void StartVoice(int index)
        {
            if (voices[index].clip == null) return;
            voices[index].Play();
            started[index] = true;
        }

        // Results/menu UI may freeze game time while music keeps playing.
        private bool MustSuspend() => !YG2.isSDKEnabled || !focused || applicationPaused || !platformFocused || !YG2.isFocusWindowGame ||
            adOpen || YG2.nowAdsShow || YG2.isPauseGame || AudioListener.pause || (browserState & 15) != 15;

        private void UpdateSuspension()
        {
            bool value = MustSuspend();
            // Unity WebGL can resume its pending channels when AudioContext becomes
            // running, independently of our ad/focus state. Keep the physical voices
            // silent while any suspension reason still applies, including that race.
            if (value)
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i] != null)
                    {
                        voices[i].mute = true;
                        if (started[i] && voices[i].isPlaying) voices[i].Pause();
                    }
            if (value == suspended) return;
            suspended = value;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null || voices[i].clip == null) continue;
                if (value) { if (started[i]) voices[i].Pause(); }
                else if (started[i]) voices[i].UnPause();
                else StartVoice(i);
            }
        }

        /// <summary>Browser visibility/context notifications pause immediately even while animation frames are throttled.</summary>
        [UnityEngine.Scripting.Preserve]
        public void OnBrowserAudioState(string state)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (int.TryParse(state, out int value)) browserState = value;
            UpdateSuspension();
#endif
        }

        private void ApplyVolumes()
        {
            float saved = SavedMusicVolume;
            if (musicGroup != null && settingsMixer != null && !Mathf.Approximately(lastAppliedSetting, saved))
            {
                settingsMixer.SetFloat("MusicVol", Mathf.Log10(Mathf.Max(.001f, saved)) * 20f);
                lastAppliedSetting = saved;
            }
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null) continue;
                voices[i].mute = suspended || saved <= 0f;
                voices[i].volume = SourceGain(levels[i] * duck, saved, musicGroup != null);
            }
        }

        private void RefreshRouting(bool force)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!force && now < nextRoutingProbe) return;
            nextRoutingProbe = now + 1;
            AudioManager current = VodovorotGameManager.Instance != null ? VodovorotGameManager.Instance.AudioManager : null;
            if (current == null) current = FindAnyObjectByType<AudioManager>();
            if (!force && current == manager && (current == null || musicGroup != null)) return;
            manager = current;
            musicGroup = null;
            settingsMixer = null;
            lastAppliedSetting = float.NaN;
            if (manager != null && manager.mixer != null)
                foreach (AudioMixerGroup group in manager.mixer.FindMatchingGroups("Music"))
                    if (group.name == "Music") { musicGroup = group; settingsMixer = manager.mixer; break; }
            foreach (AudioSource source in voices)
                if (source != null) source.outputAudioMixerGroup = musicGroup;
            SuppressLegacyMusicSources();
            ApplyVolumes();
        }

        private static void SuppressLegacyMusicSources()
        {
            foreach (AudioManager legacy in FindObjectsByType<AudioManager>(FindObjectsInactive.Include))
            {
                AudioSource source = legacy.musicSource;
                if (source == null) continue;
                source.Stop();
                source.playOnAwake = false;
                source.mute = true;
                source.enabled = false;
            }
        }

        private void StopVoice(int index)
        {
            if (voices[index] != null) { voices[index].Stop(); voices[index].clip = null; voices[index].volume = 0f; }
            started[index] = false;
            levels[index] = 0f;
        }

        private void StopVoices()
        {
            StopVoice(0); StopVoice(1);
            themeId = null;
            activePreview = false;
            crossfading = fadingToSilence = false;
            duckHold = 0f;
            duck = 1f;
        }

        private void OnSceneChanged(Scene before, Scene after)
        {
            previewTheme = null;
            sceneTheme = ThemeForScene(after.name);
            nextClipProbe = 0;
            RefreshRouting(true);
            ApplyDesiredTheme();
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshRouting(true);
        private void OnSdkReady() { BindPlatformEvents(); platformFocused = YG2.isFocusWindowGame; UpdateSuspension(); }
        private void OnPlatformPause(bool value) => UpdateSuspension();
        private void OnPlatformFocus(bool value) { platformFocused = value; UpdateSuspension(); }
        private void OnAdOpened() { adOpen = true; UpdateSuspension(); }
        private void OnAdClosed() { adOpen = false; UpdateSuspension(); }
        private void OnAudioConfigurationChanged(bool changed) { nextRoutingProbe = 0; UpdateSuspension(); }
        private void OnApplicationFocus(bool value) { focused = value; UpdateSuspension(); }
        private void OnApplicationPause(bool value) { applicationPaused = value; UpdateSuspension(); }
        private void OnApplicationQuit() { shuttingDown = true; StopVoices(); }
        private void OnDisable() => StopVoices();
        private void OnDestroy()
        {
            UnbindPlatformEvents();
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (instance == this) MusicStage3_Uninstall();
#endif
            if (instance == this) instance = null;
        }
    }
}
