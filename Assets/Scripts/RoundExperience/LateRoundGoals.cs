using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Vodovorot.RoundExperience
{
    /// <summary>Optional route goals use real absorption events without changing rewards or saves.</summary>
    public sealed class LateRoundGoals : MonoBehaviour
    {
        [SerializeField] private GamingManager game;
        [SerializeField] private HoleParent player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private RoundExperienceSettings settings;
        [SerializeField] private RoundTargetGroup[] groups;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text label;
        [SerializeField] private Image directionArrow;
        private RoundGoalProgress[] progress;
        private float nextRefresh;
        public float RevealFraction { get; set; } = .5f;
        public int ActiveGroup { get; private set; }
        public RoundGoalProgress ProgressAt(int index) => progress[index];

        private void Awake()
        {
            if (settings != null)
                RevealFraction = settings.CreateRuntimeValues().SecondaryGoalRevealFraction;
            progress = groups.Select(group => new RoundGoalProgress(Enumerable.Range(0, group.Targets.Length))).ToArray();
        }

        private void OnEnable()
        {
            if (game != null)
                game.PlayerObjectAbsorbed += OnAbsorbed;
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (game != null)
                game.PlayerObjectAbsorbed -= OnAbsorbed;
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnAbsorbed(FallingObject item)
        {
            if (progress == null || item == null)
                return;
            for (int group = 0; group < groups.Length; group++)
                for (int index = 0; index < groups[group].Targets.Length; index++)
                    if (groups[group].Targets[index] == item)
                        progress[group].Collect(index);
            nextRefresh = 0f;
        }

        private void LateUpdate()
        {
            if (panel == null || label == null || game == null || player == null)
                return;
            bool visible = !game.HasRoundEnded && Time.timeScale > 0f &&
                game.timer >= game.totalCleaningDuration * Mathf.Clamp(RevealFraction, .35f, .8f);
            panel.SetActive(visible);
            if (!visible || Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }

        private void Refresh()
        {
            bool ru = YG2.saves == null || YG2.saves.langRu;
            for (int i = 0; i < groups.Length; i++)
                for (int index = 0; index < groups[i].Targets.Length; index++)
                    if (groups[i].Targets[index] == null || groups[i].Targets[index].value <= 0)
                        progress[i].MarkUnavailable(index);
            ActiveGroup = 0;
            while (ActiveGroup < groups.Length && progress[ActiveGroup].Resolved)
                ActiveGroup++;
            if (ActiveGroup == groups.Length)
            {
                int complete = progress.Count(goal => goal.Complete);
                label.text = ru ? $"Доп. цели: {complete}/{groups.Length} · продолжай уборку!"
                    : $"Side goals: {complete}/{groups.Length} · keep cleaning!";
                return;
            }
            RoundTargetGroup group = groups[ActiveGroup];
            RoundGoalProgress goalState = progress[ActiveGroup];
            FallingObject nearest = group.Targets.Where((item, index) => item != null && goalState.IsPending(index))
                .OrderBy(item => (item.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            string status = $"{goalState.Gathered}/{goalState.Total}";
            if (goalState.Lost > 0)
                status += ru ? $" · соперники: {goalState.Lost}" : $" · rivals: {goalState.Lost}";
            label.text = (ru ? "Доп. цель · " : "Side goal · ") + group.DisplayName(ru) + "  " + status;
            if (nearest == null)
                return;
            Vector3 delta = nearest.transform.position - player.transform.position;
            delta.y = 0;
            Vector3 direction = viewCamera != null ? viewCamera.transform.InverseTransformDirection(delta) : delta;
            if (directionArrow != null)
                directionArrow.rectTransform.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg);
            int size = player.RequiredSizeFor(nearest.size);
            label.text += "\n" + group.TargetKind(ru) + $" · {Mathf.CeilToInt(delta.magnitude)} " + (ru ? "м" : "m");
            if (size > player.currentLevel + 1)
                label.text += ru ? $" · нужен размер {size}" : $" · requires size {size}";
        }
    }
}
