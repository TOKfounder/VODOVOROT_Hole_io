#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vodovorot.Stage2;
using YG;
using YG.Utils.LB;

namespace Vodovorot.Stage3
{
    /// <summary>Reads the explicit PluginYG simulation table; writes only its private table clone.</summary>
    internal sealed class PluginYgRankSimulationTransport : ILeaderboardRoundTransport, IDisposable
    {
        private sealed class PendingReply { internal double due; internal string json; internal Action<string> receive; }
        private readonly List<PendingReply> pending = new List<PendingReply>();
        private LBData source;
        private LBData working;
        private RankSimulationPlan plan;
        private bool disposed;
        internal int SubmissionCount { get; private set; }
        public bool IsAvailable => !disposed;
        public bool IsSimulation => true;
        public string SimulationLabel => "ТЕСТ · " + (plan == null ? "PluginYG" : plan.Label) +
            (RankSimulationContext.UsesTestRoundXp ? " · тестовый XP" : " · XP раунда");

        internal PluginYgRankSimulationTransport()
        {
            EditorApplication.update += Tick;
        }

        internal void Configure(RankSimulationPlan value)
        {
            plan = value;
            source = ReadPluginTable();
            Validate(source, YG2.infoYG.Authorization.uniqueID);
            LBMethods.CopyLBData(out working, source);
            foreach (LBPlayerData entry in working.players)
            {
                if (entry.uniqueID == YG2.infoYG.Authorization.uniqueID) entry.score = plan.BeforeXp;
                else
                {
                    int index = Array.FindIndex(source.players, item => item != null && item.uniqueID == entry.uniqueID);
                    int npc = 0;
                    for (int i = 1; i <= RankSimulationPlan.NpcCount; i++)
                        if (entry.uniqueID == RankSimulationPlan.NpcId(i)) { npc = i; break; }
                    if (npc == 0) throw new InvalidOperationException("QA table contains an unseeded identity at row " + index);
                    entry.score = plan.NpcScore(npc);
                }
            }
            SortWorking();
        }

        internal static void Validate(LBData table, string playerId)
        {
            if (table == null || table.technoName != RankSimulationPlan.TableName || table.isInvertSortOrder ||
                table.players == null || table.players.Length != RankSimulationPlan.NpcCount + 1)
                throw new InvalidOperationException("Создайте отдельную QA-таблицу кнопкой «Подготовить таблицу PluginYG».");
            if (string.IsNullOrEmpty(playerId) || table.players.Any(item => item == null || string.IsNullOrEmpty(item.uniqueID)) ||
                table.players.Select(item => item.uniqueID).Distinct().Count() != table.players.Length ||
                table.players.Count(item => item.uniqueID == playerId) != 1)
                throw new InvalidOperationException("У каждой строки QA-таблицы должен быть уникальный ID; текущий игрок должен совпадать с Authorization simulation.");
            for (int i = 1; i <= RankSimulationPlan.NpcCount; i++)
                if (!table.players.Any(item => item.uniqueID == RankSimulationPlan.NpcId(i)))
                    throw new InvalidOperationException("QA NPC identity missing: " + RankSimulationPlan.NpcId(i));
        }

        private static LBData ReadPluginTable()
        {
            LBData result = null;
            Action<LBData> receive = data => { if (data != null && data.technoName == RankSimulationPlan.TableName) result = data; };
            YG2.onGetLeaderboard += receive;
            try { YG2.GetLeaderboard(RankSimulationPlan.TableName, 3, 2); }
            finally { YG2.onGetLeaderboard -= receive; }
            return result;
        }

        public void Request(string receiver, int requestId, string playerId, int xp, Action<string> reply)
        {
            if (disposed) return;
            var response = new LeaderboardTransportReply { requestId = requestId, playerId = playerId, status = "error" };
            try
            {
                // Exercise PluginYG's Editor entry points. SetLeaderboard intentionally does not persist there.
                YG2.SetLeaderboard(RankSimulationPlan.TableName, xp);
                Validate(ReadPluginTable(), playerId);
                if (!plan.IsError)
                {
                    LBPlayerData self = working.players.Single(item => item.uniqueID == playerId);
                    self.score = Math.Max(self.score, xp);
                    SubmissionCount++;
                    SortWorking();
                    response.status = "confirmed";
                    response.submitted = true;
                    response.rank = self.rank;
                    response.score = self.score;
                    response.neighborsAvailable = true;
                    response.entries = working.players.Where(item => Math.Abs(item.rank - self.rank) <= 2)
                        .Select(item => new LeaderboardNeighbor { rank = item.rank, score = item.score, name = item.name,
                            isSelf = item.uniqueID == playerId }).ToArray();
                }
                pending.Add(new PendingReply { due = EditorApplication.timeSinceStartup + plan.ReplyDelay,
                    json = JsonUtility.ToJson(response), receive = reply });
            }
            catch
            {
                RankSimulationContext.Stop();
                throw;
            }
        }

        private void SortWorking()
        {
            working.players = working.players.OrderByDescending(item => item.score).ThenBy(item => item.uniqueID, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < working.players.Length; i++) working.players[i].rank = i + 1;
            LBPlayerData self = working.players.Single(item => item.uniqueID == YG2.infoYG.Authorization.uniqueID);
            working.currentPlayer = new LBCurrentPlayerData { rank = self.rank, score = self.score };
        }

        private void Tick()
        {
            if (disposed) return;
            foreach (PendingReply item in pending.Where(item => item.due <= EditorApplication.timeSinceStartup).ToArray())
            {
                pending.Remove(item);
                try { item.receive(item.json); }
                catch (Exception error) { RankSimulationContext.Stop(); Debug.LogException(error); }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pending.Clear();
            EditorApplication.update -= Tick;
        }
    }
}
#endif
