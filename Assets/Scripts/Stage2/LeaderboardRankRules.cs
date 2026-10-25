using System;
using YG;

namespace Vodovorot.Stage2
{
    public sealed class LeaderboardRankDecision
    {
        public bool accepted;
        public bool firstObservation;
        public bool improved;
        public bool newAllTimeBest;
        public int previousRank;
        public int rank;
        public int bonusCoins;
    }

    /// <summary>Pure policy. Only call with the own entry read after an acknowledged SDK write.</summary>
    public static class LeaderboardRankRules
    {
        public const string TableName = "BestPlayers";
        public const int DefaultBonusCoins = 5;

        public static LeaderboardRankDecision Apply(LeaderboardProfileRecord record, string playerId,
            string roundId, bool eligibleRound, int submittedXp, int confirmedScore, int confirmedRank,
            bool submissionAcknowledged, int configuredBonus = DefaultBonusCoins)
        {
            var result = new LeaderboardRankDecision();
            if (record == null || string.IsNullOrEmpty(playerId) || record.playerId != playerId ||
                string.IsNullOrEmpty(roundId) || submittedXp < 0 || confirmedScore < submittedXp ||
                confirmedRank <= 0 || !submissionAcknowledged || confirmedScore < record.lastConfirmedScore) return result;

            result.accepted = true;
            result.rank = confirmedRank;
            result.previousRank = record.lastConfirmedRank;
            bool baseline = record.bestRank <= 0 || record.lastConfirmedRank <= 0;
            bool repeat = record.lastEvaluatedRoundId == roundId || record.lastRewardRoundId == roundId;
            result.firstObservation = baseline;
            result.improved = !baseline && !repeat && confirmedRank < record.lastConfirmedRank;
            result.newAllTimeBest = !baseline && confirmedRank < record.bestRank;
            // Growth must belong to this round; a rank drift at unchanged XP is not a paid achievement.
            if (eligibleRound && !baseline && !repeat && result.newAllTimeBest &&
                submittedXp > record.lastConfirmedScore && record.lastRewardRoundId != roundId)
            {
                result.bonusCoins = Math.Max(0, Math.Min(configuredBonus, 100));
                record.lastRewardRoundId = roundId;
            }
            record.bestRank = record.bestRank > 0 ? Math.Min(record.bestRank, confirmedRank) : confirmedRank;
            record.lastConfirmedRank = confirmedRank;
            record.lastConfirmedScore = Math.Max(record.lastConfirmedScore, confirmedScore);
            record.lastEvaluatedRoundId = roundId;
            return result;
        }

        public static long XpToPass(int ownScore, int higherScore)
        {
            return ownScore < 0 || higherScore < ownScore ? 0 : (long)higherScore - ownScore + 1;
        }
    }
}
