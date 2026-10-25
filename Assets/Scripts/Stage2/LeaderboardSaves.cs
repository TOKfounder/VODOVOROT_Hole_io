using System;
using System.Collections.Generic;

namespace YG
{
    public partial class SavesYG
    {
        // A separate record per Yandex identity prevents an account switch from
        // treating another account's rank as a previous personal result.
        public List<LeaderboardProfileRecord> stage2LeaderboardProfiles = new List<LeaderboardProfileRecord>();
    }

    [Serializable]
    public sealed class LeaderboardProfileRecord
    {
        public string playerId;
        public int bestRank;
        public int lastConfirmedRank;
        public int lastConfirmedScore;
        public string lastEvaluatedRoundId;
        public string lastRewardRoundId;
    }
}
