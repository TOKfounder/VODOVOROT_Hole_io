using System;

namespace Vodovorot.RoundExperience
{
    public static class RewardedProgressRules
    {
        public static int Remaining(int progress, int requiredAds)
        {
            int required = Math.Max(1, requiredAds);
            return required - Math.Max(0, Math.Min(required - 1, progress));
        }

        public static int Advance(ref int progress, int requiredAds, int reward)
        {
            int required = Math.Max(1, requiredAds);
            progress = required - Remaining(progress, required) + 1;
            if (progress < required) return 0;
            progress = 0;
            return Math.Max(0, reward);
        }
    }
}
