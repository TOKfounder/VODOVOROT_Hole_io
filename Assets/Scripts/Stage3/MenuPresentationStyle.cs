using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;

namespace Vodovorot.Stage3
{
    /// <summary>
    /// Scene-local text polish using MainMenuController's authored references.
    /// The existing forms, mode blockers, progress content and purchase controls remain their owners.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(9000)]
    public sealed class MenuPresentationStyle : MonoBehaviour
    {
        private sealed class TitleBinding
        {
            public Text text;
            public int maximumSize;
            public string lastText;
            public Vector2 lastSize;
            public float lastPixelScale;
        }

        private sealed class LockedDescription
        {
            public Text description, status;
            public bool originallyEnabled;

            public void Refresh()
            {
                if (description == null || status == null) return;
                bool statusVisible = status.enabled && status.gameObject.activeInHierarchy;
                bool enabled = originallyEnabled && !statusVisible;
                if (description.enabled != enabled) description.enabled = enabled;
            }

            public void Restore()
            {
                if (description != null) description.enabled = originallyEnabled;
            }
        }

        private sealed class ProgressBinding
        {
            public ScrollRect scroll;
            public RectTransform viewport, track;
            public Text hint;
            public float lastPixelScale = -1f, lastTrackHeight = -1f;
            public bool lastRussian;
        }

        private MainMenuController owner;
        private RankingVisualStyle style;
        private readonly HashSet<Text> styledTexts = new HashSet<Text>();
        private readonly List<LockedDescription> lockedDescriptions = new List<LockedDescription>();
        private TitleBinding mobileTitle, desktopTitle;
        private ProgressBinding mobileProgress, desktopProgress;
        private bool initialized, lastRussian;
        private int lastWidth, lastHeight;

        public MainMenuController Owner => owner;
        public int StyledTextCount => styledTexts.Count;
        public Text MobileProgressHint => mobileProgress != null ? mobileProgress.hint : null;
        public Text DesktopProgressHint => desktopProgress != null ? desktopProgress.hint : null;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern float LeaderboardStage2_CssRatio();
#endif

        private static float CssPixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Mathf.Clamp(LeaderboardStage2_CssRatio(), .25f, 8f);
#else
            return 1f;
#endif
        }

