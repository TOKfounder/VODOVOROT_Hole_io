using UnityEngine;

namespace Vodovorot.Stage2
{
    /// <summary>Call on a real scored absorption and on a server-confirmed rank improvement only.</summary>
    public static class Stage2Audio
    {
        private static AbsorptionAudioPool instance;

        /// <summary>Does not create a pool, reserve a voice or bypass the normal rank accent limiter.</summary>
        public static double NextRankAccentAt => instance != null ? instance.NextRankAccentAt : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public static void PlayAbsorption(HoleParent owner, int value, Vector3 physicalSize)
        {
            if (!Application.isPlaying || owner == null || value <= 0 ||
                (owner.holeType != HoleParent.TypeOfHole.player && owner.holeType != HoleParent.TypeOfHole.playerHelper))
                return;
            GetPool().EnqueueAbsorption(value, physicalSize);
        }

        public static void PlayConfirmedRankImprovement(int previousRank, int currentRank, string confirmationId)
        {
            if (!Application.isPlaying || !Stage2AudioPolicy.IsConfirmedRankImprovement(previousRank, currentRank, confirmationId))
                return;
            GetPool().PlayRankAccent(confirmationId);
        }

        private static AbsorptionAudioPool GetPool()
        {
            if (instance != null) return instance;
            GameObject root = new GameObject("Stage 2 absorption audio");
            Object.DontDestroyOnLoad(root);
            instance = root.AddComponent<AbsorptionAudioPool>();
            return instance;
        }
    }
}
