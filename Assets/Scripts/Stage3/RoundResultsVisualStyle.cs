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

        /// <summary>Reflows the existing results objects in actual CSS pixels; never changes rewards, sprites or button actions.</summary>
        public static void Layout(EndGameController results, GamingManager gaming, RectTransform footer,
            Button rankingButton, Button shareButton, float cssRatio)
        {
            if (!Application.isPlaying || results == null || gaming == null || footer == null) return;
            RectTransform panel = results.transform as RectTransform;
            Canvas canvas = panel != null ? panel.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) return;
            Canvas root = canvas.rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            if (root.renderMode != RenderMode.ScreenSpaceOverlay && camera == null) return;
            Rect pixels = camera != null ? camera.pixelRect : root.pixelRect;
            cssRatio = Mathf.Clamp(cssRatio, .25f, 8f);
            Rect view = new Rect(pixels.x / cssRatio, pixels.y / cssRatio, pixels.width / cssRatio, pixels.height / cssRatio);
            if (view.width < 240 || view.height < 280) return;
            bool wide = view.width > view.height;
            float panelWidth = Mathf.Min(view.width - 24, wide ? 960 : 560);
            float panelHeight = Mathf.Min(view.height - 24, wide ? 560 : 620);
            Rect paper = new Rect(view.center.x - panelWidth * .5f, view.center.y - panelHeight * .5f, panelWidth, panelHeight);
            PlaceScreen(panel, paper, camera, cssRatio);
            bool mobile = gaming.MobpanelOfEnd == results.gameObject;
            Text[] labels = mobile ? gaming.PanelOfEnd : gaming.DPanelOfEnd;
            Text title = At(labels, mobile ? 0 : 1);
            Text xpLabel = At(labels, mobile ? 1 : 0);
            Text xp = mobile ? results.MexpText : results.expText;
            Text coins = mobile ? results.McoinText : results.coinText;
            Text diamonds = mobile ? results.MbrillText : results.brillText;
            Button continueButton = At(labels, 4)?.GetComponentInParent<Button>();
            Button adButton = At(labels, 5)?.GetComponentInParent<Button>();
            float padding = wide ? Mathf.Clamp(paper.height * .06f, 12, 24) : 20;
            Rect inside = new Rect(paper.x + padding, paper.y + padding, paper.width - padding * 2, paper.height - padding * 2);
            float titleHeight = wide ? Mathf.Clamp(paper.height * .14f, 36, 56) : 56;
            float gap = wide ? Mathf.Clamp(view.height * .03f, 8, 12) : 12;
            float footerGap = wide ? Mathf.Clamp(view.height * .04f, 8, 16) : 20;
            float titleWidth = Mathf.Min(inside.width, 360);
            Rect heading = new Rect(inside.center.x - titleWidth * .5f, inside.yMax - titleHeight, titleWidth, titleHeight);
            float medalWidth = titleHeight * .73f;
            Rect titleBox = new Rect(heading.x + medalWidth + 10, heading.y, heading.width - medalWidth - 10, heading.height);
            PlaceText(title, titleBox, camera, cssRatio, 32, 24, TextAnchor.MiddleCenter);
            if (results.resultImage != null)
                PlaceScreen(results.resultImage.rectTransform, new Rect(heading.x, heading.y, medalWidth, titleHeight), camera, cssRatio);
            float bodyTop = heading.yMin - gap;
            Rect stats, actions;
            if (wide)
            {
                float columnWidth = (inside.width - 24) * .5f;
                stats = new Rect(inside.x, inside.y, columnWidth, bodyTop - inside.y);
                actions = new Rect(inside.xMax - columnWidth, inside.y, columnWidth, bodyTop - inside.y);
            }
            else
            {
                // Three separate rows of actions reserve real space for Continue,
                // rewarded x3 and the new footer. Nothing is placed over an ad.
                float actionHeight = 64 + 12 + 64 + 20 + 52;
                actions = new Rect(inside.x, inside.y, inside.width, actionHeight);
                stats = new Rect(inside.x, actions.yMax + 16, inside.width, bodyTop - actions.yMax - 16);
            }
            float rowGap = 8;
            float rowHeight = Mathf.Min(64, (stats.height - rowGap * 2) / 3);
            float rowsTop = stats.center.y + (rowHeight * 3 + rowGap * 2) * .5f;
            RewardRow(xpLabel, xp, "Image (1)", new Rect(stats.x, rowsTop - rowHeight, stats.width, rowHeight), camera, cssRatio);
            RewardRow(At(labels, 2), coins, "Image", new Rect(stats.x, rowsTop - rowHeight * 2 - rowGap, stats.width, rowHeight), camera, cssRatio);
            RewardRow(At(labels, 3), diamonds, "Image", new Rect(stats.x, rowsTop - rowHeight * 3 - rowGap * 2, stats.width, rowHeight), camera, cssRatio);
            float buttonHeight = wide ? Mathf.Clamp((actions.height - gap - footerGap - 52) * .5f, 52, 64) : 64;
            float blockHeight = buttonHeight * 2 + gap + footerGap + 52;
            float blockBottom = actions.center.y - blockHeight * .5f;
            Rect footerBox = new Rect(actions.x, blockBottom, actions.width, 52);
            float adWidth = Mathf.Min(actions.width, buttonHeight * 3.58f), continueWidth = Mathf.Min(actions.width, buttonHeight * 3.27f);
            Rect adBox = new Rect(actions.center.x - adWidth * .5f, footerBox.yMax + footerGap, adWidth, buttonHeight);
            Rect continueBox = new Rect(actions.center.x - continueWidth * .5f, adBox.yMax + gap, continueWidth, buttonHeight);
            PlaceButton(continueButton, At(labels, 4), continueBox, camera, cssRatio, 22, 18);
            PlaceButton(adButton, At(labels, 5), adBox, camera, cssRatio, 17, 15);
            PlaceScreen(footer, footerBox, camera, cssRatio);
            float half = (footerBox.width - 12) * .5f;
            PlaceButton(rankingButton, rankingButton != null ? rankingButton.GetComponentInChildren<Text>(true) : null,
                new Rect(footerBox.x, footerBox.y, half, footerBox.height), camera, cssRatio, 19, 16);
            PlaceButton(shareButton, shareButton != null ? shareButton.GetComponentInChildren<Text>(true) : null,
                new Rect(footerBox.x + half + 12, footerBox.y, half, footerBox.height), camera, cssRatio, 19, 16);
        }

        private static Text At(Text[] labels, int index) => labels != null && index < labels.Length ? labels[index] : null;

        private static void RewardRow(Text label, Text number, string iconPath, Rect box, Camera camera, float ratio)
        {
            float icon = Mathf.Min(42, box.height - 8), valueWidth = Mathf.Min(76, box.width * .22f);
            float labelWidth = box.width - icon - valueWidth - 16;
            PlaceText(label, new Rect(box.x, box.y, labelWidth, box.height), camera, ratio, 24, 18, TextAnchor.MiddleRight);
            Rect iconBox = new Rect(box.x + labelWidth + 8, box.center.y - icon * .5f, icon, icon);
            PlaceText(number, new Rect(iconBox.xMax + 8, box.y, valueWidth, box.height), camera, ratio, 26, 20, TextAnchor.MiddleLeft);
            // These exact icon children are authored on the three numeric Texts
            // in both GamingPrefabs/ImmersivePack results panels.
            RectTransform image = number != null ? number.transform.Find(iconPath) as RectTransform : null;
            if (image != null) PlaceScreen(image, iconBox, camera, ratio);
        }

        private static void PlaceButton(Button button, Text label, Rect box, Camera camera, float ratio, int maximum, int minimum)
        {
            if (button == null) return;
            PlaceScreen(button.GetComponent<RectTransform>(), box, camera, ratio);
            PlaceText(label, new Rect(box.x + 8, box.y + 4, box.width - 16, box.height - 8),
                camera, ratio, maximum, minimum, TextAnchor.MiddleCenter);
            if (label == null) return;
            Outline outline = label.GetComponent<Outline>();
            if (outline != null)
            {
                float unit = ratio / PixelsPerUnit(label.rectTransform, camera);
                outline.effectDistance = new Vector2(1.4f * unit, -1.4f * unit);
            }
        }

        private static void PlaceText(Text text, Rect box, Camera camera, float ratio, int maximum, int minimum, TextAnchor alignment)
        {
            if (text == null) return;
            PlaceScreen(text.rectTransform, box, camera, ratio);
            float unit = ratio / PixelsPerUnit(text.rectTransform, camera);
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(minimum * unit));
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, Mathf.RoundToInt(maximum * unit));
            text.fontSize = text.resizeTextMaxSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static float PixelsPerUnit(RectTransform rect, Camera camera)
        {
            Vector2 from = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            Vector2 to = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.up));
            return Mathf.Max(.001f, Vector2.Distance(from, to));
        }

        private static void PlaceScreen(RectTransform rect, Rect css, Camera camera, float ratio)
        {
            if (rect == null || rect.parent == null) return;
            Vector2 center = css.center * ratio;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(css.xMin * ratio, center.y), camera, out Vector2 left) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(css.xMax * ratio, center.y), camera, out Vector2 right) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(center.x, css.yMin * ratio), camera, out Vector2 bottom) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(center.x, css.yMax * ratio), camera, out Vector2 top)) return;
            float width = right.x - left.x, height = top.y - bottom.y;
            if (width <= 0 || height <= 0) return;
            Vector3 pivot = new Vector3(left.x + rect.pivot.x * width, bottom.y + rect.pivot.y * height, 0);
            Vector3 destination = rect.parent.InverseTransformPoint(rect.TransformPoint(pivot));
            Vector2 middle = new Vector2(.5f, .5f);
            if (rect.anchorMin != middle) rect.anchorMin = middle;
            if (rect.anchorMax != middle) rect.anchorMax = middle;
            if (Mathf.Abs(rect.rect.width - width) > .001f) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (Mathf.Abs(rect.rect.height - height) > .001f) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Vector3 position = rect.localPosition;
            if (Mathf.Abs(position.x - destination.x) > .001f || Mathf.Abs(position.y - destination.y) > .001f)
                rect.localPosition = new Vector3(destination.x, destination.y, position.z);
        }

        private static void PaperPanel(GameObject panel, RankingVisualStyle style)
        {
            if (panel == null) return;
            Image image = panel.GetComponent<Image>();
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
