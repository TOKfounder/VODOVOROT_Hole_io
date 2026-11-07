using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using Vodovorot.Stage3;
using YG;

namespace Vodovorot.Stage2
{
    /// <summary>Uses the captured round skin identity and its complete catalog model. Sharing never fabricates a rank.</summary>
    public sealed class AchievementCard : MonoBehaviour
    {
        [Serializable]
        public sealed class CardData
        {
            public bool ru;
            public string percent;
            public string map;
            public string mode;
            public string skin;
            public string skinId;
            public string url;
            public int rank;
            public int xp;
            public string cardImageBase64, webShareButton, webSaveButton, webCopyButton, webCloseButton;
            public bool fullCardImage;
            public string webInk, webPaper, webBlue, webGreen, webYellow;
        }

        private static AchievementCard current;
        private CardData data;
        private Texture2D portrait;
        private RectTransform card;
        private Text status;
        private Text compactSummary;
        private RectTransform compactSummaryPanel;
        private Button saveButton, copyButton, closeButton;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void VodovorotAchievementOpen(string json, string portraitBase64);
        [DllImport("__Internal")] private static extern float LeaderboardStage2_CssRatio();
#endif

        public static CardData BuildData(GamingManager match)
        {
            bool ru = YG2.saves.langRu;
            int map = match != null ? match.Stage2Map : UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex - 1;
            string[] maps = ru ? new[] { "Город", "Сад", "Средневековье" } : new[] { "City", "Garden", "Medieval Castle" };
            string[] modes = ru ? new[] { "Тотальная зачистка", "Босс", "Охота", "Командный" }
                : new[] { "Total Cleaning", "Boss", "Hunting", "Team" };
            SkinVisualCatalog.Entry skin = ResolveSkin(match != null ? match.Stage2EquippedSkinId :
                SkinVisualCatalog.IdForLegacyIndex(Mathf.Clamp(YG2.saves.equipedMaterial, 0, 4)));
            var confirmed = LeaderboardRoundService.Current;
            int rank = confirmed != null && match != null && confirmed.roundId == match.Stage2RoundId &&
                confirmed.status == LeaderboardRoundStatus.Confirmed ? confirmed.rank : 0;
            string app = YG2.envir.appID;
            if (string.IsNullOrEmpty(app) || !long.TryParse(app, out long id) || id <= 0) app = "473763";
            return new CardData
            {
                ru = ru,
                percent = (Mathf.Floor(Mathf.Clamp01(match != null ? match.GetCapturePercent() : 0) * 1000f) / 10f)
                    .ToString("0.0", CultureInfo.InvariantCulture) + "%",
                map = maps[Mathf.Clamp(map, 0, maps.Length - 1)],
                mode = modes[Mathf.Clamp(match != null ? match.Stage2Mode : YG2.saves.chosenMode, 0, modes.Length - 1)],
                skinId = skin.id,
                skin = SkinVisualFactory.LocalizedName(skin, ru).Replace('\n', ' ').Replace('\r', ' '),
                rank = Mathf.Max(0, rank), xp = Mathf.Max(0, YG2.saves.exp),
                url = "https://yandex.ru/games/app/" + app
            };
        }

        public static void Show(GamingManager match)
        {
            if (current != null) Destroy(current.gameObject);
            var data = BuildData(match);
            Texture2D portrait = SkinPortraitRenderer.Render(ResolveSkin(data.skinId), data.ru, 1024);
#if UNITY_WEBGL && !UNITY_EDITOR
            Texture2D fullCard = null;
            try
            {
                data.fullCardImage = true;
                AchievementCardVisual.ApplyWebArtwork(data);
                fullCard = AchievementCardVisual.RenderCard(data, portrait);
                VodovorotAchievementOpen(JsonUtility.ToJson(data), fullCard != null ? Convert.ToBase64String(fullCard.EncodeToPNG()) : "");
            }
            finally
            {
                if (fullCard != null) Destroy(fullCard);
                if (portrait != null) Destroy(portrait);
            }
#else
            var overlay = Stage2Ui.Overlay("AchievementCard", 3100);
            current = overlay.gameObject.AddComponent<AchievementCard>();
            current.data = data; current.portrait = portrait;
            current.Build(overlay);
#endif
        }

        private void Build(RectTransform root)
        {
            Image backdrop = root.Find("Backdrop")?.GetComponent<Image>();
            if (backdrop != null) { Color dim = RankingVisualStyle.Load().backdrop; dim.a = .98f; backdrop.color = dim; }
            card = AchievementCardVisual.BuildCard(root, data, portrait);
            saveButton = Stage2Ui.Button(root, data.ru ? "Сохранить PNG" : "Save PNG", Vector2.zero, Vector2.zero,
                () => StartCoroutine(SaveImage()), RankingVisualStyle.Load().green);
            copyButton = Stage2Ui.Button(root, data.ru ? "Скопировать ссылку" : "Copy link", Vector2.zero, Vector2.zero,
                () => { GUIUtility.systemCopyBuffer = data.url; status.text = data.ru ? "Ссылка скопирована" : "Link copied"; });
            closeButton = Stage2Ui.Button(root, data.ru ? "Закрыть" : "Close", Vector2.zero, Vector2.zero, () => Destroy(gameObject), RankingVisualStyle.Load().amber);
            status = Stage2Ui.Text(root, "", 18, Vector2.zero, Vector2.zero, Color.white);
            compactSummaryPanel = RankingUiStyle.Surface("Compact result", root, RankingVisualStyle.Load(), RankingVisualStyle.Load().paper, out _);
            compactSummary = Stage2Ui.Text(compactSummaryPanel, "", 18, Vector2.zero, Vector2.zero, RankingVisualStyle.Load().ink);
            compactSummary.alignment = TextAnchor.UpperLeft;
            compactSummary.resizeTextForBestFit = false;
            string rankLine = data.rank > 0 ? (data.ru ? "Место в рейтинге: #" : "Global rank: #") + data.rank
                : (data.ru ? "Расту с каждой игрой" : "Growing with every game");
            compactSummary.text = data.skin + "\n\n" + data.percent + "\n" + data.map + " · " + data.mode + "\n" + rankLine + "\n" + data.url;
            LayoutNativeCard();
        }

