using System;
using UnityEngine;

namespace Vodovorot.Stage2
{
    /// <summary>Cosmetic audio tuning only. Never changes score, physics, skin ownership or movement.</summary>
    public static class Stage2AudioPolicy
    {
        public const int MaxAbsorptionVoices = 3;
        public const int MaxQueuedAbsorptions = 6;
        public const double MinimumNoteInterval = .085;
        public const double MaximumQueueAge = .5;
        public const double SeriesWindow = .42;
        public const float MaximumVoiceGain = .28f;
        public const float RankAccentGain = .24f;
        public const float MinimumPitch = .86f;
        public const float MaximumPitch = 1.16f;
        public const float MaximumAbsorptionClipPeak = .72f;
        public const float MaximumAccentClipPeak = .62f;
        public const float MaximumStage2PeakBound = MaxAbsorptionVoices * MaximumVoiceGain * MaximumAbsorptionClipPeak
                                                   + RankAccentGain * MaximumAccentClipPeak;

        public struct Note
        {
            public string resource;
            public float pitch;
            public float gain;
            public float weight;
        }

        public static Note AbsorptionNote(int value, Vector3 physicalSize, int equippedSkin, int seriesIndex, float variation)
        {
            float longest = Mathf.Max(SafeMagnitude(physicalSize.x), SafeMagnitude(physicalSize.y), SafeMagnitude(physicalSize.z));
            float valueWeight = Mathf.Clamp01(Mathf.Log(Mathf.Clamp(value, 1, 100), 2f) / 6.643856f);
            float sizeWeight = Mathf.Clamp01(Mathf.Log(1f + longest) / Mathf.Log(13f));
            float weight = Mathf.Max(valueWeight, sizeWeight * .8f);
            bool deep = value >= 5 || longest >= 1.2f;
            string resource = equippedSkin == 2
                ? (deep ? "Stage2Audio/AbsorbGoldDeep" : "Stage2Audio/AbsorbGoldLight")
                : equippedSkin == 4
                    ? (deep ? "Stage2Audio/AbsorbLordDeep" : "Stage2Audio/AbsorbLordLight")
                    : (deep ? "Stage2Audio/AbsorbBaseDeep" : "Stage2Audio/AbsorbBaseLight");
            if (float.IsNaN(variation) || float.IsInfinity(variation)) variation = 0f;
            float pitch = Mathf.Lerp(1.07f, .89f, weight)
                          + Mathf.Clamp(seriesIndex, 0, 4) * .014f
                          + Mathf.Clamp(variation, -1f, 1f) * .012f;
            return new Note
            {
                resource = resource,
                pitch = Mathf.Clamp(pitch, MinimumPitch, MaximumPitch),
                gain = Mathf.Lerp(.23f, MaximumVoiceGain, weight),
                weight = weight
            };
        }

        public static bool IsConfirmedRankImprovement(int previousRank, int currentRank, string confirmationId)
        {
            return previousRank > 0 && currentRank > 0 && currentRank < previousRank
                   && !string.IsNullOrWhiteSpace(confirmationId);
        }

        public static float SavedVolume(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }

        public static float OutputGain(float gain, float savedVolume, bool routedThroughSettingsMixer)
        {
            float setting = SavedVolume(savedVolume);
            return setting <= 0f ? 0f : Mathf.Clamp(gain, 0f, MaximumVoiceGain) * (routedThroughSettingsMixer ? 1f : setting);
        }

        private static float SafeMagnitude(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Min(Mathf.Abs(value), 1000f);
        }
    }

    /// <summary>Uses DSP time rather than frame count, so a cascade cannot exceed the voice or rate cap.</summary>
    public sealed class AbsorptionAudioLimiter
    {
        private readonly double[] ends = new double[Stage2AudioPolicy.MaxAbsorptionVoices];
        private double nextAllowed = double.NegativeInfinity;
        private double lastNote = double.NegativeInfinity;
        private int seriesIndex;

        public int NextSeriesIndex(double now)
        {
            return now - lastNote <= Stage2AudioPolicy.SeriesWindow ? Math.Min(seriesIndex + 1, 4) : 0;
        }

        public bool TryAcquire(double now, double duration, out int voice)
        {
            voice = -1;
            if (double.IsNaN(now) || double.IsInfinity(now) || duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration))
                return false;
            if (now < nextAllowed) return false;
            for (int i = 0; i < ends.Length; i++)
            {
                if (ends[i] > now) continue;
                voice = i;
                seriesIndex = NextSeriesIndex(now);
                lastNote = now;
                nextAllowed = now + Stage2AudioPolicy.MinimumNoteInterval;
                ends[i] = now + duration;
                return true;
            }
            return false;
        }

        public int ActiveVoices(double now)
        {
            int count = 0;
            foreach (double end in ends) if (end > now) count++;
            return count;
        }

        public void Reset()
        {
            Array.Clear(ends, 0, ends.Length);
            nextAllowed = lastNote = double.NegativeInfinity;
            seriesIndex = 0;
        }
    }
}
