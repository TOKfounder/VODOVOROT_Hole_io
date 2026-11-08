using UnityEngine;
using UnityEngine.UI;
using PinePie.SimpleJoystick.Examples.DemoScript;
using YG;

namespace Vodovorot.RoundExperience
{
    /// <summary>Explains an actual blocked attempt; it never changes fit or absorption.</summary>
    public sealed class TooLargeTargetFeedback : MonoBehaviour
    {
        [SerializeField] private HoleParent player;
        [SerializeField] private MovementScript movement;
        [SerializeField] private GamingManager game;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text label;
        [SerializeField] private RoundExperienceSettings settings;
        public float VisibleSeconds { get; set; } = 1.2f;
        private float shownUntil, nextSelection;
        private int requiredSize;
        private FallingObject current;
        private bool lastRussian;

        private void Awake()
        {
            if (settings != null)
                VisibleSeconds = settings.CreateRuntimeValues().BlockedTargetHintSeconds;
        }

        private void OnEnable()
        {
            if (player != null)
                player.TargetRejected += OnRejected;
            Hide();
        }

        private void OnDisable()
        {
            if (player != null)
                player.TargetRejected -= OnRejected;
            Hide();
        }

        private void OnRejected(FallingObject item)
        {
            if (panel == null || label == null || player == null || movement == null || game == null || item == null || !item.IsAvailableTarget ||
                Time.timeScale <= 0f || game.HasRoundEnded || movement.IntentDirection.sqrMagnitude < .01f)
                return;
            Bounds bounds = item.rend.bounds;
            if (bounds.size.y < .08f)
                return;
            Vector3 direction = bounds.ClosestPoint(player.transform.position) - player.transform.position;
            direction.y = 0f;
            float reach = Mathf.Max(player.size.x, player.size.z) * .5f + .25f;
            if (direction.sqrMagnitude > reach * reach ||
                (direction.sqrMagnitude > .01f && Vector3.Dot(movement.IntentDirection, direction.normalized) < .25f))
                return;
            if (current != item && Time.unscaledTime < nextSelection)
                return;
            if (current != item || Time.unscaledTime >= nextSelection)
            {
                requiredSize = player.RequiredSizeFor(item.size);
                if (requiredSize > 0 && requiredSize <= player.currentLevel + 1)
                    return; // The visible growth is still catching up with the earned size.
                current = item;
                nextSelection = Time.unscaledTime + .35f;
                RefreshText();
            }
            shownUntil = Time.unscaledTime + Mathf.Clamp(VisibleSeconds, .5f, 3f);
            panel.SetActive(true);
        }

        private void LateUpdate()
        {
            if (panel == null || !panel.activeSelf)
                return;
            if (Time.timeScale <= 0f || game == null || game.HasRoundEnded || current == null ||
                !current.IsAvailableTarget || Time.unscaledTime >= shownUntil ||
                (requiredSize > 0 && player.currentLevel + 1 >= requiredSize))
            {
                Hide();
                return;
            }
            if (lastRussian != (YG2.saves != null && YG2.saves.langRu))
                RefreshText();
        }

        private void RefreshText()
        {
            lastRussian = YG2.saves != null && YG2.saves.langRu;
            label.text = requiredSize > 0
                ? (lastRussian ? "Нужен размер " : "Requires size ") + requiredSize
                : (lastRussian ? "Этот объект слишком крупный" : "This object is too large");
        }

        private void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
            current = null;
        }
    }
}
