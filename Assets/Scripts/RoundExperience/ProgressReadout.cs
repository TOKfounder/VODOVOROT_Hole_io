using System;
using System.Globalization;

namespace Vodovorot.RoundExperience
{
    public static class ProgressReadout
    {
        public static string Capture(float fraction, bool russian)
        {
            float percent = Math.Max(0f, Math.Min(1f, fraction)) * 100f;
            string value = percent.ToString("0.0", CultureInfo.InvariantCulture);
            return (russian ? value.Replace('.', ',') : value) + "%";
        }

        public static string Growth(int level, int remaining, bool russian)
        {
            string size = russian ? "Размер " : "Size ";
            if (remaining <= 0)
                return size + (level + 1) + (russian ? " · Максимум!" : " · Maximum!");
            return size + (level + 1) + (russian ? " · До роста: " : " · Next size: ") + remaining;
        }

        public static string AccountGoal(int experience, int perLevel, bool russian)
        {
            int safeExperience = Math.Max(0, experience);
            int required = Math.Max(1, perLevel);
            int nextLevel = safeExperience / required + 1;
            int remaining = required - safeExperience % required;
            return russian
                ? "До уровня " + nextLevel + ": " + remaining + " опыта"
                : remaining + " XP to level " + nextLevel;
        }
    }
}
