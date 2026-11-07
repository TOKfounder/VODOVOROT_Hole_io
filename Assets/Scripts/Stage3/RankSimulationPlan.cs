#if UNITY_EDITOR
using System;

namespace Vodovorot.Stage3
{
    public enum RankSimulationScenario
    {
        FirstAppearance, Up1, Up5, Up20, Top3, NoImprovement,
        ReturnToPreviousBest, RepeatResult, Guest, Delay, Error
    }

    /// <summary>Deterministic Editor fixtures. All award decisions still belong to LeaderboardRankRules.</summary>
    public sealed class RankSimulationPlan
    {
        public const string TableName = "BestPlayers_VisualTest";
        public const int NpcCount = 40;
        public RankSimulationScenario Scenario { get; private set; }
        public int BeforeXp { get; private set; }
        public int AfterXp { get; private set; }
        public int BeforeRank { get; private set; }
        public int TargetRank { get; private set; }
        public int PreviousBestRank { get; private set; }
        public bool HasBaseline => Scenario != RankSimulationScenario.FirstAppearance && Scenario != RankSimulationScenario.Guest;
        public bool IsGuest => Scenario == RankSimulationScenario.Guest;
        public bool IsError => Scenario == RankSimulationScenario.Error;
        public float ReplyDelay => Scenario == RankSimulationScenario.Delay ? 2.5f : .15f;
        public string Label => LabelFor(Scenario);

        public static RankSimulationPlan Create(RankSimulationScenario scenario, int beforeXp = 1000, int earnedXp = 100)
        {
            beforeXp = Math.Min(int.MaxValue - 20000, Math.Max(0, beforeXp));
            int afterXp = (int)Math.Min(int.MaxValue - 10000L, (long)beforeXp + Math.Max(0, earnedXp));
            int target = scenario == RankSimulationScenario.Up1 ? 29 :
                scenario == RankSimulationScenario.Up20 ? 10 : scenario == RankSimulationScenario.Top3 ? 3 :
                scenario == RankSimulationScenario.Up5 || scenario == RankSimulationScenario.ReturnToPreviousBest ||
                scenario == RankSimulationScenario.RepeatResult || scenario == RankSimulationScenario.Delay ||
                scenario == RankSimulationScenario.Error ? 25 : 30;
            // Integer XP needs a score strictly between before and after to cross an NPC honestly.
            if (afterXp - beforeXp < 2) target = 30;
            return new RankSimulationPlan { Scenario = scenario, BeforeXp = beforeXp, AfterXp = afterXp,
                BeforeRank = 30, TargetRank = target,
                PreviousBestRank = scenario == RankSimulationScenario.ReturnToPreviousBest ? target : 30 };
        }

        public int NpcScore(int oneBasedNpc)
        {
            if (oneBasedNpc < 1 || oneBasedNpc > NpcCount) throw new ArgumentOutOfRangeException(nameof(oneBasedNpc));
            if (oneBasedNpc < TargetRank) return AfterXp + 100 + (NpcCount - oneBasedNpc) * 10;
            if (oneBasedNpc < BeforeRank) return BeforeXp + 1;
            return Math.Max(0, BeforeXp - 100 - (oneBasedNpc - BeforeRank) * 10);
        }

        public static string NpcId(int oneBasedNpc) => "vodovorot-visual-npc-" + oneBasedNpc.ToString("D2");
        public static string LabelFor(RankSimulationScenario scenario)
        {
            switch (scenario)
            {
                case RankSimulationScenario.FirstAppearance: return "Первое появление";
                case RankSimulationScenario.Up1: return "+1 место";
                case RankSimulationScenario.Up5: return "+5 мест";
                case RankSimulationScenario.Up20: return "+20 мест";
                case RankSimulationScenario.Top3: return "Вход в топ-3";
                case RankSimulationScenario.NoImprovement: return "Без улучшения";
                case RankSimulationScenario.ReturnToPreviousBest: return "Возврат к прежнему рекорду";
                case RankSimulationScenario.RepeatResult: return "Повтор результата";
                case RankSimulationScenario.Guest: return "Гость";
                case RankSimulationScenario.Delay: return "Задержка 2,5 с";
                case RankSimulationScenario.Error: return "Ошибка ответа";
                default: throw new ArgumentOutOfRangeException(nameof(scenario));
            }
        }
    }
}
#endif
