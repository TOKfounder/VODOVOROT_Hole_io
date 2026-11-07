using System;
using System.Collections.Generic;
using System.Linq;
using Vodovorot.Stage2;
using Vodovorot.Stage3;
using YG;

int checks = 0;
void Check(bool value, string detail) { checks++; if (!value) throw new Exception(detail); }
const string selfId = "000";
int Rank(RankSimulationPlan plan, int xp) => Enumerable.Range(1, RankSimulationPlan.NpcCount).Count(i => plan.NpcScore(i) > xp) + 1;
foreach (RankSimulationScenario scenario in Enum.GetValues<RankSimulationScenario>())
{
    RankSimulationPlan plan = RankSimulationPlan.Create(scenario);
    Check(Rank(plan, plan.BeforeXp) == 30, scenario + ": own seed must start at actual rank 30.");
    Check(Rank(plan, plan.AfterXp) == plan.TargetRank, scenario + ": table scores must produce the selected actual rank.");
    Check(Enumerable.Range(1, RankSimulationPlan.NpcCount).Select(RankSimulationPlan.NpcId).Append(selfId).Distinct().Count() == 41,
        scenario + ": every NPC and the Authorization identity must be unique.");
    if (plan.IsGuest || plan.IsError) continue; // The transport/service returns status; no acknowledged policy call.
    var record = new LeaderboardProfileRecord { playerId = selfId };
    if (plan.HasBaseline)
    {
        record.bestRank = plan.PreviousBestRank;
        record.lastConfirmedRank = plan.BeforeRank;
        record.lastConfirmedScore = plan.BeforeXp;
        record.lastEvaluatedRoundId = "baseline";
    }
    string round = "round-" + scenario;
    LeaderboardRankDecision actual = LeaderboardRankRules.Apply(record, selfId, round, true,
        plan.AfterXp, plan.AfterXp, Rank(plan, plan.AfterXp), true);
    int expectedBonus = scenario == RankSimulationScenario.FirstAppearance || scenario == RankSimulationScenario.NoImprovement ||
        scenario == RankSimulationScenario.ReturnToPreviousBest ? 0 : 5;
    Check(actual.accepted && actual.bonusCoins == expectedBonus, scenario + ": ordinary policy must compute its expected bonus.");
    Check(actual.firstObservation == !plan.HasBaseline, scenario + ": only a first appearance establishes the baseline.");
    Check(actual.improved == (plan.HasBaseline && plan.TargetRank < 30), scenario + ": celebration must follow the actual rank comparison.");
    LeaderboardRankDecision repeated = LeaderboardRankRules.Apply(record, selfId, round, true,
        plan.AfterXp, plan.AfterXp, plan.TargetRank, true);
    Check(repeated.accepted && repeated.bonusCoins == 0 && !repeated.improved, scenario + ": repeated round cannot pay or celebrate twice.");
    var ineligible = new LeaderboardProfileRecord { playerId = selfId, bestRank = 30, lastConfirmedRank = 30, lastConfirmedScore = plan.BeforeXp };
    Check(LeaderboardRankRules.Apply(ineligible, selfId, round, false, plan.AfterXp, plan.AfterXp, plan.TargetRank, true).bonusCoins == 0,
        scenario + ": real short City with no eligible absorption cannot acquire a bonus through the simulator.");
}
foreach (int earned in new[] { 0, 1, 2, 25, 50 })
{
    RankSimulationPlan plan = RankSimulationPlan.Create(RankSimulationScenario.Up20, 1000, earned);
    Check(Rank(plan, plan.BeforeXp) == 30 && Rank(plan, plan.AfterXp) == plan.TargetRank,
        "Actual City reward " + earned + " must still match the table, including no-XP results.");
    Check(plan.TargetRank == (earned < 2 ? 30 : 10), "Insufficient integer XP must not invent crossed competitors.");
}
var extreme = RankSimulationPlan.Create(RankSimulationScenario.Top3, int.MaxValue, int.MaxValue);
Check(extreme.AfterXp >= extreme.BeforeXp && Enumerable.Range(1, 40).All(i => extreme.NpcScore(i) >= 0), "Fixture arithmetic must not overflow.");
Check(RankSimulationPlan.TableName != LeaderboardRankRules.TableName, "QA and production tables must remain distinct.");
Console.WriteLine($"PASS: {checks} Stage 3 simulation scenario/rank/bonus/idempotency contracts");
