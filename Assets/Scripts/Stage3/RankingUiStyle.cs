using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Vodovorot.Stage3
{
    /// <summary>Shared game font, sprites and button states for ranking, results and achievement artwork.</summary>
    public static class RankingUiStyle
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Surface(string name, Transform parent, RankingVisualStyle style,
            Color color, out Image fill, bool shadow = false)
        {
            RectTransform rect = Rect(name, parent);
            var edge = rect.gameObject.AddComponent<Image>();
            edge.sprite = style.roundedShape; edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = 5f; edge.color = style.outline;
            if (shadow)
            {
                var drop = rect.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0, 0, 0, .35f); drop.effectDistance = new Vector2(0, -4);
            }
            RectTransform inner = Rect("Color", rect);
            Stretch(inner, style.border);
            fill = inner.gameObject.AddComponent<Image>();
            // The canonical outline has alpha 77/255 in its center. The separate
            // filled primitive has alpha 255/255, so background art cannot leak through.
            fill.sprite = style.filledShape; fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 6f; fill.color = color; fill.raycastTarget = false;
            return rect;
        }

        public static Text Label(string name, Transform parent, RankingVisualStyle style, int size,
            Color color, bool outlined = false, FontStyle fontStyle = FontStyle.Normal)
        {
            Text text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = style.comicFont; text.fontSize = size; text.fontStyle = fontStyle;
            text.color = color; text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignment = TextAnchor.MiddleLeft;
            if (outlined)
            {
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = style.outline;
                outline.effectDistance = new Vector2(style.textOutline, -style.textOutline);
                outline.useGraphicAlpha = true;
            }
            return text;
        }

        public static Button Button(string name, Transform parent, RankingVisualStyle style,
            Color color, UnityAction action)
        {
            RectTransform rect = Surface(name, parent, style, color, out Image fill);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
            colors.pressedColor = new Color(.8f, .8f, .8f, 1);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.6f, .6f, .6f, .85f);
            colors.fadeDuration = .08f; button.colors = colors;
            Text label = Label("Label", rect, style, 19, Color.white, true, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, 8);
            button.onClick.AddListener(action);
            return button;
        }

        public static Image Icon(string name, Transform parent, Sprite sprite)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        public static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