        private void LateUpdate()
        {
            if (card == null) return;
            LayoutNativeCard();
            if (Input.GetKeyDown(KeyCode.Escape)) Destroy(gameObject);
        }

        private IEnumerator SaveImage()
        {
            Texture2D image = null;
            string path = Path.Combine(Application.persistentDataPath, "vodovorot-achievement.png");
            try
            {
                image = AchievementCardVisual.RenderCard(data, portrait);
                if (image == null) throw new InvalidOperationException("Full achievement image could not be rendered.");
                File.WriteAllBytes(path, image.EncodeToPNG());
                status.text = data.ru ? "Карточка сохранена. Ссылка доступна рядом." : "Card saved. Copy the link alongside it.";
                Debug.Log("[Vodovorot] Achievement card saved: " + path);
            }
            catch (Exception) { status.text = data.ru ? "Не удалось сохранить карточку" : "Could not save the card"; }
            finally { if (image != null) Destroy(image); }
            yield break;
        }

        /// <summary>Compatibility wrapper for existing callers; match sharing resolves its captured string ID instead.</summary>
        public static Texture2D RenderEquippedSkin(int equippedSkin = -1)
        {
            int legacy = equippedSkin >= 0 ? equippedSkin : YG2.saves.equipedMaterial;
            SkinVisualCatalog.Entry entry = SkinVisualCatalog.Load().GetByLegacyIndex(Mathf.Clamp(legacy, 0, 4));
            return SkinPortraitRenderer.Render(entry, YG2.saves == null || YG2.saves.langRu);
        }

        private static SkinVisualCatalog.Entry ResolveSkin(string skinId)
        {
            SkinVisualCatalog catalog = SkinVisualCatalog.Load();
            if (!catalog.TryGetById(skinId, out SkinVisualCatalog.Entry entry))
                throw new InvalidOperationException("The round has no captured catalog skin identity: " + skinId);
            return entry;
        }

        private void LayoutNativeCard()
        {
            if (card == null || saveButton == null) return;
            Canvas canvas = GetComponent<Canvas>();
            float canvasScale = canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
            float cssRatio = 1;
#if UNITY_WEBGL && !UNITY_EDITOR
            cssRatio = Mathf.Clamp(LeaderboardStage2_CssRatio(), .25f, 8);
#endif
            float unit = cssRatio / canvasScale;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            float x = safe.xMin / canvasScale, y = safe.yMin / canvasScale;
            float width = safe.width / canvasScale, height = safe.height / canvasScale;
            float padding = 12 * unit, gap = 12 * unit, buttonHeight = 52 * unit;
            float inside = Mathf.Max(1, width - padding * 2);
            float buttonWidth = (inside - gap) * .5f;
            PlaceBottom(saveButton.GetComponent<RectTransform>(), x + padding, y + padding, buttonWidth, buttonHeight);
            PlaceBottom(copyButton.GetComponent<RectTransform>(), x + padding + buttonWidth + gap, y + padding, buttonWidth, buttonHeight);
            PlaceBottom(closeButton.GetComponent<RectTransform>(), x + width - padding - 132 * unit, y + height - padding - buttonHeight, 132 * unit, buttonHeight);
            SizeButtonText(saveButton, unit); SizeButtonText(copyButton, unit); SizeButtonText(closeButton, unit);
            PlaceBottom(status.rectTransform, x + padding, y + padding + buttonHeight + 4 * unit, inside, 26 * unit);
            status.fontSize = Mathf.Max(12, Mathf.RoundToInt(13 * unit));
            status.resizeTextMaxSize = status.fontSize;
            float contentBottom = y + padding + buttonHeight + 34 * unit;
            float contentTop = y + height - padding - buttonHeight - 12 * unit;
            float contentHeight = Mathf.Max(1, contentTop - contentBottom);
            bool compact = width / unit >= 680 && height / unit < 560;
            compactSummaryPanel.gameObject.SetActive(compact);
            float cardScale;
            float centerX;
            if (compact)
            {
                cardScale = Mathf.Min(inside * .38f / 1080, contentHeight / 1350);
                float previewWidth = 1080 * cardScale;
                centerX = x + padding + previewWidth * .5f;
                PlaceBottom(compactSummaryPanel, x + padding + previewWidth + 24 * unit,
                    contentBottom, Mathf.Max(1, inside - previewWidth - 24 * unit), contentHeight);
                RankingUiStyle.Stretch(compactSummary.rectTransform, 12 * unit);
                compactSummary.fontSize = Mathf.Max(14, Mathf.RoundToInt(18 * unit));
            }
            else
            {
                cardScale = Mathf.Min(inside / 1080, contentHeight / 1350);
                centerX = x + width * .5f;
            }
            card.anchorMin = card.anchorMax = Vector2.zero;
            card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = new Vector2(centerX, contentBottom + contentHeight * .5f);
            card.localScale = Vector3.one * Mathf.Max(.001f, cardScale);
        }

        private static void SizeButtonText(Button button, float cssUnit)
        {
            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null) return;
            label.fontSize = Mathf.Max(14, Mathf.RoundToInt(18 * cssUnit));
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(14 * cssUnit));
        }

        private static void PlaceBottom(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void OnDestroy() { if (portrait != null) Destroy(portrait); if (current == this) current = null; }
    }
}
