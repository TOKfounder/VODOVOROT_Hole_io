using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vodovorot.Stage3;
using YG;

namespace Vodovorot.Stage2
{
    /// <summary>One author-style ranking prototype over the settled round result.</summary>
    public sealed class LeaderboardRoundPanel : MonoBehaviour
    {
        public static event Action ShareRequested;
        private static LeaderboardRoundPanel instance;
        private string roundId;
        private RankingVisualStyle style;
        private RectTransform panel, header, safeRoot, viewport, body, summaryRoot, hero, neighborsRoot;
        private RectTransform rowsRoot, simulationRoot;
        private ScrollRect scroll;
        private Rect lastSafeArea;
        private Text title, statusText, rankText, goalText, bonusText;
        private Text rankCaption, oldRankText, gainText, gainCaption, neighborsTitle, simulationBadge, scrollHint;
        private Image heroFill, headerMedal;
        private Button loginButton, shareButton, closeButton, topCloseButton;
        private Canvas canvas;
        private Coroutine celebration;
        private int lastWidth, lastHeight, renderRevision;
        private float lastRatio, rowHeight;
        private bool compactRows, wideLayout;
        private LeaderboardRoundSnapshot rendered;
        private readonly List<RankRow> rows = new List<RankRow>();
        private readonly List<Image> sparkles = new List<Image>();
        private bool ru => YG2.saves == null || YG2.saves.langRu;

        private sealed class RankRow
        {
            public RectTransform rect;
            public Image background;
            public Text label, score;
            public float startY, targetY;
            public LeaderboardNeighbor entry;
            public bool isSelf;
        }

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

        public static void Show(string id, bool eligibleRound)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (instance == null)
            {
                var go = new GameObject("Round leaderboard · Stage 3 prototype", typeof(RectTransform));
                instance = go.AddComponent<LeaderboardRoundPanel>();
                instance.Build();
            }
            instance.roundId = id;
            instance.gameObject.SetActive(true);
            instance.Layout();
            instance.body.anchoredPosition = Vector2.zero;
            int before = instance.renderRevision;
            LeaderboardRoundService.BeginRound(id, eligibleRound);
            if (before == instance.renderRevision) instance.Render(LeaderboardRoundService.Current);
        }

        public static void Close()
        {
            if (instance != null) instance.gameObject.SetActive(false);
        }

        private void OnEnable() { LeaderboardRoundService.Changed += Render; }
        private void OnDisable()
        {
            LeaderboardRoundService.Changed -= Render;
            StopCelebration();
        }
        private void OnDestroy() { if (instance == this) instance = null; }

