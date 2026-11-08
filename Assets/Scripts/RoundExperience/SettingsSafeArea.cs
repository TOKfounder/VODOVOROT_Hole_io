using UnityEngine;

namespace Vodovorot.RoundExperience
{
    /// <summary>Adapts the saved settings container to viewport/device insets.</summary>
    public sealed class SettingsSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        private Rect previous;
        private int width, height;

        private void LateUpdate()
        {
            if (content == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            if (area == previous && width == Screen.width && height == Screen.height) return;
            width = Screen.width;
            height = Screen.height;
            previous = area;
            content.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / width), Mathf.Clamp01(area.yMin / height));
            content.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / width), Mathf.Clamp01(area.yMax / height));
            content.offsetMin = content.offsetMax = Vector2.zero;
        }
    }
}
