using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Vodovorot.RoundExperience
{
    public sealed class RoundHudView : MonoBehaviour
    {
        [SerializeField] private HoleParent player;
        [SerializeField] private GamingManager game;
        [SerializeField] private Text growthText;

        private void OnEnable()
        {
            if (player != null) player.GrowthProgressChanged += RefreshGrowth;
            if (game != null) game.ProgressUpdated += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (player != null) player.GrowthProgressChanged -= RefreshGrowth;
            if (game != null) game.ProgressUpdated -= Refresh;
        }

        private void RefreshGrowth(HoleParent changedPlayer) => Refresh();

        private void Refresh()
        {
            if (player == null || growthText == null || YG2.saves == null) return;
            growthText.enabled = game == null || !game.HasRoundEnded;
            growthText.text = ProgressReadout.Growth(player.currentLevel, player.PointsToNextSize, YG2.saves.langRu);
        }
    }
}
