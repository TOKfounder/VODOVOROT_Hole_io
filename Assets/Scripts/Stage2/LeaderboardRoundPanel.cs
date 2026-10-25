using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YG;

namespace Vodovorot.Stage2
{
    /// <summary>Optional, dismissible ranking view over the settled round result.</summary>
    public sealed class LeaderboardRoundPanel : MonoBehaviour
    {
        public static event Action ShareRequested;
        private static LeaderboardRoundPanel instance;
        private string roundId;
        private RectTransform panel;
        private RectTransform safeRoot, viewport, body;
        private ScrollRect scroll;
        private Rect lastSafeArea;
        private Text title, statusText, rankText, goalText, bonusText;
        private RectTransform rowsRoot;
        private readonly List<RankRow> rows = new List<RankRow>();
        private float rowHeight;
        private bool compactRows;
        private sealed class RankRow
        {
            public RectTransform rect;
            public Image background;
            public Text label;
            public float startY, targetY;
        }
        private Button loginButton, shareButton, closeButton;
        private Canvas canvas;
        private Font font;
        private Sprite roundedSprite;
        private Coroutine celebration;
        private int lastWidth, lastHeight;
        private float lastRatio;
        private bool ru => YG2.saves == null || YG2.saves.langRu;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern float LeaderboardStage2_CssRatio();
        [DllImport("__Internal")] private static extern float LeaderboardStage2_SafeInset(int edge);
#endif
        private static float CssRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Mathf.Clamp(LeaderboardStage2_CssRatio(), .25f, 8f);
#else
            return 1f;
#endif
        }

        public static void Show(string roundId, bool eligibleRound)
        {
            if (string.IsNullOrEmpty(roundId)) return;
            if (instance == null)
            {
                var go = new GameObject("Round leaderboard", typeof(RectTransform));
                instance = go.AddComponent<LeaderboardRoundPanel>();
                instance.Build();
            }
            instance.roundId = roundId;
            instance.gameObject.SetActive(true);
            instance.Layout();
            instance.body.anchoredPosition = Vector2.zero;
            LeaderboardRoundService.BeginRound(roundId, eligibleRound);
        }

        public static void Close()
        {
            if (instance != null) instance.gameObject.SetActive(false);
        }