        private void Build()
        {
            style = RankingVisualStyle.Load();
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = CssRatio();
            if (EventSystem.current == null)
                new GameObject("Leaderboard UI events", typeof(EventSystem), typeof(StandaloneInputModule));
            gameObject.AddComponent<Image>().color = style.backdrop;
            safeRoot = RankingUiStyle.Rect("Safe area in CSS pixels", transform);
            panel = RankingUiStyle.Surface("Ranking prototype", safeRoot, style, style.paper, out _, true);
            header = RankingUiStyle.Surface("Blue title banner", panel, style, style.blue, out _);
            headerMedal = RankingUiStyle.Icon("Canonical gold medal", header, style.goldMedal);
            title = Label("Title", header, 28, Color.white, true, FontStyle.Bold);
            topCloseButton = RankingUiStyle.Button("Close immediately", header, style, style.amber, Close);
            SetButtonLabel(topCloseButton, "X");
            Text closeGlyph = topCloseButton.GetComponentInChildren<Text>();
            closeGlyph.fontSize = 22;
            RankingUiStyle.Stretch(closeGlyph.rectTransform, 3);

            simulationRoot = RankingUiStyle.Surface("Explicit simulation marker", panel, style, style.amber, out _);
            simulationBadge = Label("Simulation badge", simulationRoot, 13, style.ink, false, FontStyle.Bold);
            simulationBadge.alignment = TextAnchor.MiddleCenter;
            simulationBadge.horizontalOverflow = HorizontalWrapMode.Overflow;
            RankingUiStyle.Stretch(simulationBadge.rectTransform, 4);
            simulationRoot.gameObject.SetActive(false);

            viewport = RankingUiStyle.Rect("Scrollable ranking body", panel);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            body = RankingUiStyle.Rect("Content", viewport);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = body;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28; scroll.inertia = true;
            summaryRoot = RankingUiStyle.Rect("Your result", body);
            statusText = Label("Status", summaryRoot, 18, style.ink);
            hero = RankingUiStyle.Surface("Your rank highlight", summaryRoot, style, style.blue, out heroFill);
            rankCaption = Label("Your rank caption", hero, 14, Color.white, true, FontStyle.Bold);
            rankText = Label("Own rank", hero, 52, Color.white, true, FontStyle.Bold);
            rankText.alignment = TextAnchor.MiddleCenter;
            rankText.resizeTextForBestFit = true; rankText.resizeTextMinSize = 27; rankText.resizeTextMaxSize = 52;
            oldRankText = Label("Previous to new rank", hero, 17, Color.white, true, FontStyle.Bold);
            oldRankText.alignment = TextAnchor.MiddleCenter;
            oldRankText.resizeTextForBestFit = true; oldRankText.resizeTextMinSize = 12; oldRankText.resizeTextMaxSize = 17;
            gainText = Label("Places gained counter", hero, 30, style.amber, true, FontStyle.Bold);
            gainText.alignment = TextAnchor.MiddleCenter;
            gainText.resizeTextForBestFit = true; gainText.resizeTextMinSize = 18; gainText.resizeTextMaxSize = 30;
            gainCaption = Label("Places caption", hero, 14, Color.white, true, FontStyle.Bold);
            gainCaption.alignment = TextAnchor.MiddleCenter;
            goalText = Label("Next goal", summaryRoot, 18, style.ink, false, FontStyle.Bold);
            bonusText = Label("Actual bonus", summaryRoot, 16, style.ink);

            neighborsRoot = RankingUiStyle.Rect("Nearby players", body);
            neighborsTitle = Label("Neighbors heading", neighborsRoot, 16, style.ink, false, FontStyle.Bold);
            rowsRoot = RankingUiStyle.Rect("Neighbor rows", neighborsRoot);
            for (int i = 0; i < 5; i++)
            {
                RectTransform rect = RankingUiStyle.Surface("Rank row " + i, rowsRoot, style, style.lightBlue, out Image fill);
                var row = new RankRow { rect = rect, background = fill };
                row.label = Label("Rank and player", rect, 17, style.ink, false, FontStyle.Bold);
                row.label.horizontalOverflow = HorizontalWrapMode.Overflow;
                row.score = Label("XP", rect, 16, style.ink, false, FontStyle.Bold);
                row.score.alignment = TextAnchor.MiddleRight;
                row.score.resizeTextForBestFit = true; row.score.resizeTextMinSize = 12; row.score.resizeTextMaxSize = 16;
                rows.Add(row); rect.gameObject.SetActive(false);
            }
            // All accents stay inside the rank card; no full-screen flash or particle system.
            for (int i = 0; i < 8; i++)
            {
                Image sparkle = RankingUiStyle.Icon("Rank sparkle " + i, hero, style.goldStar);
                sparkle.gameObject.SetActive(false); sparkles.Add(sparkle);
            }

            scrollHint = Label("Scroll hint", panel, 12, style.ink);
            scrollHint.alignment = TextAnchor.MiddleCenter;
            loginButton = RankingUiStyle.Button("Sign in", panel, style, style.blue, LeaderboardRoundService.RequestConnection);
            shareButton = RankingUiStyle.Button("Share", panel, style, style.amber, () => { Close(); ShareRequested?.Invoke(); });
            closeButton = RankingUiStyle.Button("Back to results", panel, style, style.green, Close);
            loginButton.gameObject.SetActive(false);
            Layout();
        }

