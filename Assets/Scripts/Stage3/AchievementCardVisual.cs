using System;
using UnityEngine;
using UnityEngine.UI;
using Vodovorot.Stage2;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Vodovorot.Stage3
{
    /// <summary>One authored layout for the in-game card, exported PNG and browser preview.</summary>
    public static class AchievementCardVisual
    {
        public const int Width = 1080, Height = 1350;
        private const int CaptureLayer = 31;
        private static readonly string[] WebButtons = new string[8];

        public static RectTransform BuildCard(RectTransform parent, AchievementCard.CardData data, Texture2D portrait)
        {
            var style = RankingVisualStyle.Load();
            RectTransform card = RankingUiStyle.Surface("Achievement artwork", parent, style, style.paper, out Image paper);
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(Width, Height);
            RankingUiStyle.Stretch(paper.rectTransform, 10);
            RectTransform header = RankingUiStyle.Surface("Blue header", card, style, style.blue, out _);
            RankingUiStyle.Place(header, 24, 24, 1032, 166);
            Label(card, "Title", data.ru ? "ВОДОВОРОТ" : "VODOVOROT", 72, Color.white, 42, 30, 996, 100, true);
            Label(card, "Subtitle", data.ru ? "МОЁ ДОСТИЖЕНИЕ" : "MY ACHIEVEMENT", 34, Color.white, 42, 128, 996, 48, true);
            RectTransform portraitPanel = RankingUiStyle.Surface("Portrait field", card, style, style.lightBlue, out _);
            RankingUiStyle.Place(portraitPanel, 44, 214, 992, 610);
            var image = RankingUiStyle.Rect("Complete shop skin", portraitPanel).gameObject.AddComponent<RawImage>();
            image.texture = portrait; image.raycastTarget = false;
            RankingUiStyle.Place(image.rectTransform, 196, 5, 600, 600);
            Label(card, "Skin name", (data.skin ?? "").Replace('\n', ' ').Replace('\r', ' '), 43, style.ink, 58, 828, 964, 66);
            Label(card, "Result", data.percent, 104, style.blue, 48, 893, 984, 130, true);
            Label(card, "Map", data.map, 39, style.ink, 50, 1031, 980, 54);
            Label(card, "Mode", data.mode, 31, style.ink, 50, 1085, 980, 45);
            RectTransform rank = RankingUiStyle.Surface("Achievement badge", card, style,
                data.rank > 0 ? style.amber : style.lightBlue, out _);
            RankingUiStyle.Place(rank, 60, 1154, 960, 84);
            string rankText = data.rank > 0 ? (data.ru ? "Место в рейтинге: #" : "Global rank: #") + data.rank
                : (data.ru ? "Расту с каждой игрой!" : "Growing with every game!");
            Label(rank, "Rank", rankText, 37, style.ink, 18, 8, 924, 68);
            Label(card, "Public game link", data.url, 26, style.ink, 42, 1265, 996, 46);
            return card;
        }

        private static Text Label(Transform parent, string name, string value, int size, Color color,
            float x, float y, float width, float height, bool outlined = false)
        {
            Text text = RankingUiStyle.Label(name, parent, RankingVisualStyle.Load(), size, color, outlined, FontStyle.Bold);
            text.text = value ?? ""; text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = Mathf.RoundToInt(size * .75f);
            text.resizeTextMaxSize = size;
            RankingUiStyle.Place(text.rectTransform, x, y, width, height);
            Outline outline = text.GetComponent<Outline>();
            if (outline != null) outline.effectDistance = new Vector2(3, -3);
            return text;
        }

        public static Texture2D RenderCard(AchievementCard.CardData data, Texture2D portrait) =>
            RenderUi(Width, Height, root => BuildCard(root, data, portrait));

        /// <summary>Browser buttons use the actual game sprites and font, even on devices without that font.</summary>
        public static void ApplyWebArtwork(AchievementCard.CardData data)
        {
            RankingVisualStyle style = RankingVisualStyle.Load();
            data.webInk = Hex(style.ink); data.webPaper = Hex(style.paper);
            data.webBlue = Hex(style.blue); data.webGreen = Hex(style.green); data.webYellow = Hex(style.amber);
            string[] labels = data.ru ? new[] { "Отправить другу", "Сохранить PNG", "Скопировать ссылку", "Закрыть" }
                : new[] { "Share", "Save PNG", "Copy link", "Close" };
            Color[] colors = { style.amber, style.green, style.blue, style.amber };
            for (int i = 0; i < 4; i++)
            {
                int index = (data.ru ? 0 : 4) + i;
                if (WebButtons[index] == null)
                {
                    int buttonIndex = i;
                    int width = buttonIndex == 3 ? 346 : 600;
                    Texture2D button = RenderUi(width, 120, root =>
                    {
                        Button control = RankingUiStyle.Button("Browser button", root, style, colors[buttonIndex], () => { });
                        RankingUiStyle.Stretch(control.GetComponent<RectTransform>(), 4);
                        Text title = control.GetComponentInChildren<Text>();
                        title.text = labels[buttonIndex]; title.fontSize = 35;
                        title.alignment = TextAnchor.MiddleCenter;
                        title.GetComponent<Outline>().effectDistance = new Vector2(2, -2);
                    });
                    try { WebButtons[index] = Convert.ToBase64String(button.EncodeToPNG()); }
                    finally { Destroy(button); }
                }
            }
            int first = data.ru ? 0 : 4;
            data.webShareButton = WebButtons[first]; data.webSaveButton = WebButtons[first + 1];
            data.webCopyButton = WebButtons[first + 2]; data.webCloseButton = WebButtons[first + 3];
        }

        private static string Hex(Color value) => "#" + ColorUtility.ToHtmlStringRGB(value);

        private static Texture2D RenderUi(int width, int height, Action<RectTransform> build)
        {
            GameObject stage = null; Camera camera = null; RenderTexture target = null; Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
#if UNITY_EDITOR
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
#endif
            try
            {
                stage = new GameObject("Achievement capture"); stage.SetActive(false);
                stage.hideFlags = HideFlags.HideAndDontSave;
#if UNITY_EDITOR
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stage, scene);
#else
                stage.transform.position = Vector3.down * 1100;
#endif
                var root = new GameObject("Artwork canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
                root.SetParent(stage.transform, false); root.sizeDelta = new Vector2(width, height);
                root.localScale = Vector3.one * .001f;
                Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                camera = new GameObject("Artwork camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(stage.transform, false); camera.transform.localPosition = new Vector3(0, 0, -10);
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = height * .0005f;
                camera.aspect = (float)width / height; camera.cullingMask = 1 << CaptureLayer;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.nearClipPlane = .1f; camera.farClipPlane = 20; camera.allowHDR = false;
                canvas.worldCamera = camera;
#if UNITY_EDITOR
                camera.scene = scene;
#endif
                build(root);
                foreach (Transform item in stage.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = CaptureLayer;
                stage.SetActive(true); Canvas.ForceUpdateCanvases();
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Texture2D result = image; image = null; return result;
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (image != null) Destroy(image);
                if (stage != null) { stage.SetActive(false); Destroy(stage); }
#if UNITY_EDITOR
                EditorSceneManager.ClosePreviewScene(scene);
#endif
            }
        }

        private static void Destroy(UnityEngine.Object item)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