        private void OnEnable() { LeaderboardRoundService.Changed += Render; }
        private void OnDisable()
        {
            LeaderboardRoundService.Changed -= Render;
            if (celebration != null) StopCoroutine(celebration);
            celebration = null;
        }
        private void OnDestroy() { if (instance == this) instance = null; }

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Reuse a sliced sprite already used by this game's menu, when available.
            foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
                if (image != null && image.sprite != null && image.type == Image.Type.Sliced)
                { roundedSprite = image.sprite; break; }
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = CssRatio();
            if (EventSystem.current == null)
                new GameObject("Leaderboard UI events", typeof(EventSystem), typeof(StandaloneInputModule));
            Image backdrop = gameObject.AddComponent<Image>();
            backdrop.color = new Color(0, 0, 0, .65f);
            safeRoot = NewRect("Safe area", transform);
            panel = NewRect("Panel", safeRoot);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(231, 231, 231, 255);
            background.sprite = roundedSprite;
            background.type = Image.Type.Sliced;
            title = Label("Title", panel, 24, FontStyle.Bold);
            viewport = NewRect("Scrollable body", panel);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            body = NewRect("Content", viewport);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = body;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            statusText = Label("Status", body, 15);
            rankText = Label("Own rank", body, 36, FontStyle.Bold);
            rankText.alignment = TextAnchor.MiddleCenter;
            rankText.resizeTextForBestFit = true;
            rankText.resizeTextMinSize = 20;
            rankText.resizeTextMaxSize = 36;
            rowsRoot = NewRect("Neighbor rows", body);
            rowsRoot.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 5; i++)
            {
                RectTransform rect = NewRect("Rank row " + i, rowsRoot);
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = roundedSprite; image.type = Image.Type.Sliced;
                Text label = Label("Entry", rect, 14);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(9, 0); label.rectTransform.offsetMax = new Vector2(-9, 0);
                rows.Add(new RankRow { rect = rect, background = image, label = label });
            }
            goalText = Label("Next goal", body, 16, FontStyle.Bold);
            bonusText = Label("Bonus", body, 14);
            loginButton = Button("Sign in", panel, new Color32(50, 101, 182, 255), LeaderboardRoundService.RequestConnection);
            closeButton = Button("Back to result", panel, new Color32(50, 101, 182, 255), Close);
            shareButton = Button("Share", panel, new Color32(120, 182, 50, 255), () => { Close(); ShareRequested?.Invoke(); });
            Layout();
        }

        private void Update()
        {
            float ratio = CssRatio();
            RefreshConnectionButton(LeaderboardRoundService.Current);
            if (Screen.width != lastWidth || Screen.height != lastHeight || Mathf.Abs(lastRatio - ratio) > .01f || lastSafeArea != Screen.safeArea)
            {
                Layout();
                Render(LeaderboardRoundService.Current);
            }
        }

        private void Layout()
        {
            if (canvas == null || panel == null) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastRatio = CssRatio();
            GetComponent<CanvasScaler>().scaleFactor = lastRatio;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
#if UNITY_WEBGL && !UNITY_EDITOR
            float left = Mathf.Max(safe.xMin, LeaderboardStage2_SafeInset(0) * lastRatio);
            float right = Mathf.Min(safe.xMax, Screen.width - LeaderboardStage2_SafeInset(1) * lastRatio);
            float top = Mathf.Min(safe.yMax, Screen.height - LeaderboardStage2_SafeInset(2) * lastRatio);
            float bottom = Mathf.Max(safe.yMin, LeaderboardStage2_SafeInset(3) * lastRatio);
            safe = Rect.MinMaxRect(left, bottom, right, top);
#endif
            lastSafeArea = Screen.safeArea;
            safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            float width = Mathf.Max(1, Mathf.Min(580f, safe.width / lastRatio - 24f));
            float height = Mathf.Max(1, Mathf.Min(580f, safe.height / lastRatio - 24f));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(width, height);
            bool compact = height < 540f;
            compactRows = compact;
            rowHeight = compact ? 24f : 30f;
            float pad = 16f, inside = Mathf.Max(1, width - pad * 2);
            float headerHeight = compact ? 28f : 34f;
            float footerHeight = loginButton.gameObject.activeSelf ? 106f : 48f;
            float bodyTop = pad + headerHeight + 10f;
            float viewportHeight = Mathf.Max(1, height - bodyTop - pad - footerHeight - 10f);
            Place(title.rectTransform, pad, pad, inside, headerHeight);
            Place(viewport, pad, bodyTop, inside, viewportHeight);
            float y = 0, gap = compact ? 4f : 8f;
            Place(statusText.rectTransform, 0, y, inside, compact ? 38 : 54); y += (compact ? 38 : 54) + gap;
            Place(rankText.rectTransform, 0, y, inside, compact ? 36 : 58); y += (compact ? 36 : 58) + gap;
            Place(rowsRoot, 0, y, inside, compact ? 72 : 150); y += (compact ? 72 : 150) + gap;
            Place(goalText.rectTransform, 0, y, inside, compact ? 34 : 46); y += (compact ? 34 : 46) + gap;
            Place(bonusText.rectTransform, 0, y, inside, compact ? 34 : 42); y += compact ? 34 : 42;
            body.anchorMin = new Vector2(0, 1); body.anchorMax = new Vector2(1, 1);
            body.pivot = new Vector2(.5f, 1);
            body.sizeDelta = new Vector2(0, Mathf.Max(viewportHeight, y));
            float buttonY = height - pad - 48;
            Place(shareButton.GetComponent<RectTransform>(), pad, buttonY, (inside - 10) * .5f, 48);
            Place(closeButton.GetComponent<RectTransform>(), pad + (inside + 10) * .5f, buttonY, (inside - 10) * .5f, 48);
            Place(loginButton.GetComponent<RectTransform>(), pad, buttonY - 58, inside, 48);
            foreach (RankRow row in rows) row.label.fontSize = 14;
            rankText.fontSize = compact ? 30 : 36;
            rankText.resizeTextMaxSize = rankText.fontSize;
            title.text = ru ? "Ваше место в рейтинге" : "Your leaderboard rank";
            SetButtonLabel(shareButton, ru ? "Поделиться" : "Share");
            SetButtonLabel(closeButton, ru ? "К результатам" : "Back to results");
        }

        private void Render(LeaderboardRoundSnapshot data)
        {
            if (data == null || data.roundId != roundId || !gameObject.activeInHierarchy || rankText == null) return;
            if (celebration != null) StopCoroutine(celebration);
            celebration = null;
            rankText.transform.localScale = Vector3.one;
            foreach (RankRow row in rows) row.rect.gameObject.SetActive(false);
            RefreshConnectionButton(data);
            Layout();
            goalText.text = bonusText.text = "";
            rankText.text = "—";
            switch (data.status)
            {
                case LeaderboardRoundStatus.WaitingForProfile:
                    statusText.text = ru ? "Загружаем профиль. Можно вернуться к результатам." : "Loading your profile. You can return to results.";
                    break;
                case LeaderboardRoundStatus.Loading:
                    statusText.text = ru ? "Проверяем результат…" : "Checking your result…";
                    break;
                case LeaderboardRoundStatus.Authenticating:
                    statusText.text = ru ? "Входим… Можно вернуться к результатам." : "Signing in… You can return to results.";
                    break;
                case LeaderboardRoundStatus.Guest:
                    statusText.text = ru ? "Войдите с Яндекс ID, чтобы видеть своё место и сохранять прогресс в облаке. Можно играть без входа." : "Sign in with Yandex ID to see your rank and use cloud saves. You can keep playing as a guest.";
                    break;
                case LeaderboardRoundStatus.Pending:
                    statusText.text = ru ? "Место пока не обновилось. Проверьте после следующего раунда." : "Your rank has not updated yet. Check after your next round.";
                    ShowLastKnown();
                    break;
                case LeaderboardRoundStatus.Unavailable:
                    statusText.text = LeaderboardRoundService.CanRetryProfileLoad
                        ? (ru ? "Профиль пока недоступен. Повторите подключение." : "Your profile is unavailable. Try reconnecting.")
                        : (ru ? "Рейтинг сейчас недоступен. Можно продолжать играть." : "Leaderboard unavailable. You can keep playing.");
                    ShowLastKnown();
                    break;
                case LeaderboardRoundStatus.Confirmed:
                    statusText.text = ru ? "Место обновлено" : "Rank updated";
                    rankText.text = "#" + data.rank;
                    bool animate = LeaderboardRoundService.TryConsumeCelebration(roundId);
                    RenderNeighbors(data, animate);
                    if (data.rank == 1) goalText.text = ru ? "Вы на первом месте!" : "You are in first place!";
                    else if (data.targetRank > 0)
                        goalText.text = ru ? "До результата #" + data.targetRank + ": ещё " + data.targetXp + " опыта" :
                            "To pass #" + data.targetRank + ": " + data.targetXp + " more XP";
                    else goalText.text = ru ? "Сыграйте ещё раунд, чтобы подняться выше." : "Play another round to climb higher.";
                    int gained = data.improved ? data.previousRank - data.rank : 0;
                    string movement = gained > 0 ? "+" + gained + (ru ? " мест" : " places") : "";
                    bonusText.text = movement + (data.bonusCoins > 0 ? (movement.Length > 0 ? "  ·  " : "") +
                        "+" + data.bonusCoins + (ru ? " монет за новый рекорд" : " coins for a new best") : "");
                    if (animate)
                    {
                        if (celebration != null) StopCoroutine(celebration);
                        celebration = StartCoroutine(AnimateImprovement(data));
                        Stage2Audio.PlayConfirmedRankImprovement(data.previousRank, data.rank, roundId + ":" + data.rank);
                    }
                    break;
            }
        }

        private void RefreshConnectionButton(LeaderboardRoundSnapshot data)
        {
            if (data == null || data.roundId != roundId || loginButton == null) return;
            bool retry = data.status == LeaderboardRoundStatus.Unavailable && LeaderboardRoundService.CanRetryProfileLoad;
            bool guest = data.status == LeaderboardRoundStatus.Guest && LeaderboardRoundService.CanUsePlatform;
            loginButton.gameObject.SetActive(retry || guest);
            float wait = retry ? LeaderboardRoundService.RetryAllowedIn : 0;
            loginButton.interactable = !retry || wait <= 0;
            string label = retry
                ? (wait > 0 ? (ru ? "Повторить через " : "Retry in ") + Mathf.CeilToInt(wait) + (ru ? " с" : "s")
                    : (ru ? "Повторить подключение" : "Reconnect"))
                : (ru ? "Войти с Яндекс ID" : "Sign in with Yandex ID");
            SetButtonLabel(loginButton, label);
        }

        private void ShowLastKnown()
        {
            if (LeaderboardRoundService.TryGetConfirmedRank(out int rank))
                goalText.text = (ru ? "Последнее известное место: #" : "Last known rank: #") + rank;
        }

        private void RenderNeighbors(LeaderboardRoundSnapshot data, bool animate)
        {
            var above = new List<LeaderboardNeighbor>();
            var below = new List<LeaderboardNeighbor>();
            foreach (LeaderboardNeighbor item in data.neighbors)
            {
                if (item == null || item.isSelf || item.rank <= 0) continue;
                if (item.rank < data.rank) above.Add(item);
                else if (item.rank > data.rank) below.Add(item);
            }
            above.Sort((a, b) => b.rank.CompareTo(a.rank));
            below.Sort((a, b) => a.rank.CompareTo(b.rank));
            int limit = compactRows ? 1 : 2;
            if (above.Count > limit) above.RemoveRange(limit, above.Count - limit);
            if (below.Count > limit) below.RemoveRange(limit, below.Count - limit);
            above.Reverse();
            var visible = new List<LeaderboardNeighbor>(above);
            int ownIndex = visible.Count;
            visible.Add(new LeaderboardNeighbor { rank = data.rank, score = data.score, isSelf = true });
            visible.AddRange(below);
            for (int i = 0; i < rows.Count; i++)
            {
                RankRow row = rows[i];
                row.rect.gameObject.SetActive(i < visible.Count);
                if (i >= visible.Count) continue;
                LeaderboardNeighbor entry = visible[i];
                row.label.text = NeighborLine(entry);
                row.label.color = entry.isSelf ? Color.white : new Color32(35, 39, 43, 255);
                row.label.fontStyle = entry.isSelf ? FontStyle.Bold : FontStyle.Normal;
                row.background.color = entry.isSelf ? new Color32(50, 101, 182, 255) : new Color32(214, 214, 214, 255);
                row.targetY = -i * rowHeight;
                int initialIndex = entry.isSelf ? Mathf.Max(ownIndex + 1, visible.Count - 1) : i > ownIndex ? i - 1 : i;
                row.startY = animate ? -initialIndex * rowHeight : row.targetY;
                Place(row.rect, 0, -row.startY, rowsRoot.sizeDelta.x, rowHeight - 3);
            }
        }

        private string NeighborLine(LeaderboardNeighbor neighbor)
        {
            string name = neighbor.isSelf ? (ru ? "Вы" : "You") :
                string.IsNullOrWhiteSpace(neighbor.name) ? (ru ? "Игрок" : "Player") : neighbor.name.Replace("\n", " ").Replace("\r", " ");
            if (name.Length > 12) name = name.Substring(0, 12) + "…";
            return "#" + neighbor.rank + "  " + name + "  ·  " + neighbor.score + (ru ? " опыта" : " XP");
        }

        private IEnumerator AnimateImprovement(LeaderboardRoundSnapshot data)
        {
            int gained = data.previousRank - data.rank;
            // Large confirmed jumps use the same bounded real rows, but cross them faster.
            float duration = gained >= 20 ? .32f : gained >= 5 ? .5f : .8f;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                foreach (RankRow row in rows)
                    if (row.rect.gameObject.activeSelf)
                        row.rect.anchoredPosition = new Vector2(0, Mathf.Lerp(row.startY, row.targetY, eased));
                yield return null;
            }
            foreach (RankRow row in rows)
                if (row.rect.gameObject.activeSelf) row.rect.anchoredPosition = new Vector2(0, row.targetY);
            celebration = null;
        }

        private Text Label(string name, Transform parent, int size, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = style;
            text.color = new Color32(35, 39, 43, 255);
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private Button Button(string name, Transform parent, Color color, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = roundedSprite; image.type = Image.Type.Sliced; image.color = color;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image; button.onClick.AddListener(action);
            Text label = Label("Label", rect, 16, FontStyle.Bold);
            label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
            return button;
        }
        private static void SetButtonLabel(Button button, string value) { button.GetComponentInChildren<Text>(true).text = value; }
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; ShareRequested = null; }
    }
}
