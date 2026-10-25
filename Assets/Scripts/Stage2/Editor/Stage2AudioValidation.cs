#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Vodovorot.Stage2.Editor
{
    /// <summary>Batch: -executeMethod Vodovorot.Stage2.Editor.Stage2AudioValidation.Run</summary>
    public static class Stage2AudioValidation
    {
        private const string DirectoryPath = "Assets/Audio/Stage2/Resources/Stage2Audio/";
        private static readonly string[] Names = { "AbsorbBaseLight", "AbsorbBaseDeep", "AbsorbGoldLight", "AbsorbGoldDeep", "AbsorbLordLight", "AbsorbLordDeep", "RankImproved" };

        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public string[] checks;
            public int pcmBytes;
            public int maximumVoices;
            public float stage2PeakBound;
            public string error;
        }

        [MenuItem("Tools/Vodovorot/Stage 2/Validate original audio")]
        public static void Run()
        {
            var checks = new List<string>();
            int bytes = 0;
            try
            {
                foreach (string name in Names)
                {
                    string path = DirectoryPath + name + ".wav";
                    byte[] wav = File.ReadAllBytes(path);
                    bytes += wav.Length;
                    ValidatePcm(name, wav);
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    Require(clip != null && clip.channels == 1 && clip.frequency == 44100, "AudioClip import missing or incorrect: " + name);
                    Require(Math.Abs(clip.length - (name == "RankImproved" ? .82f : name.EndsWith("Deep") ? .3f : .175f)) < .002f,
                        "AudioClip duration changed during import: " + name);
                    AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    Require(importer != null && importer.forceToMono && importer.defaultSampleSettings.preloadAudioData &&
                            importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM,
                        "Use preloaded mono PCM for bounded peaks and small deterministic cues: " + name);
                }
                checks.Add("seven_original_mono_pcm_clips_with_peak_rms_edge_and_duration_limits");

                int[] values = { 1, 2, 3, 5, 10, 25, 40, 60, 100, int.MaxValue };
                foreach (int value in values)
                    foreach (int skin in new[] { 0, 1, 2, 3, 4 })
                        foreach (int series in new[] { 0, 1, 4, 200 })
                            foreach (float jitter in new[] { -1f, 0f, 1f, float.NaN })
                            {
                                Stage2AudioPolicy.Note note = Stage2AudioPolicy.AbsorptionNote(value,
                                    new Vector3(value * .13f, float.PositiveInfinity, float.NaN), skin, series, jitter);
                                Require(note.pitch >= Stage2AudioPolicy.MinimumPitch && note.pitch <= Stage2AudioPolicy.MaximumPitch,
                                    "Pitch escaped the safe range.");
                                Require(note.gain >= 0f && note.gain <= Stage2AudioPolicy.MaximumVoiceGain && !float.IsNaN(note.gain),
                                    "Voice gain escaped the hard cap.");
                                Require(Resources.Load<AudioClip>(note.resource) != null, "Missing cue resource " + note.resource);
                            }
                Stage2AudioPolicy.Note light = Stage2AudioPolicy.AbsorptionNote(1, Vector3.one * .2f, 0, 0, 0);
                Stage2AudioPolicy.Note deep = Stage2AudioPolicy.AbsorptionNote(60, Vector3.one * 5f, 0, 0, 0);
                Require(light.pitch > deep.pitch && light.resource.EndsWith("Light") && deep.resource.EndsWith("Deep"),
                    "Large physical objects must sound deeper than small ones.");
                foreach (int skin in new[] { 2, 4 })
                {
                    Stage2AudioPolicy.Note cosmetic = Stage2AudioPolicy.AbsorptionNote(1, Vector3.one * .2f, skin, 0, 0);
                    Require(cosmetic.resource != light.resource && cosmetic.gain == light.gain && cosmetic.pitch == light.pitch,
                        "Skin timbres must remain cosmetic, with matching gain and pitch policy.");
                }
                Require(Stage2AudioPolicy.MaximumStage2PeakBound < .8f, "Worst-case Stage 2 sum lost its headroom.");
                checks.Add("size_pitch_skin_cosmetics_and_worst_case_peak_headroom");

                var limiter = new AbsorptionAudioLimiter();
                double previousNote = double.NegativeInfinity;
                int started = 0;
                for (int millisecond = 0; millisecond < 5000; millisecond++)
                {
                    double now = millisecond / 1000.0;
                    if (limiter.TryAcquire(now, .3 / Stage2AudioPolicy.MinimumPitch, out int voice))
                    {
                        Require(voice >= 0 && voice < Stage2AudioPolicy.MaxAbsorptionVoices, "Voice index escaped pool.");
                        Require(now - previousNote + 1e-9 >= Stage2AudioPolicy.MinimumNoteInterval, "Cascade exceeded note-rate cap.");
                        previousNote = now;
                        started++;
                    }
                    Require(limiter.ActiveVoices(now) <= Stage2AudioPolicy.MaxAbsorptionVoices, "Cascade exceeded simultaneous voice cap.");
                }
                Require(started > 20 && started < 60, "Limiter must produce a bounded audible series under a 1000-hit/s flood.");
                limiter.Reset();
                Require(limiter.ActiveVoices(0) == 0 && limiter.TryAcquire(0, .2, out _), "Reset must clear pending voice reservations.");
                checks.Add("actual_limiter_flood_voice_and_rate_caps_and_reset");

                Require(Stage2AudioPolicy.OutputGain(.28f, 0f, true) == 0f && Stage2AudioPolicy.OutputGain(.28f, 0f, false) == 0f,
                    "Muted audio must remain silent with or without a mixer.");
                Require(Mathf.Approximately(Stage2AudioPolicy.OutputGain(.28f, .5f, false), .14f) &&
                        Mathf.Approximately(Stage2AudioPolicy.OutputGain(.28f, .5f, true), .28f),
                    "Saved volume must be applied exactly once, including the mixer route.");
                AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/MyAudioMixer.mixer");
                Require(mixer != null && mixer.FindMatchingGroups("SFX").Length > 0 && mixer.FindMatchingGroups("Music").Length > 0,
                    "Existing settings mixer routes were not found.");
                Require(Stage2AudioPolicy.IsConfirmedRankImprovement(12, 9, "round-confirmed") &&
                        !Stage2AudioPolicy.IsConfirmedRankImprovement(0, 9, "first-baseline") &&
                        !Stage2AudioPolicy.IsConfirmedRankImprovement(9, 9, "unchanged") &&
                        !Stage2AudioPolicy.IsConfirmedRankImprovement(9, 12, "worse") &&
                        !Stage2AudioPolicy.IsConfirmedRankImprovement(12, 9, " "),
                    "Rank accent must require a valid confirmed improvement identity.");
                checks.Add("existing_sound_music_settings_routes_and_confirmed_rank_guards");

                Debug.Log("STAGE2_AUDIO_VALIDATION " + JsonUtility.ToJson(new Result { passed = true, checks = checks.ToArray(),
                    pcmBytes = bytes, maximumVoices = Stage2AudioPolicy.MaxAbsorptionVoices, stage2PeakBound = Stage2AudioPolicy.MaximumStage2PeakBound, error = "" }));
            }
            catch (Exception exception)
            {
                Debug.LogError("STAGE2_AUDIO_VALIDATION " + JsonUtility.ToJson(new Result { passed = false, checks = checks.ToArray(), error = exception.ToString() }));
                throw;
            }
        }

        private static void ValidatePcm(string name, byte[] bytes)
        {
            Require(bytes.Length > 44 && Text(bytes, 0, 4) == "RIFF" && Text(bytes, 8, 4) == "WAVE", "Invalid WAV: " + name);
            int data = 0, dataLength = 0;
            for (int offset = 12; offset + 8 <= bytes.Length;)
            {
                int count = BitConverter.ToInt32(bytes, offset + 4);
                Require(count >= 0 && offset + 8L + count <= bytes.Length, "Corrupt WAV chunk: " + name);
                if (Text(bytes, offset, 4) == "fmt ")
                    Require(count >= 16 && BitConverter.ToInt16(bytes, offset + 8) == 1 &&
                            BitConverter.ToInt16(bytes, offset + 10) == 1 && BitConverter.ToInt32(bytes, offset + 12) == 44100 &&
                            BitConverter.ToInt16(bytes, offset + 22) == 16, "Expected 44.1kHz mono 16-bit PCM: " + name);
                if (Text(bytes, offset, 4) == "data") { data = offset + 8; dataLength = count; }
                offset += 8 + count + (count & 1);
            }
            Require(data > 0 && dataLength > 0 && (dataLength & 1) == 0, "Missing WAV PCM payload: " + name);
            double sum = 0, squares = 0, peak = 0;
            for (int offset = data; offset < data + dataLength; offset += 2)
            {
                double sample = BitConverter.ToInt16(bytes, offset) / 32767.0;
                sum += sample; squares += sample * sample; peak = Math.Max(peak, Math.Abs(sample));
            }
            int samples = dataLength / 2;
            double rms = Math.Sqrt(squares / samples);
            Require(peak <= (name == "RankImproved" ? .6201 : .7201) && rms > .04 && rms < .3 && Math.Abs(sum / samples) < .003,
                "Unsafe peak/RMS/DC in " + name);
            Require(BitConverter.ToInt16(bytes, data) == 0 && BitConverter.ToInt16(bytes, data + dataLength - 2) == 0,
                "PCM endpoints must be silent to avoid clicks: " + name);
        }

        private static string Text(byte[] bytes, int start, int count) => System.Text.Encoding.ASCII.GetString(bytes, start, count);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
