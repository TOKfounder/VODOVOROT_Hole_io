using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Vodovorot.Stage2
{
    /// <summary>Additional actions on the existing results, without replacing its rewards or ad buttons.</summary>
    public sealed class Stage2RoundPresentation : MonoBehaviour
    {
        private GamingManager match;
        private bool initialized;

        public static void Install(EndGameController results, GamingManager gamingManager)
        {
            var presentation = results.GetComponent<Stage2RoundPresentation>();
            if (presentation == null) presentation = results.gameObject.AddComponent<Stage2RoundPresentation>();
            presentation.match = gamingManager;
            if (!presentation.initialized) presentation.StartCoroutine(presentation.Present());
        }

        private IEnumerator Present()
        {
            initialized = true;
            yield return null; // The original results have finished settling their base reward.
            var parent = transform as RectTransform;
            if (parent == null || match == null) yield break;

            bool ru = YG.YG2.saves.langRu;
            var bar = new GameObject("Stage2ResultActions", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(0.08f, 0.015f);
            rect.anchorMax = new Vector2(0.92f, 0.095f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Stage2Ui.Button(rect, ru ? "Рейтинг" : "Ranking", new Vector2(0, 0), new Vector2(0.48f, 1),
                () => LeaderboardRoundPanel.Show(match.Stage2RoundId, match.IsStage2RankRewardEligible));
            Stage2Ui.Button(rect, ru ? "Поделиться" : "Share", new Vector2(0.52f, 0), new Vector2(1, 1),
                () => AchievementCard.Show(match));

            LeaderboardRoundPanel.ShareRequested += Share;
            // Always dismissible; guest invitation is offered after gameplay, never at the entrance.
            LeaderboardRoundPanel.Show(match.Stage2RoundId, match.IsStage2RankRewardEligible);
        }

        private void Share() { if (match != null) AchievementCard.Show(match); }

        private void OnDestroy()
        {
            LeaderboardRoundPanel.ShareRequested -= Share;
        }
    }

    internal static class Stage2Ui
    {
        internal static readonly Color Ink = new Color(0.16f, 0.13f, 0.25f);
        internal static readonly Color Paper = new Color(1f, 0.98f, 0.92f);
        internal static readonly Color Accent = new Color(0.25f, 0.15f, 0.57f);
        private static Font font;
        internal static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static Text Text(Transform parent, string text, int size, Vector2 min, Vector2 max, Color? color = null)
        {
            var label = Rect(parent, "Label", min, max).gameObject.AddComponent<Text>();
            label.font = Font; label.text = text; label.fontSize = size;
            label.color = color ?? Ink; label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.Max(14, size / 2);
            label.resizeTextMaxSize = size; label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }

        internal static Button Button(Transform parent, string text, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(parent, "Button_" + text, min, max);
            var image = rect.gameObject.AddComponent<Image>(); image.color = Accent;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.8f);
            button.colors = colors;
            Text(rect, text, 29, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), Color.white);
            button.onClick.AddListener(action);
            return button;
        }

        internal static RectTransform Overlay(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            var dim = Rect(go.transform, "Backdrop", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            dim.color = new Color(0.07f, 0.05f, 0.13f, 0.86f);
            return (RectTransform)go.transform;
        }
    }
}
