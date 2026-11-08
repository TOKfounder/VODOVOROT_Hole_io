using UnityEngine;

namespace Vodovorot.RoundExperience
{
    /// <summary>The authored modal backdrop becomes interactive only with an open result form.</summary>
    public sealed class ResultsCanvasVisibility : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private GameObject mobilePanel;
        [SerializeField] private GameObject desktopPanel;
        private bool visible;

        private void Awake() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (group == null)
                return;
            bool open = (mobilePanel != null && mobilePanel.activeSelf) ||
                (desktopPanel != null && desktopPanel.activeSelf);
            if (visible == open && group.alpha == (open ? 1f : 0f))
                return;
            visible = open;
            group.alpha = open ? 1f : 0f;
            group.interactable = open;
            group.blocksRaycasts = open;
        }
    }
}