        private Text Label(string name, Transform parent, int size, Color color, bool outlined = false,
            FontStyle fontStyle = FontStyle.Normal) => RankingUiStyle.Label(name, parent, style, size, color, outlined, fontStyle);

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
            safeRoot.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            safeRoot.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            float safeWidth = safe.width / lastRatio, safeHeight = safe.height / lastRatio;
            wideLayout = safeWidth >= 680 && safeWidth > safeHeight * 1.15f;
            float width = Mathf.Max(1, Mathf.Min(wideLayout ? 900 : 520, safeWidth - 24));
            float height = Mathf.Max(1, Mathf.Min(wideLayout ? 620 : 790, safeHeight - 24));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(width, height);
            bool shortLandscape = wideLayout && height < 430;
            const float pad = 16, gap = 12;
            float inside = Mathf.Max(1, width - pad * 2);
            float headerHeight = height < 430 ? 58 : 66;
            Place(header, pad - 4, 12, inside + 8, headerHeight);
            Place(headerMedal.rectTransform, 10, 8, 38, headerHeight - 16);
            Place(title.rectTransform, 56, 4, Mathf.Max(1, inside - 112), headerHeight - 8);
            title.fontSize = width < 370 ? 25 : 28;
            Place(topCloseButton.GetComponent<RectTransform>(), inside - 48, (headerHeight - 48) * .5f, 48, 48);
            float bodyTop = 12 + headerHeight + gap;
            if (simulationRoot.gameObject.activeSelf)
            {
                Place(simulationRoot, pad, bodyTop - 3, inside, 30);
                simulationBadge.text = FitName(simulationBadge, ru ? "СИМУЛЯЦИЯ · " : "SIMULATION · ",
                    LocalizedSimulationLabel(rendered), inside - 14);
                bodyTop += 36;
            }
            float buttonHeight = Mathf.Max(48, style.minimumButtonHeight);
            float footerHeight = buttonHeight + (loginButton.gameObject.activeSelf ? buttonHeight + 10 : 0);
            float footerTop = height - pad - footerHeight;
            float viewportHeight = Mathf.Max(1, footerTop - bodyTop - 24);
            Place(viewport, pad, bodyTop, inside, viewportHeight);
            float summaryWidth = wideLayout ? Mathf.Clamp(inside * .36f, 238, 300) : inside;
            float neighborWidth = wideLayout ? inside - summaryWidth - 20 : inside;
            bool confirmed = rendered != null && rendered.status == LeaderboardRoundStatus.Confirmed;
            float statusHeight = ParagraphHeight(statusText, summaryWidth, confirmed ? 32 : 56);
            Place(statusText.rectTransform, 2, 0, summaryWidth - 4, statusHeight);
            float y = statusHeight + 10;
            float heroHeight = shortLandscape ? 104 : wideLayout ? 116 : 124;
            Place(hero, 0, y, summaryWidth, heroHeight); y += heroHeight + 12;
            Place(rankCaption.rectTransform, 12, shortLandscape ? 4 : 8, summaryWidth - 24, shortLandscape ? 20 : 22);
            bool hasGain = confirmed && rendered.improved && rendered.previousRank > rendered.rank;
            float rankWidth = hasGain ? summaryWidth * .66f : summaryWidth - 20;
            rankText.resizeTextMaxSize = shortLandscape ? 44 : 52;
            gainText.resizeTextMaxSize = shortLandscape ? 27 : 30;
            Place(rankText.rectTransform, 10, shortLandscape ? 24 : 28, rankWidth - (hasGain ? 10 : 0), shortLandscape ? 47 : 58);
            Place(gainText.rectTransform, summaryWidth * .66f, shortLandscape ? 24 : 30, summaryWidth * .34f - 10, shortLandscape ? 32 : 38);
            Place(gainCaption.rectTransform, summaryWidth * .66f, shortLandscape ? 52 : 65, summaryWidth * .34f - 10, shortLandscape ? 20 : 21);
            gainText.gameObject.SetActive(hasGain); gainCaption.gameObject.SetActive(hasGain);
            Place(oldRankText.rectTransform, 10, heroHeight - 31, summaryWidth - 20, 24);
            float heroBottom = y;
            float goalHeight = string.IsNullOrEmpty(goalText.text) ? 0 : ParagraphHeight(goalText, summaryWidth, 42);
            float bonusHeight = string.IsNullOrEmpty(bonusText.text) ? 0 : ParagraphHeight(bonusText, summaryWidth, 24);
            int aboveCount = 0, belowCount = 0;
            if (confirmed && rendered.neighbors != null) foreach (LeaderboardNeighbor entry in rendered.neighbors)
            {
                if (entry == null || entry.isSelf || entry.rank <= 0) continue;
                if (entry.rank < rendered.rank) aboveCount++;
                else if (entry.rank > rendered.rank) belowCount++;
            }
            int fullRowCount = 1 + Mathf.Min(2, aboveCount) + Mathf.Min(2, belowCount);
            float fullRowHeight = shortLandscape ? 42 : 56;
            float fullNeighborsHeight = 30 + fullRowCount * fullRowHeight;
            float detailsHeight = goalHeight + (goalHeight > 0 ? 8 : 0) + bonusHeight;
            float fullContentHeight = wideLayout ? Mathf.Max(heroBottom + detailsHeight, fullNeighborsHeight)
                : heroBottom + 6 + fullNeighborsHeight + 12 + detailsHeight + 3;
            // Budget from the actual CSS viewport, localized copy and optional simulation
            // marker. Five real rows remain only when the complete result fits with them.
            compactRows = confirmed && fullContentHeight > viewportHeight;
            rowHeight = shortLandscape ? 42 : compactRows ? 54 : 56;
            neighborsRoot.gameObject.SetActive(confirmed);
            float neighborY = wideLayout ? 0 : heroBottom + 6;
            int activeRows = 0;
            foreach (RankRow row in rows) if (row.rect.gameObject.activeSelf) activeRows++;
            float neighborHeight = 30 + activeRows * rowHeight;
            Place(neighborsRoot, wideLayout ? summaryWidth + 20 : 0, neighborY, neighborWidth, neighborHeight);
            Place(neighborsTitle.rectTransform, 2, 0, neighborWidth - 4, 24);
            Place(rowsRoot, 0, 30, neighborWidth, activeRows * rowHeight);
            foreach (RankRow row in rows)
            {
                row.rect.sizeDelta = new Vector2(neighborWidth, rowHeight - 6);
                float scoreWidth = neighborWidth < 340 ? 86 : 112;
                Place(row.label.rectTransform, 12, 3, Mathf.Max(1, neighborWidth - scoreWidth - 28), rowHeight - 12);
                Place(row.score.rectTransform, neighborWidth - scoreWidth - 12, 3, scoreWidth, rowHeight - 12);
                if (row.entry != null) SetRowLabel(row, row.entry.rank);
            }
            // Portrait gives the moving rows priority. Goals remain readable just below them,
            // instead of pushing the entire celebration outside a short phone viewport.
            y = confirmed && !wideLayout ? neighborY + neighborHeight + 12 : heroBottom;
            Place(goalText.rectTransform, 2, y, summaryWidth - 4, goalHeight); y += goalHeight;
            if (goalHeight > 0) y += 8;
            Place(bonusText.rectTransform, 2, y, summaryWidth - 4, bonusHeight); y += bonusHeight;
            Place(summaryRoot, 0, 0, summaryWidth, y);
            float contentHeight = confirmed && wideLayout ? Mathf.Max(y, neighborHeight) : y;
            body.anchorMin = new Vector2(0, 1); body.anchorMax = new Vector2(1, 1); body.pivot = new Vector2(.5f, 1);
            body.sizeDelta = new Vector2(0, Mathf.Max(viewportHeight, contentHeight + 3));
            scroll.vertical = contentHeight > viewportHeight;
            scrollHint.gameObject.SetActive(scroll.vertical);
            scrollHint.text = ru ? "Прокрутите вниз, чтобы увидеть всё" : "Scroll down to see more";
            Place(scrollHint.rectTransform, pad, footerTop - 24, inside, 24);

