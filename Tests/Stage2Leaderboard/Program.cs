using System;
using Vodovorot.Stage2;
using YG;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
LeaderboardRankDecision Apply(LeaderboardProfileRecord r, string round, int xp, int rank, bool eligible = true,
    bool acknowledged = true, int? score = null, string player = "a") =>
    LeaderboardRankRules.Apply(r, player, round, eligible, xp, score ?? xp, rank, acknowledged);
var record = new LeaderboardProfileRecord { playerId = "a" };
var baseline = Apply(record, "first", 100, 80);
Check(baseline.accepted && baseline.firstObservation && baseline.bonusCoins == 0 && !baseline.improved, "First appearance establishes an unpaid baseline.");
Check(record.bestRank == 80 && record.lastConfirmedRank == 80, "Baseline is the actual confirmed rank.");
var improved = Apply(record, "second", 101, 70);
Check(improved.improved && improved.newAllTimeBest && improved.bonusCoins == 5, "A confirmed all-time best can pay five coins.");
Check(Apply(record, "second", 101, 70).bonusCoins == 0, "Duplicate callback cannot pay again.");
Check(Apply(record, "second", 102, 60).bonusCoins == 0, "Reusing a round with a later response cannot pay again.");
Apply(record, "drop", 103, 100);
var recovered = Apply(record, "recover", 104, 60);
Check(recovered.improved && !recovered.newAllTimeBest && recovered.bonusCoins == 0, "Returning to an achieved rank animates but does not pay.");
Check(Apply(record, "drift", 104, 50).bonusCoins == 0, "External rank drift without new XP cannot pay.");
Check(Apply(record, "early", 105, 40, false).bonusCoins == 0, "Early exit cannot pay rank coins.");
Check(Apply(record, "idle", 105, 30).bonusCoins == 0, "Idle XP cannot pay rank coins.");
int before = record.bestRank;
Check(!Apply(record, "pending", 200, 1, acknowledged: false).accepted && record.bestRank == before, "Unacknowledged submission cannot change best.");
Check(!Apply(record, "stale", 200, 1, score: 199).accepted && record.bestRank == before, "A stale post-write score cannot confirm rank.");
Check(!Apply(record, "wrong-account", 200, 1, player: "b").accepted && record.bestRank == before, "Cross-account response cannot change a profile.");
Check(!Apply(record, "no-entry", 200, 0).accepted, "No-entry rank is not an achievement.");
Check(!Apply(record, "", 200, 1).accepted, "Missing stable round identity is rejected.");
Check(!Apply(record, "negative", -1, 1).accepted, "Negative XP is rejected.");
var reload = new LeaderboardProfileRecord { playerId = "a", bestRank = record.bestRank, lastConfirmedRank = record.lastConfirmedRank, lastConfirmedScore = record.lastConfirmedScore, lastEvaluatedRoundId = record.lastEvaluatedRoundId, lastRewardRoundId = record.lastRewardRoundId };
Check(Apply(reload, "idle", 105, 30).bonusCoins == 0, "Persisted history protects a repeated round after reload.");
Check(LeaderboardRankRules.XpToPass(100, 100) == 1, "Ties require one additional XP.");
Check(LeaderboardRankRules.XpToPass(0, int.MaxValue) == 2147483648L, "Goal arithmetic cannot overflow.");
Check(LeaderboardRankRules.XpToPass(101, 100) == 0, "A stale weaker neighbor is not a next-place goal.");
var other = new LeaderboardProfileRecord { playerId = "b" };
Check(Apply(other, "new-account", 999, 1, player: "b").bonusCoins == 0, "A new identity has its own unpaid baseline.");
var partial = new LeaderboardProfileRecord { playerId = "a", bestRank = 20 };
Check(Apply(partial, "repair", 100, 80).bonusCoins == 0 && partial.bestRank == 20, "Incomplete history preserves the better known all-time rank without paying.");
Check(!Apply(record, "old-response", 100, 1, score: 100).accepted, "An older response cannot roll back a newer confirmed score.");
Check(LeaderboardProfileLoadRules.Choose(true, false, false) == LeaderboardAuthProfileChoice.LoadedAccount, "A real existing cloud account is selected without adding guest XP, wallet or skins.");
Check(LeaderboardProfileLoadRules.Choose(true, true, false) == LeaderboardAuthProfileChoice.GuestForEmptyAccount, "Explicit empty cloud during a deliberate login preserves the complete guest profile.");
Check(LeaderboardProfileLoadRules.Choose(false, true, false) == LeaderboardAuthProfileChoice.Ignore, "Ordinary startup must not guess that an empty cloud belongs to a post-round guest migration.");
Check(LeaderboardProfileLoadRules.Choose(true, false, true) == LeaderboardAuthProfileChoice.RejectBrokenCloud, "Broken cloud is not treated as an empty new account.");
Check(LeaderboardProfileLoadRules.Choose(true, true, true) == LeaderboardAuthProfileChoice.RejectBrokenCloud, "Corruption wins over an ambiguous missing flag.");
Console.WriteLine($"PASS: {checks} leaderboard rank/reward/profile-load boundary checks");
