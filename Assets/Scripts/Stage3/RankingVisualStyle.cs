using System;
using System.Text;
using UnityEngine;

namespace Vodovorot.Stage3
{
    /// <summary>
    /// Explicit author-style catalog shared by ranking, results, cards and browser artwork.
    /// References come from MainMenu/PlayButton and the game's existing GUI atlas;
    /// no runtime scene search, importer change, or global UI restyle is required.
    /// </summary>
    [CreateAssetMenu(menuName = "Vodovorot/Ranking visual style")]
    public sealed class RankingVisualStyle : ScriptableObject
    {
        public const string ResourcePath = "Stage3/RankingVisualStyle";
        private static RankingVisualStyle cached;

        [Header("Canonical game assets")]
        public Font comicFont;
        [Tooltip("Canonical PlayButton outline. Its center is translucent, so it is never used as a fill.")]
        public Sprite roundedShape;
        public Sprite filledShape;
        public Sprite goldMedal;
        public Sprite goldStar;

        [Header("Shared semantic roles")]
        public Color ink = new Color32(17, 27, 34, 255);
        public Color paper = new Color32(238, 250, 255, 255);
        public Color blue = new Color32(31, 157, 231, 255);
        public Color green = new Color32(89, 214, 37, 255);
        public Color amber = new Color32(255, 196, 47, 255);
        public Color lightBlue = new Color32(205, 238, 252, 255);
        public Color outline = Color.black;
        public Color backdrop = new Color32(5, 24, 36, 210);
        [Min(2)] public float border = 3f;
        [Min(48)] public float minimumButtonHeight = 52f;
        [Min(1)] public float textOutline = 1.4f;

        public static RankingVisualStyle Load()
        {
            if (cached == null) cached = Resources.Load<RankingVisualStyle>(ResourcePath);
            if (cached == null || cached.comicFont == null || cached.roundedShape == null || cached.filledShape == null ||
                cached.goldMedal == null || cached.goldStar == null)
                throw new InvalidOperationException("Missing canonical ranking style. Run Tools/Vodovorot/Stage 3/Build ranking visual style.");
            return cached;
        }

        /// <summary>Same role names and CSS-pixel measurements for a future browser consumer.</summary>
        public string ToCssVariables()
        {
            var css = new StringBuilder(420);
            css.Append(":root {\n  --vodo-font: 'Comic Sans MS', cursive;\n");
            Append(css, "ink", ink); Append(css, "paper", paper); Append(css, "blue", blue);
            Append(css, "green", green); Append(css, "amber", amber); Append(css, "light-blue", lightBlue);
            Append(css, "outline", outline); Append(css, "backdrop", backdrop);
            css.Append("  --vodo-border: ").Append(border.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("px;\n");
            css.Append("  --vodo-button-min-height: ").Append(minimumButtonHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("px;\n}\n");
            return css.ToString();
        }

        private static void Append(StringBuilder css, string role, Color color) =>
            css.Append("  --vodo-").Append(role).Append(": #").Append(ColorUtility.ToHtmlStringRGBA(color)).Append(";\n");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { cached = null; }
    }
}
