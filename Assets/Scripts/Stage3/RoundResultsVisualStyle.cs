using UnityEngine;
using UnityEngine.UI;

namespace Vodovorot.Stage3
{
    /// <summary>Styles the authored round results and in-game settings through their explicit controller references.</summary>
    public static class RoundResultsVisualStyle
    {
        public static void Apply(EndGameController results, GamingManager gaming)
        {
            if (!Application.isPlaying || gaming == null) return;
            RankingVisualStyle style = RankingVisualStyle.Load();
            PaperPanel(gaming.MobpanelOfEnd, style);
            PaperPanel(gaming.DeskpanelOfEnd, style);
            ResultLabels(gaming.PanelOfEnd, style);
            ResultLabels(gaming.DPanelOfEnd, style);
            Numbers(results, style);
            // Each authored panel has its own EndGameController. Include both
            // explicit roots because some mobile numeric references are shared.
            if (gaming.MobpanelOfEnd != null) Numbers(gaming.MobpanelOfEnd.GetComponent<EndGameController>(), style);
            if (gaming.DeskpanelOfEnd != null) Numbers(gaming.DeskpanelOfEnd.GetComponent<EndGameController>(), style);
            ApplySettings(gaming);
        }

        public static void ApplySettings(GamingManager gaming)
        {
            if (!Application.isPlaying || gaming == null) return;
            RankingVisualStyle style = RankingVisualStyle.Load();
            SettingsLabels(gaming.MobilePanelOfSettings, style);
            SettingsLabels(gaming.DesktopPanelOfSettings, style);
        }

        private static void PaperPanel(GameObject panel, RankingVisualStyle style)
        {
            if (panel == null) return;
            Transform authoredPaper = panel.transform.Find("Paper background");
            Image image = authoredPaper != null ? authoredPaper.GetComponent<Image>() : panel.GetComponent<Image>();
            // Both actual GamingPrefabs/ImmersivePack panels use app-fill.png:
            // its interior is opaque white; gray comes only from Image.color.
            // Keep the author's sprite, cutout shape, shadow and image settings.
            if (image != null) image.color = style.paper;
        }

        private static void ResultLabels(Text[] labels, RankingVisualStyle style)
        {
            if (labels == null) return;
            for (int index = 0; index < Mathf.Min(4, labels.Length); index++) Ink(labels[index], style);
            for (int index = 4; index < Mathf.Min(6, labels.Length); index++) Outlined(labels[index], style, Color.white);
        }

        private static void Numbers(EndGameController results, RankingVisualStyle style)
        {
            if (results == null) return;
            Ink(results.MexpText, style); Ink(results.McoinText, style); Ink(results.MbrillText, style);
            Ink(results.expText, style); Ink(results.coinText, style); Ink(results.brillText, style);
        }

        private static void Ink(Text text, RankingVisualStyle style)
        {
            if (text == null) return;
            text.font = style.comicFont;
            text.color = style.ink;
            // Dark text on opaque paper needs no additional black glyph copies.
            // Outline derives from Shadow; disable either effect only on these labels.
            foreach (Shadow shadow in text.GetComponents<Shadow>()) shadow.enabled = false;
        }

        private static void SettingsLabels(Text[] labels, RankingVisualStyle style)
        {
            if (labels == null) return;
            for (int index = 0; index < Mathf.Min(5, labels.Length); index++) Outlined(labels[index], style, style.paper);
        }

        private static void Outlined(Text text, RankingVisualStyle style, Color color)
        {
            if (text == null) return;
            text.font = style.comicFont;
            text.color = color;
            Outline outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = style.outline;
            float distance = Mathf.Max(style.textOutline, Mathf.Abs(outline.effectDistance.x), Mathf.Abs(outline.effectDistance.y));
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }
    }
}
