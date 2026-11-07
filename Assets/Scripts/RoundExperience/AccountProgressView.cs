using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Vodovorot.RoundExperience
{
    public sealed class AccountProgressView : MonoBehaviour
    {
        [SerializeField] private RoundExperienceSettings settings;
        [SerializeField] private MainMenuController menu;
        [SerializeField] private GamingManager round;
        [SerializeField] private Text goalText;
        private int experiencePerLevel;

        private void Awake()
        {
            experiencePerLevel = settings.CreateRuntimeValues().ExperiencePerLevel;
        }

        private void OnEnable()
        {
            if (menu != null) menu.ProgressUpdated += Refresh;
            if (round != null) round.RewardUpdated += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (menu != null) menu.ProgressUpdated -= Refresh;
            if (round != null) round.RewardUpdated -= Refresh;
        }

        private void Refresh()
        {
            if (goalText == null || YG2.saves == null) return;
            goalText.text = ProgressReadout.AccountGoal(YG2.saves.exp, experiencePerLevel, YG2.saves.langRu);
        }
    }
}
