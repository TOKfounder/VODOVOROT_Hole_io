using System;
using UnityEngine.Scripting;

namespace YG.Insides
{
    public partial class YGSendMessage
    {
        [Preserve]
        public void Stage2AuthDialogClosed(string authenticated)
        {
            Vodovorot.Stage2.LeaderboardRoundService.NotifyAuthDialogClosed(
                string.Equals(authenticated, "true", StringComparison.OrdinalIgnoreCase));
        }
    }
}
