using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Vodovorot.Stage3;

namespace Vodovorot.Stage2
{
    /// <summary>Additional actions on the existing results, without replacing its rewards or ad buttons.</summary>
    public sealed class Stage2RoundPresentation : MonoBehaviour
    {
        private GamingManager match;
        private EndGameController results;
        private bool initialized;
        [SerializeField] private RectTransform actionBar;
        [SerializeField] private Button rankingButton;
        [SerializeField] private Button shareButton;
        public bool HasAuthoredActions => actionBar != null && rankingButton != null && shareButton != null;

        public static void Install(EndGameController results, GamingManager gamingManager)
        {
            RoundResultsVisualStyle.Apply(results, gamingManager);
            var presentation = results.GetComponent<Stage2RoundPresentation>();
            if (presentation == null) presentation = results.gameObject.AddComponent<Stage2RoundPresentation>();
            presentation.match = gamingManager;
            presentation.results = results;
            if (!presentation.initialized) presentation.StartCoroutine(presentation.Present());
        }

        private IEnumerator Present()
        {
            initialized = true;
            yield return null; // The original results have finished settling their base reward.
            var parent = transform as RectTransform;
            if (parent == null || match == null) yield break;

            if (!HasAuthoredActions)
            {
                Debug.LogError("Round results require their saved action bar and buttons.");
                yield break;
            }
            rankingButton.onClick.AddListener(OpenRanking);
            shareButton.onClick.AddListener(Share);

            LeaderboardRoundPanel.ShareRequested += Share;
            // Always dismissible; guest invitation is offered after gameplay, never at the entrance.
            LeaderboardRoundPanel.Show(match.Stage2RoundId, match.IsStage2RankRewardEligible);
            LayoutActions();
        }

        private void LateUpdate() => LayoutActions();

        private void LayoutActions()
        {
            if (actionBar == null) return;
            bool ru = YG.YG2.saves == null || YG.YG2.saves.langRu;
            rankingButton.GetComponentInChildren<Text>(true).text = ru ? "Рейтинг" : "Ranking";
            shareButton.GetComponentInChildren<Text>(true).text = ru ? "Поделиться" : "Share";
            // Native saved layout groups own all geometry. They wrap columns in
            // Canvas units; no per-element screen/DPR conversion runs here.
        }

        private void OpenRanking()
        {
            if (match != null)
                LeaderboardRoundPanel.Show(match.Stage2RoundId, match.IsStage2RankRewardEligible);
        }

        private void Share() { if (match != null) AchievementCard.Show(match); }

        private void OnDestroy()
        {
            LeaderboardRoundPanel.ShareRequested -= Share;
            if (rankingButton != null) rankingButton.onClick.RemoveListener(OpenRanking);
            if (shareButton != null) shareButton.onClick.RemoveListener(Share);
        }
    }

    internal static class Stage2Ui
    {
        internal static Color Ink => RankingVisualStyle.Load().ink;
        internal static Color Paper => RankingVisualStyle.Load().paper;
        internal static Color Accent => RankingVisualStyle.Load().blue;
        internal static Font Font => RankingVisualStyle.Load().comicFont;

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

        internal static Button Button(Transform parent, string text, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action, Color? color = null)
        {
            var button = RankingUiStyle.Button("Button_" + text, parent, RankingVisualStyle.Load(), color ?? Accent, action);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text label = button.GetComponentInChildren<Text>(); label.text = text; label.fontSize = 29;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = 29;
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
            dim.color = RankingVisualStyle.Load().backdrop;
            return (RectTransform)go.transform;
        }
    }
}
