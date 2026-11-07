#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Vodovorot.Stage2;

namespace Vodovorot.Stage3.Editor
{
    public sealed class RankSimulationWindow : EditorWindow
    {
        private RankSimulationScenario selected = RankSimulationScenario.Up5;
        private bool fixtureXp = true;
        private float seconds = 12f;
        private Vector2 scroll;
        private string error;

        [MenuItem("Tools/Vodovorot/Проверка рейтинга")]
        public static void Open()
        {
            var window = GetWindow<RankSimulationWindow>("Проверка рейтинга");
            window.minSize = new Vector2(405, 570);
        }

        private void OnEnable() { EditorApplication.update += Repaint; }
        private void OnDisable() { EditorApplication.update -= Repaint; }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Рейтинг · тестовый профиль", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Все XP, места и бонусы здесь тестовые. Профиль хранится только в памяти. Сохранения Editor заблокированы на время проверки; сеть и рабочая таблица BestPlayers не используются.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Подготовить таблицу PluginYG", GUILayout.Height(27))) Run(RankSimulationSetup.SeedVisualTestTable);
            EditorGUILayout.LabelField(RankSimulationPlan.TableName + " · 40 NPC · ID из Authorization simulation", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);
            string[] choices = Array.ConvertAll((RankSimulationScenario[])Enum.GetValues(typeof(RankSimulationScenario)), RankSimulationPlan.LabelFor);
            selected = (RankSimulationScenario)EditorGUILayout.Popup("Сценарий", (int)selected, choices);
            using (new EditorGUI.DisabledScope(RankSimulationContext.CityRoundRunning))
            {
                if (GUILayout.Button("Показать рейтинг сейчас", GUILayout.Height(34))) Run(() =>
                {
                    RankSimulationContext.Start(selected);
                    RankSimulationContext.ShowImmediate();
                });
                using (new EditorGUI.DisabledScope(!Application.isPlaying || !RankSimulationContext.Active || LeaderboardRoundService.Current == null))
                    if (GUILayout.Button("Повторить тот же результат", GUILayout.Height(27))) Run(RankSimulationContext.RepeatResult);
                EditorGUILayout.Space(7);
                fixtureXp = EditorGUILayout.ToggleLeft("Тестовый опыт для выбранного сценария", fixtureXp);
                EditorGUILayout.HelpBox(fixtureXp
                    ? "В коротком City работают настоящие движение, физика и таймер. На результате явно подставляются тестовый XP +100 и тестовый допуск к бонусу. Рейтинг и награду считает обычный сервис."
                    : "Используются XP и допуск настоящего раунда. Без полезного поглощения или роста XP выбранный подъём и бонус могут не произойти.", MessageType.None);
                seconds = EditorGUILayout.Slider("Длительность City, с", seconds, 5, 30);
                if (GUILayout.Button("Сыграть короткий City → рейтинг", GUILayout.Height(32))) Run(() =>
                {
                    RankSimulationContext.UseTestRoundXp = fixtureXp;
                    RankSimulationContext.Start(selected);
                    RankSimulationContext.StartCityRound(seconds);
                });
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Текущая проверка", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(RankSimulationContext.Detail, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("XP до / после", RankSimulationContext.BeforeXp + " / " + RankSimulationContext.AfterXp);
            EditorGUILayout.LabelField("Место до / после", Rank(RankSimulationContext.BeforeRank) + " / " + Rank(RankSimulationContext.AfterRank));
            EditorGUILayout.LabelField("Тестовый бонус", "+" + RankSimulationContext.TestBonusCoins + " монет · выдач: " + RankSimulationContext.AwardCount);
            EditorGUILayout.LabelField("Записей на диск заблокировано: " + RankSimulationContext.SuppressedSaveCount,
                EditorStyles.wordWrappedMiniLabel);
            if (LeaderboardRoundService.Current != null && RankSimulationContext.Active)
                EditorGUILayout.LabelField("Ответ сервиса", LeaderboardRoundService.Current.status.ToString());
            using (new EditorGUI.DisabledScope(!RankSimulationContext.Active && !EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Остановить и восстановить профиль", GUILayout.Height(30))) Run(() =>
                {
                    Stage3MusicAudition.Stop();
                    RankSimulationContext.Stop();
                });
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Музыкальные демо", EditorStyles.boldLabel);
            Stage3MusicAudition.DrawControls();
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        private static string Rank(int rank) => rank > 0 ? "#" + rank : "—";
        private void Run(Action action)
        {
            try { error = null; action(); }
            catch (Exception exception) { error = exception.Message; Debug.LogException(exception); }
        }
    }
}
#endif