            float actionY = height - pad - buttonHeight;
            Place(shareButton.GetComponent<RectTransform>(), pad, actionY, (inside - 10) * .46f, buttonHeight);
            Place(closeButton.GetComponent<RectTransform>(), pad + (inside - 10) * .46f + 10, actionY, (inside - 10) * .54f, buttonHeight);
            Place(loginButton.GetComponent<RectTransform>(), pad, footerTop, inside, buttonHeight);
            title.text = ru ? "Рейтинг" : "Ranking";
            rankCaption.text = ru ? "ВАШЕ МЕСТО" : "YOUR RANK";
            neighborsTitle.text = ru ? "РЯДОМ С ВАМИ" : "NEAR YOU";
            SetButtonLabel(shareButton, ru ? "Поделиться" : "Share");
            SetButtonLabel(closeButton, ru ? "К результатам" : "Back to results");
            shareButton.GetComponentInChildren<Text>().fontSize = width < 370 ? 16 : 18;
            closeButton.GetComponentInChildren<Text>().fontSize = width < 370 ? 16 : 18;
        }

        private static float ParagraphHeight(Text text, float width, float minimum)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1, width - 4), 2000));
            float preferred = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / Mathf.Max(.01f, text.pixelsPerUnit);
            return Mathf.Max(minimum, Mathf.Ceil(preferred) + 6);
        }

        private void Render(LeaderboardRoundSnapshot data)
        {
            if (data == null || data.roundId != roundId || !gameObject.activeInHierarchy || rankText == null) return;
            // Begin may publish the same synchronous confirmation twice. Preserve the one
            // consumed celebration instead of immediately stopping it on the second publish.
            if (celebration != null && ReferenceEquals(rendered, data) && data.status == LeaderboardRoundStatus.Confirmed) return;
            renderRevision++;
            StopCelebration();
            rendered = data;
            foreach (RankRow row in rows) row.rect.gameObject.SetActive(false);
            RefreshConnectionButton(data);
            simulationRoot.gameObject.SetActive(data.isSimulation);
            simulationBadge.text = data.isSimulation
                ? (ru ? "СИМУЛЯЦИЯ · " : "SIMULATION · ") + LocalizedSimulationLabel(data) : "";
            goalText.text = bonusText.text = gainText.text = gainCaption.text = "";
            rankText.text = "—";
            oldRankText.text = ru ? "Можно продолжать играть" : "You can keep playing";
            bool animate = false;
            switch (data.status)
            {
                case LeaderboardRoundStatus.WaitingForProfile:
                    statusText.text = ru ? "Загружаем ваш профиль…" : "Loading your profile…";
                    break;
                case LeaderboardRoundStatus.Loading:
                    statusText.text = ru ? "Проверяем результат раунда…" : "Checking your round result…";
                    break;
                case LeaderboardRoundStatus.Authenticating:
                    statusText.text = ru ? "Подключаем профиль…" : "Connecting your profile…";
                    break;
                case LeaderboardRoundStatus.Guest:
                    statusText.text = ru ? "Войдите, чтобы увидеть своё место и сохранять прогресс в облаке." : "Sign in to see your rank and save progress to the cloud.";
                    rankText.text = "?";
                    oldRankText.text = ru ? "Ваше место появится после входа" : "Your rank appears after sign-in";
                    goalText.text = ru ? "Можно играть и без входа." : "You can also play as a guest.";
                    break;
                case LeaderboardRoundStatus.Pending:
                    statusText.text = ru ? "Результат ещё обрабатывается. Место обновится позже." : "Your result is still processing. Your rank will update later.";
                    ShowLastKnown();
                    break;
                case LeaderboardRoundStatus.Unavailable:
                    statusText.text = LeaderboardRoundService.CanRetryProfileLoad
                        ? (ru ? "Профиль пока недоступен. Попробуйте подключиться ещё раз." : "Your profile is unavailable. Try connecting again.")
                        : (ru ? "Рейтинг временно недоступен. Можно продолжать играть." : "The leaderboard is unavailable. You can keep playing.");
                    ShowLastKnown();
                    break;
                case LeaderboardRoundStatus.Confirmed:
                    statusText.text = data.improved ? (ru ? "Вы поднялись в рейтинге!" : "You moved up the ranking!") : (ru ? "Ваше место подтверждено" : "Your rank is confirmed");
                    rankText.text = "#" + data.rank;
                    int gained = data.improved ? Mathf.Max(0, data.previousRank - data.rank) : 0;
                    oldRankText.text = gained > 0
                        ? (ru ? "Было #" : "From #") + data.previousRank + " → #" + data.rank
                        : (ru ? "Результат: " : "Result: ") + data.score + (ru ? " опыта" : " XP");
                    gainText.text = "+" + gained;
                    gainCaption.text = PlacesCaption(gained);
                    if (data.rank == 1) goalText.text = ru ? "Первое место! Так держать!" : "First place! Keep it up!";
                    else if (data.targetRank > 0)
                        goalText.text = ru ? "До места #" + data.targetRank + ": ещё " + data.targetXp + " опыта" :
                            "To reach #" + data.targetRank + ": " + data.targetXp + " more XP";
                    else goalText.text = ru ? "Ещё один раунд — ещё один шаг вверх!" : "Another round, another chance to climb!";
                    if (data.bonusCoins > 0)
                        bonusText.text = "+" + data.bonusCoins + (ru ? " монет за новый рекорд" : " coins for a new best");
                    // A view reopen, rotation or synthetic Render call cannot create a celebration.
                    animate = LeaderboardRoundService.TryConsumeCelebration(roundId);
                    break;
            }
            Layout();
            if (data.status == LeaderboardRoundStatus.Confirmed) RenderNeighbors(data, animate);
            Layout(); // Real row count determines the scroll extent; footer stays outside it.
            if (animate)
            {
                celebration = StartCoroutine(AnimateImprovement(data));
                Stage2Audio.PlayConfirmedRankImprovement(data.previousRank, data.rank, roundId + ":" + data.rank);
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
            if (data.neighbors != null) foreach (LeaderboardNeighbor item in data.neighbors)
            {
                if (item == null || item.isSelf || item.rank <= 0) continue;
                if (item.rank < data.rank) above.Add(item);
                else if (item.rank > data.rank) below.Add(item);
            }
            above.Sort((a, b) => b.rank.CompareTo(a.rank)); below.Sort((a, b) => a.rank.CompareTo(b.rank));
            int limit = compactRows ? 1 : 2;
            if (above.Count > limit) above.RemoveRange(limit, above.Count - limit);
            if (below.Count > limit) below.RemoveRange(limit, below.Count - limit);
            above.Reverse();
            var visible = new List<LeaderboardNeighbor>(above);
            int ownIndex = visible.Count;
            visible.Add(new LeaderboardNeighbor { rank = data.rank, score = data.score, isSelf = true });
            visible.AddRange(below);
            int crossed = Mathf.Min(data.previousRank - data.rank, visible.Count - ownIndex - 1);
            crossed = Mathf.Max(0, crossed);
            for (int i = 0; i < rows.Count; i++)
            {
                RankRow row = rows[i]; row.rect.gameObject.SetActive(i < visible.Count);
                if (i >= visible.Count) { row.entry = null; continue; }
                row.entry = visible[i]; row.isSelf = row.entry.isSelf;
                row.background.color = row.isSelf ? style.green : style.lightBlue;
                row.targetY = -i * rowHeight;
                int initialIndex = row.isSelf ? ownIndex + crossed : i > ownIndex && i <= ownIndex + crossed ? i - 1 : i;
                row.startY = animate ? -initialIndex * rowHeight : row.targetY;
                Place(row.rect, 0, -row.startY, rowsRoot.sizeDelta.x, rowHeight - 6);
                row.score.text = row.entry.score.ToString("N0", CultureInfo.InvariantCulture) + (ru ? " оп." : " XP");
                SetRowLabel(row, row.entry.rank);
            }
            foreach (RankRow row in rows) if (row.isSelf && row.rect.gameObject.activeSelf) row.rect.SetAsLastSibling();
        }

        private void SetRowLabel(RankRow row, int displayedRank)
        {
            string prefix = "#" + displayedRank + "  ";
            string name = row.isSelf ? (ru ? "Вы" : "You") : CleanName(row.entry.name);
            if (string.IsNullOrWhiteSpace(name)) name = ru ? "Игрок" : "Player";
            row.label.text = FitName(row.label, prefix, name, row.label.rectTransform.rect.width);
        }

        private static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var clean = new System.Text.StringBuilder(Mathf.Min(256, value.Length));
            foreach (char c in value)
            {
                if (clean.Length >= 256) break;
                clean.Append(char.IsControl(c) ? ' ' : c);
            }
            return clean.ToString().Trim();
        }

        private string LocalizedSimulationLabel(LeaderboardRoundSnapshot data)
        {
            string label = CleanName(data == null ? "" : data.simulationLabel);
            const string redundantTestPrefix = "ТЕСТ · ";
            if (label.StartsWith(redundantTestPrefix, StringComparison.Ordinal)) label = label.Substring(redundantTestPrefix.Length);
            if (ru) return label;
            // Translate transport-provided labels; the badge never invents a fixture
            // state and has no runtime dependency on the Editor simulation controls.
            return label.Replace("Первое появление", "First rank")
                .Replace("+1 место", "+1 place").Replace("+5 мест", "+5 places").Replace("+20 мест", "+20 places")
                .Replace("Вход в топ-3", "Top 3").Replace("Без улучшения", "No change")
                .Replace("Возврат к прежнему рекорду", "Previous best").Replace("Повтор результата", "Repeat")
                .Replace("Гость", "Guest").Replace("Задержка 2,5 с", "2.5s delay").Replace("Ошибка ответа", "Reply error")
                .Replace("тестовый XP", "fixture XP").Replace("XP раунда", "round XP");
        }

        private static string FitName(Text text, string prefix, string name, float width)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(10000, 100));
            float unit = Mathf.Max(.01f, text.pixelsPerUnit);
            if (text.cachedTextGeneratorForLayout.GetPreferredWidth(prefix + name, settings) / unit <= width) return prefix + name;
            int[] starts = StringInfo.ParseCombiningCharacters(name);
            int low = 0, high = starts.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                int end = middle == starts.Length ? name.Length : starts[middle];
                string candidate = prefix + name.Substring(0, end) + "…";
                if (text.cachedTextGeneratorForLayout.GetPreferredWidth(candidate, settings) / unit <= width) low = middle;
                else high = middle - 1;
            }
            return prefix + name.Substring(0, low == starts.Length ? name.Length : starts[low]) + "…";
        }

        private IEnumerator AnimateImprovement(LeaderboardRoundSnapshot data)
        {
            int gained = Mathf.Max(0, data.previousRank - data.rank);
            // A bigger jump is more expressive and has MORE reading time, never a faster crossing.
            float duration = gained >= 20 ? 1.9f : gained >= 5 ? 1.65f : 1.45f;
            int accents = gained >= 20 ? 8 : gained >= 5 ? 5 : 3;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Brief anticipation, a readable pass, then a settled final hold.
                float move = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .84f, t));
                float pulse = Mathf.Sin(Mathf.PI * move);
                foreach (RankRow row in rows)
                {
                    if (!row.rect.gameObject.activeSelf) continue;
                    row.rect.anchoredPosition = new Vector2(0, Mathf.Lerp(row.startY, row.targetY, move));
                    if (row.isSelf)
                    {
                        row.rect.localScale = Vector3.one * (1 + pulse * .018f);
                        row.background.color = Color.Lerp(style.green, Color.white, pulse * .14f);
                        SetRowLabel(row, move < .72f ? data.previousRank : data.rank);
                    }
                }
                int count = Mathf.Min(gained, Mathf.FloorToInt(gained * move + .001f));
                gainText.text = "+" + count; gainCaption.text = PlacesCaption(count);
                heroFill.color = Color.Lerp(style.blue, style.lightBlue, pulse * .16f);
                for (int i = 0; i < sparkles.Count; i++)
                {
                    Image sparkle = sparkles[i];
                    float local = Mathf.Clamp01((t - .18f - i * .035f) / .5f);
                    bool visible = i < accents && local > 0 && local < 1;
                    sparkle.gameObject.SetActive(visible);
                    if (!visible) continue;
                    float x = 14 + (hero.sizeDelta.x - 44) * ((i * .381966f + .08f) % 1);
                    float y = 8 + (hero.sizeDelta.y - 36) * ((i * .618034f + .19f) % 1);
                    float size = 12 + (i % 3) * 3;
                    Place(sparkle.rectTransform, x, y - Mathf.Sin(local * Mathf.PI) * 6, size, size);
                    sparkle.rectTransform.localRotation = Quaternion.Euler(0, 0, local * 36 - 18);
                    sparkle.color = new Color(1, 1, 1, Mathf.Sin(local * Mathf.PI) * .9f);
                }
                yield return null;
            }
            foreach (RankRow row in rows)
            {
                if (!row.rect.gameObject.activeSelf) continue;
                row.rect.anchoredPosition = new Vector2(0, row.targetY);
                row.rect.localScale = Vector3.one;
                row.background.color = row.isSelf ? style.green : style.lightBlue;
                if (row.entry != null) SetRowLabel(row, row.entry.rank);
            }
            gainText.text = "+" + gained; gainCaption.text = PlacesCaption(gained);
            ResetAccents(); celebration = null;
        }

        private string PlacesCaption(int count)
        {
            if (!ru) return count == 1 ? "place" : "places";
            int tens = count % 100, ones = count % 10;
            return tens >= 11 && tens <= 14 ? "мест" : ones == 1 ? "место" : ones >= 2 && ones <= 4 ? "места" : "мест";
        }

        private void StopCelebration()
        {
            if (celebration != null) StopCoroutine(celebration);
            celebration = null;
            foreach (RankRow row in rows) if (row.rect != null) row.rect.localScale = Vector3.one;
            ResetAccents();
        }

        private void ResetAccents()
        {
            foreach (Image sparkle in sparkles) if (sparkle != null) sparkle.gameObject.SetActive(false);
            if (heroFill != null && style != null) heroFill.color = style.blue;
        }

        private static void SetButtonLabel(Button button, string value) => button.GetComponentInChildren<Text>(true).text = value;
        private static void Place(RectTransform rect, float x, float y, float width, float height) => RankingUiStyle.Place(rect, x, y, width, height);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; ShareRequested = null; }
    }
}