        /// <summary>Play-mode only; never styles or saves the source scene in Edit Mode.</summary>
        public static MenuPresentationStyle Install(MainMenuController menu)
        {
            if (!Application.isPlaying || menu == null || menu.gameObject.scene.name != "MainMenu") return null;
            var presentation = menu.GetComponent<MenuPresentationStyle>();
            if (presentation == null) presentation = menu.gameObject.AddComponent<MenuPresentationStyle>();
            presentation.Initialize(menu);
            presentation.RefreshPresentation();
            return presentation;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstaller() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterFirstScene()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainMenu") return;
            MainMenuController found = null;
            // One known component type is the only scene lookup. All UI references below are serialized by it.
            foreach (MainMenuController candidate in FindObjectsOfType<MainMenuController>(true))
            {
                if (candidate.gameObject.scene != scene) continue;
                if (found != null)
                {
                    Debug.LogError("MainMenu text styling requires one MainMenuController.");
                    return;
                }
                found = candidate;
            }
            Install(found);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || gameObject.scene.name != "MainMenu") return;
            if (!initialized) Initialize(GetComponent<MainMenuController>());
            RefreshPresentation();
        }

        private void Initialize(MainMenuController menu)
        {
            if (initialized || menu == null) return;
            owner = menu;
            style = RankingVisualStyle.Load();
            mobileTitle = Title(At(menu.MainMenu, 0));
            desktopTitle = Title(At(menu.DMainMenu, 0));
            AddLockedDescriptions(menu.PanelOfModes);
            AddLockedDescriptions(menu.DPanelOfModes);
            mobileProgress = Progress(menu.PanelOfProgress);
            desktopProgress = Progress(menu.DPanelOfProgress);
            initialized = true;
        }

        private static TitleBinding Title(Text text) => text == null ? null : new TitleBinding
        {
            text = text,
            maximumSize = text.resizeTextForBestFit ? Mathf.Max(text.fontSize, text.resizeTextMaxSize) : text.fontSize
        };

        private void LateUpdate()
        {
            if (!initialized || owner == null) return;
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastRussian != Russian)
                RefreshPresentation();
            FitMainTitle(mobileTitle);
            FitMainTitle(desktopTitle);
            foreach (LockedDescription binding in lockedDescriptions) binding.Refresh();
            RefreshProgress(mobileProgress);
            RefreshProgress(desktopProgress);
        }

        /// <summary>Reapply after a localization/viewport change; does not update profile, prices or availability.</summary>
        public void RefreshPresentation()
        {
            if (!Application.isPlaying || !initialized || owner == null || gameObject.scene.name != "MainMenu") return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastRussian = Russian;
            Style(At(owner.MainMenu, 0)); Style(At(owner.DMainMenu, 0));
            Style(At(owner.PanelOfSkins, 0)); Style(At(owner.DPanelOfSkins, 0));
            Style(owner.PanelOfLeaders); Style(owner.DPanelOfLeaders);
            Style(At(owner.MobilePanelOfSettings, 0)); Style(At(owner.DesktopPanelOfSettings, 0));
            Style(At(owner.PanelOfMaps, 0)); Style(At(owner.DPanelOfMaps, 0));
            Style(At(owner.PanelOfModes, 0)); Style(At(owner.DPanelOfModes, 0));
            Style(At(owner.PanelOfProgress, 0)); Style(At(owner.DPanelOfProgress, 0));
            StyleCurrency(owner.PanelOfValute); StyleCurrency(owner.DPanelOfValute);
            foreach (LockedDescription binding in lockedDescriptions)
            {
                Style(binding.status);
                binding.Refresh();
            }
            FitMainTitle(mobileTitle, true);
            FitMainTitle(desktopTitle, true);
            RefreshProgress(mobileProgress);
            RefreshProgress(desktopProgress);
        }

        private static bool Russian => YG2.saves != null && YG2.saves.langRu;
        private static Text At(Text[] texts, int index) => texts != null && index < texts.Length ? texts[index] : null;

        private void StyleCurrency(Text[] texts)
        {
            Style(At(texts, 0));
            for (int index = 2; index <= 6; index++) Style(At(texts, index));
        }

        private void Style(Text text)
        {
            if (text == null) return;
            styledTexts.Add(text);
            text.font = style.comicFont;
            text.color = style.paper;
            Outline outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = style.outline;
            float distance = Mathf.Max(style.textOutline, Mathf.Abs(outline.effectDistance.x), Mathf.Abs(outline.effectDistance.y));
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        private void AddLockedDescriptions(Text[] texts)
        {
            for (int index = 0; index < 3; index++)
            {
                Text description = At(texts, 4 + index * 2), status = At(texts, 9 + index);
                if (description == null || status == null) continue;
                lockedDescriptions.Add(new LockedDescription { description = description, status = status, originallyEnabled = description.enabled });
                // Only the status Text moves within its existing Blockage card. The blocker and lock remain authored.
                RectTransform rect = status.rectTransform;
                rect.anchorMin = new Vector2(.06f, .08f); rect.anchorMax = new Vector2(.94f, .35f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                status.alignment = TextAnchor.MiddleCenter;
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                status.verticalOverflow = VerticalWrapMode.Truncate;
                status.resizeTextForBestFit = true;
                status.resizeTextMinSize = 10; status.resizeTextMaxSize = 32;
            }
        }

        private void FitMainTitle(TitleBinding binding, bool force = false)
        {
            if (binding == null || binding.text == null) return;
            Text text = binding.text;
            RectTransform rect = text.rectTransform;
            float pixelScale = PixelsPerLocalUnit(rect, false);
            float cssRatio = CssPixelRatio();
            if (!ConstrainTitleToViewport(rect, 16f * cssRatio)) return;
            Vector2 size = rect.rect.size;
            if (!force && binding.lastText == text.text && (binding.lastSize - size).sqrMagnitude < .0001f && Mathf.Approximately(binding.lastPixelScale, pixelScale)) return;
            binding.lastText = text.text; binding.lastSize = size; binding.lastPixelScale = pixelScale;
            if (size.x <= 1f || size.y <= 1f) return;
            TextGenerationSettings settings = text.GetGenerationSettings(size);
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            int low = 1, high = Mathf.Max(1, binding.maximumSize), fitted = 1;
            float pixelsPerUnit = Mathf.Max(.001f, text.pixelsPerUnit);
            while (low <= high)
            {
                int candidate = low + (high - low) / 2;
                settings.fontSize = candidate;
                float width = text.cachedTextGeneratorForLayout.GetPreferredWidth(text.text, settings) / pixelsPerUnit;
                float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / pixelsPerUnit;
                if (width <= size.x - 4f * cssRatio / pixelScale && height <= size.y)
                { fitted = candidate; low = candidate + 1; }
                else high = candidate - 1;
            }
            text.resizeTextForBestFit = false;
            text.fontSize = fitted;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static bool ConstrainTitleToViewport(RectTransform rect, float insetPixels)
        {
            RectTransform parent = rect.parent as RectTransform;
            Canvas canvas = rect.GetComponentInParent<Canvas>(true);
            if (parent == null || canvas == null) return false;
            Canvas root = canvas.rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            if (root.renderMode != RenderMode.ScreenSpaceOverlay && camera == null) return false;
            // A WorldSpace Canvas can extend outside the visible Game View. Its
            // authored 720-unit width is not the screen width. Use this Canvas's
            // actual render viewport, including during runtime LateUpdate; Editor
            // Screen.width can describe another focused window during QA.
            Rect viewport = camera != null ? camera.pixelRect : root.pixelRect;
            if (viewport.width <= insetPixels * 2f || viewport.height <= 0f) return false;
            Vector3 center = rect.TransformPoint(rect.rect.center);
            Vector3 parentCenter = parent.InverseTransformPoint(center);
            Vector2 parentLeft = RectTransformUtility.WorldToScreenPoint(camera,
                parent.TransformPoint(new Vector3(parent.rect.xMin, parentCenter.y, parentCenter.z)));
            Vector2 parentRight = RectTransformUtility.WorldToScreenPoint(camera,
                parent.TransformPoint(new Vector3(parent.rect.xMax, parentCenter.y, parentCenter.z)));
            float leftPixel = Mathf.Max(viewport.xMin, Mathf.Min(parentLeft.x, parentRight.x)) + insetPixels;
            float rightPixel = Mathf.Min(viewport.xMax, Mathf.Max(parentLeft.x, parentRight.x)) - insetPixels;
            if (rightPixel <= leftPixel) return false;
            float centerY = RectTransformUtility.WorldToScreenPoint(camera, center).y;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(leftPixel, centerY), camera, out Vector2 left) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(rightPixel, centerY), camera, out Vector2 right)) return false;
            float width = right.x - left.x;
            if (width <= 1f) return false;
            // Preserve the authored vertical anchors/position, scale and font. The
            // two real title RectTransforms have horizontal axes aligned to their
            // Canvas, so only their horizontal pivot position and width change.
            float pivotX = (left.x + right.x) * .5f - (.5f - rect.pivot.x) * width;
            float parentPivotX = parent.InverseTransformPoint(rect.TransformPoint(new Vector3(pivotX, 0, 0))).x;
            Vector2 minimum = rect.anchorMin, maximum = rect.anchorMax;
            minimum.x = maximum.x = .5f;
            if (rect.anchorMin != minimum) rect.anchorMin = minimum;
            if (rect.anchorMax != maximum) rect.anchorMax = maximum;
            if (Mathf.Abs(rect.rect.width - width) > .001f)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            Vector3 position = rect.localPosition;
            if (Mathf.Abs(position.x - parentPivotX) > .001f)
            {
                position.x = parentPivotX;
                rect.localPosition = position;
            }
            return true;
        }

        private ProgressBinding Progress(Text[] labels)
        {
            Text firstMilestone = At(labels, 1);
            ScrollRect scroll = firstMilestone != null ? firstMilestone.GetComponentInParent<ScrollRect>(true) : null;
            if (scroll == null || scroll.content == null || !scroll.horizontal || scroll.vertical || scroll.horizontalScrollbar == null) return null;
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
            scroll.viewport = viewport;
            RectMask2D mask = viewport.GetComponent<RectMask2D>();
            if (mask == null) mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.enabled = true;
            // The entire authored 540.5-unit viewport already accepts drag; the thin scrollbar is not the touch target.
            Image dragSurface = viewport.GetComponent<Image>();
            if (dragSurface != null) dragSurface.raycastTarget = true;
            RectTransform track = scroll.horizontalScrollbar.GetComponent<RectTransform>();
            var hintObject = new GameObject("Stage3ProgressScrollHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            hintObject.layer = track.gameObject.layer;
            Text hint = hintObject.GetComponent<Text>();
            hint.rectTransform.SetParent(track, false);
            hint.rectTransform.anchorMin = new Vector2(0f, .5f); hint.rectTransform.anchorMax = new Vector2(1f, .5f);
            hint.rectTransform.pivot = new Vector2(.5f, 1f);
            hint.font = style.comicFont; hint.fontStyle = FontStyle.Bold;
            hint.alignment = TextAnchor.MiddleCenter; hint.supportRichText = false;
            hint.horizontalOverflow = HorizontalWrapMode.Wrap; hint.verticalOverflow = VerticalWrapMode.Truncate;
            hint.raycastTarget = false;
            Style(hint);
            return new ProgressBinding { scroll = scroll, viewport = viewport, track = track, hint = hint };
        }

        private void RefreshProgress(ProgressBinding binding)
        {
            if (binding == null || binding.hint == null || binding.scroll == null) return;
            float pixelScale = PixelsPerLocalUnit(binding.track, true);
            bool russian = Russian;
            if (!Mathf.Approximately(binding.lastPixelScale, pixelScale) || binding.lastTrackHeight != binding.track.rect.height || binding.lastRussian != russian)
            {
                binding.lastPixelScale = pixelScale; binding.lastTrackHeight = binding.track.rect.height; binding.lastRussian = russian;
                binding.hint.text = russian ? "<  Листайте влево / вправо  >" : "<  Swipe or drag left / right  >";
                binding.hint.fontSize = Mathf.Max(1, Mathf.RoundToInt(18f / pixelScale));
                binding.hint.rectTransform.anchoredPosition = new Vector2(0f, -binding.track.rect.height * .5f - 8f / pixelScale);
                binding.hint.rectTransform.sizeDelta = new Vector2(0f, 34f / pixelScale);
            }
            bool overflow = binding.scroll.content != null && binding.scroll.content.rect.width > binding.viewport.rect.width + 1f;
            bool visible = overflow && binding.scroll.gameObject.activeInHierarchy;
            if (binding.hint.gameObject.activeSelf != visible) binding.hint.gameObject.SetActive(visible);
        }

        /// <summary>QA can verify the existing large drag surface without changing its authored geometry.</summary>
        public float ProgressDragHeightPixels(bool desktop)
        {
            ProgressBinding binding = desktop ? desktopProgress : mobileProgress;
            return binding == null ? 0f : binding.viewport.rect.height * PixelsPerLocalUnit(binding.viewport, true);
        }

        private static float PixelsPerLocalUnit(RectTransform rect, bool vertical)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>(true);
            if (canvas == null) return 1f;
            Canvas root = canvas.rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector3 direction = vertical ? Vector3.up : Vector3.right;
            Vector2 from = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.zero));
            Vector2 to = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(direction));
            float distance = Vector2.Distance(from, to);
            return distance > .001f ? distance : Mathf.Max(.001f, canvas.scaleFactor);
        }

        private void OnDisable()
        {
            foreach (LockedDescription binding in lockedDescriptions) binding.Restore();
            if (mobileProgress != null && mobileProgress.hint != null) mobileProgress.hint.gameObject.SetActive(false);
            if (desktopProgress != null && desktopProgress.hint != null) desktopProgress.hint.gameObject.SetActive(false);
        }
    }
}
