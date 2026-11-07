#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vodovorot.Stage3;
using YG;
using YG.Utils.LB;

namespace Vodovorot.Stage3.Editor
{
    /// <summary>Explicit setup action. Adds one local PluginYG simulation table, never a Yandex table.</summary>
    public static class RankSimulationSetup
    {
        public static void SeedVisualTestTable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Подготовьте QA-таблицу вне Play Mode.");
            InfoYG settings = YG2.infoYG;
            string selfId = settings.Authorization.uniqueID;
            if (string.IsNullOrEmpty(selfId) || Enumerable.Range(1, RankSimulationPlan.NpcCount).Any(i => RankSimulationPlan.NpcId(i) == selfId))
                throw new InvalidOperationException("Задайте непустой уникальный ID текущего игрока в Authorization simulation.");
            var plan = RankSimulationPlan.Create(RankSimulationScenario.Up5);
            var players = new List<LBPlayerData>();
            string[] names = { "Маша", "Макс", "Алиса", "Тим", "Лео", "Вика", "Саша", "Рома" };
            for (int i = 1; i <= RankSimulationPlan.NpcCount; i++)
                players.Add(new LBPlayerData { uniqueID = RankSimulationPlan.NpcId(i),
                    name = names[(i - 1) % names.Length] + " · QA " + i.ToString("D2"),
                    score = plan.NpcScore(i), photo = InfoYG.ANONYMOUS });
            players.Add(new LBPlayerData { uniqueID = selfId, name = settings.Authorization.playerName + " · ТЕСТ",
                score = plan.BeforeXp, photo = InfoYG.ANONYMOUS });
            LBPlayerData[] sorted = players.OrderByDescending(item => item.score).ThenBy(item => item.uniqueID, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < sorted.Length; i++) sorted[i].rank = i + 1;
            LBPlayerData self = sorted.Single(item => item.uniqueID == selfId);
            var table = new LBData { technoName = RankSimulationPlan.TableName, type = "numeric", isInvertSortOrder = false,
                entries = "QA ONLY · local PluginYG simulation · no production ranking", players = sorted,
                currentPlayer = new LBCurrentPlayerData { rank = self.rank, score = self.score } };
            // Preserve every other field and every non-QA table; this is the only settings mutation.
            Undo.RecordObject(settings, "Подготовить таблицу проверки рейтинга");
            var tables = (settings.Leaderboards.listLBSim ?? new LBData[0]).ToList();
            int existing = tables.FindIndex(item => item != null && item.technoName == RankSimulationPlan.TableName);
            if (existing >= 0) tables[existing] = table;
            else tables.Add(table);
            settings.Leaderboards.listLBSim = tables.ToArray();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("[Stage3 QA] Prepared local " + RankSimulationPlan.TableName + ": 40 stable NPC IDs + Authorization simulation identity. No Yandex console calls.");
        }
    }
}
#endif
