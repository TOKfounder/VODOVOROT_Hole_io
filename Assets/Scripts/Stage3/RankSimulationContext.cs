#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vodovorot.Stage2;
using YG;

namespace Vodovorot.Stage3
{
    /// <summary>Explicit Editor session. The original profile, identity and service remain untouched.</summary>
    [InitializeOnLoad]
    public static class RankSimulationContext
    {
        private const string ArmedKey = "Vodovorot.Stage3.RankSimulation.Armed";
        private const string ScenarioKey = "Vodovorot.Stage3.RankSimulation.Scenario";
        private const string ActionKey = "Vodovorot.Stage3.RankSimulation.Action";
        private const string FixtureKey = "Vodovorot.Stage3.RankSimulation.Fixture";
        private const string SecondsKey = "Vodovorot.Stage3.RankSimulation.Seconds";
        private const string GuardKey = "Vodovorot.Stage3.RankSimulation.GuardUntilEdit";
        private const string OwnsPlayKey = "Vodovorot.Stage3.RankSimulation.OwnsPlay";
        private static SavesYG originalProfile, simulationProfile;
        private static YG2.PlayerData originalPlayer;
        private static IDisposable serviceSession;
        private static PluginYgRankSimulationTransport transport;
        private static RankSimulationPlan plan;
        private static readonly HashSet<string> observedAwards = new HashSet<string>();
        private static string roundId;
        private static float originalTimeScale;
        private static bool haveTimeScale, cityPending, cityRunning, cityFixturePrepared;
        private static GamingManager cityGame;
        private static double cityDeadline, queuedDeadline;
        private static float citySeconds;
        private static int cityStartXp;
        private static bool repeatQueued, repeatAlreadyShown;
        private static double repeatDue;

        public static bool Active => simulationProfile != null;
        public static bool BlockEditorSave => Active || SessionState.GetBool(ArmedKey, false) || SessionState.GetBool(GuardKey, false);
        public static RankSimulationScenario CurrentScenario { get; private set; }
        public static string ScenarioLabel => RankSimulationPlan.LabelFor(CurrentScenario);
        public static bool UseTestRoundXp { get; set; } = true;
        public static bool UsesTestRoundXp => !cityRunning || UseTestRoundXp;
        public static int BeforeXp { get; private set; }
        public static int AfterXp { get; private set; }
        public static int BeforeRank { get; private set; }
        public static int AfterRank { get; private set; }
        public static int TestBonusCoins { get; private set; }
        public static int AwardCount { get; private set; }
        public static int SuppressedSaveCount { get; private set; }
        public static int SubmissionCount => transport == null ? 0 : transport.SubmissionCount;
        public static string CurrentRoundId => roundId;
        public static string Detail { get; private set; } = "Симуляция выключена.";
        public static bool CityRoundRunning => cityPending || cityRunning;
        public static bool OriginalProfileRestored { get; private set; } = true;

        static RankSimulationContext()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.quitting += Stop;
            if (SessionState.GetBool(GuardKey, false) && EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
        }

        public static void Start(RankSimulationScenario scenario)
        {
            if (!Application.isPlaying)
            {
                QueuePlay(scenario, "none", UseTestRoundXp, 12f);
                return;
            }
            try
            {
                if (cityPending || cityRunning) throw new InvalidOperationException("Остановите проверку City перед выбором другого сценария.");
                if (Active && serviceSession != null) Restore(false);
                SessionState.SetBool(ArmedKey, true);
                SessionState.SetInt(ScenarioKey, (int)scenario);
                CurrentScenario = scenario;
                EnsureProfileIsolation();
                ConfigureSession();
            }
            catch { Stop(); throw; }
        }

        public static void ShowImmediate()
        {
            if (!Application.isPlaying)
            {
                SessionState.SetString(ActionKey, "immediate");
                if (!EditorApplication.isPlayingOrWillChangePlaymode) QueuePlay(CurrentScenario, "immediate", UseTestRoundXp, 12f);
                return;
            }
            try
            {
                if (!Active || serviceSession == null) throw new InvalidOperationException("Сначала вызовите Start(сценарий).");
                if (cityPending || cityRunning) throw new InvalidOperationException("Дождитесь окончания короткого раунда или остановите проверку.");
                simulationProfile.exp = plan.AfterXp;
                AfterXp = simulationProfile.exp;
                Time.timeScale = 0f;
                Detail = "Тестовый XP и тестовый результат · изолированный профиль в памяти · " + ScenarioLabel;
                // The same Show -> BeginRound -> transport -> Changed -> celebration path as a real result.
                LeaderboardRoundPanel.Show(roundId, true);
            }
            catch { Stop(); throw; }
        }

        public static void RepeatResult()
        {
            if (!Application.isPlaying || !Active || string.IsNullOrEmpty(roundId)) return;
            if (LeaderboardRoundService.Current == null) return;
            LeaderboardRoundPanel.Show(roundId, true);
            Detail = "Повтор того же результата: тот же roundId; монеты и звук повторно не выдаются.";
        }

        public static void StartCityRound(float seconds = 12f)
        {
            if (!Application.isPlaying)
            {
                SessionState.SetString(ActionKey, "city");
                SessionState.SetFloat(SecondsKey, Mathf.Clamp(seconds, 5f, 30f));
                if (!EditorApplication.isPlayingOrWillChangePlaymode) QueuePlay(CurrentScenario, "city", UseTestRoundXp, seconds);
                return;
            }
            if (!Active || serviceSession == null) throw new InvalidOperationException("Сначала вызовите Start(сценарий).");
            try
            {
                LeaderboardRoundPanel.Close();
                citySeconds = Mathf.Clamp(seconds, 5f, 30f);
                cityStartXp = simulationProfile.exp;
                simulationProfile.selectedMapID = 0;
                simulationProfile.chosenMode = 0; // Actual TotalCleaning mode.
                cityPending = true;
                cityFixturePrepared = false;
                cityDeadline = EditorApplication.timeSinceStartup + 25;
                Time.timeScale = 1f;
                Detail = UseTestRoundXp
                    ? "City: реальные движение, физика и таймер; после раунда — явно тестовый XP и допуск к бонусу выбранного сценария."
                    : "City: реальный результат и допуск. Без полезного поглощения/роста XP бонус не выдаётся.";
                SceneManager.LoadSceneAsync(CascadeCityLayout.CitySceneName);
            }
            catch { Stop(); throw; }
        }

        /// <summary>Storage calls this before an Editor load; active simulation must never reload disk into its profile.</summary>
        public static bool TryLoadIsolatedEditorProfile()
        {
            if (!Active) return false;
            YG2.saves = simulationProfile;
            return true;
        }

        /// <summary>The first normal plugin disk read is retained, then isolated before any gameplay can save.</summary>
        public static void OnEditorProfileLoaded()
        {
            if (!SessionState.GetBool(ArmedKey, false) || Active) return;
            try { EnsureProfileIsolation(); }
            catch { Stop(); throw; }
        }

        public static void NotifySuppressedEditorSave() { SuppressedSaveCount++; }

        internal static void BeforeRoundBegins(string actualRoundId, ref bool eligibleRound)
        {
            if (!Active || !cityRunning || cityFixturePrepared || cityGame == null || cityGame.Stage2RoundId != actualRoundId) return;
            try
            {
                cityFixturePrepared = true;
                roundId = actualRoundId;
                int earnedXp = Math.Max(0, simulationProfile.exp - cityStartXp);
                if (UseTestRoundXp)
                {
                    plan = RankSimulationPlan.Create(CurrentScenario, cityStartXp, 100);
                    simulationProfile.exp = plan.AfterXp;
                    eligibleRound = true;
                    Detail = "Реальный короткий City завершён. ТЕСТОВЫЙ XP +100 и тестовый допуск; бонус считает обычное правило рейтинга.";
                }
                else
                {
                    plan = RankSimulationPlan.Create(CurrentScenario, cityStartXp, earnedXp);
                    Detail = "Реальный City: XP +" + earnedXp + "; допуск к бонусу: " + (eligibleRound ? "да" : "нет") +
                        ". Таблица QA; исходный профиль не изменяется.";
                }
                PrepareProfileRecord();
                transport.Configure(plan);
                BeforeXp = plan.BeforeXp; AfterXp = simulationProfile.exp;
            }
            catch { Stop(); throw; }
        }

        private static void EnsureProfileIsolation()
        {
            if (Active) return;
            if (YG2.saves == null) throw new InvalidOperationException("PluginYG ещё не загрузил профиль.");
            originalProfile = YG2.saves;
            originalPlayer = YG2.player;
            simulationProfile = JsonUtility.FromJson<SavesYG>(JsonUtility.ToJson(originalProfile));
            if (simulationProfile == null) throw new InvalidOperationException("Не удалось создать профиль в памяти.");
            simulationProfile.exp = 1000;
            simulationProfile.goldCoins = 100;
            simulationProfile.diamonds = 0;
            simulationProfile.isGaming = false;
            simulationProfile.langRu = true;
            simulationProfile.stage2LeaderboardProfiles = new List<LeaderboardProfileRecord>();
            if (SessionState.GetBool("Vodovorot.RoundExperience.SilentFreshProfile", false))
            {
                var fresh = new SavesYG { isFirst = false, selectedMapID = 0, tutorialDone = true, soundValue = 0f, musicValue = 0f };
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), simulationProfile);
            }
            YG2.saves = simulationProfile;
            originalTimeScale = Time.timeScale;
            haveTimeScale = true;
            OriginalProfileRestored = false;
        }

        private static void ConfigureSession()
        {
            if (!YG2.isSDKEnabled) throw new InvalidOperationException("Дождитесь инициализации PluginYG.");
            CurrentScenario = (RankSimulationScenario)SessionState.GetInt(ScenarioKey, (int)CurrentScenario);
            // Only the runtime identity is replaced. The Authorization settings asset remains untouched.
            YG2.player = new YG2.PlayerData { auth = CurrentScenario != RankSimulationScenario.Guest,
                id = YG2.infoYG.Authorization.uniqueID, name = YG2.infoYG.Authorization.playerName,
                photo = originalPlayer == null ? "" : originalPlayer.photo,
                payingStatus = YG2.infoYG.Authorization.payingStatus };
            plan = RankSimulationPlan.Create(CurrentScenario);
            BeforeXp = plan.BeforeXp; AfterXp = plan.BeforeXp;
            BeforeRank = plan.HasBaseline ? plan.BeforeRank : 0; AfterRank = 0;
            TestBonusCoins = 0; AwardCount = 0; SuppressedSaveCount = 0;
            observedAwards.Clear();
            repeatAlreadyShown = false;
            roundId = "visual-test-" + Guid.NewGuid().ToString("N");
            PrepareProfileRecord();
            transport = new PluginYgRankSimulationTransport();
            transport.Configure(plan);
            serviceSession = LeaderboardRoundService.IsolateEditorSession(transport);
            LeaderboardRoundService.BonusCoins = LeaderboardRankRules.DefaultBonusCoins;
            LeaderboardRoundService.Changed += Observe;
            Detail = "ТЕСТ · " + ScenarioLabel + " · профиль только в памяти · таблица " + RankSimulationPlan.TableName;
        }

        private static void PrepareProfileRecord()
        {
            simulationProfile.stage2LeaderboardProfiles.Clear();
            if (plan.HasBaseline)
                simulationProfile.stage2LeaderboardProfiles.Add(new LeaderboardProfileRecord {
                    playerId = YG2.player.id, bestRank = plan.PreviousBestRank, lastConfirmedRank = plan.BeforeRank,
                    lastConfirmedScore = plan.BeforeXp, lastEvaluatedRoundId = "visual-test-baseline" });
            BeforeRank = plan.HasBaseline ? plan.BeforeRank : 0;
        }

        private static void Observe(LeaderboardRoundSnapshot snapshot)
        {
            if (!Active || snapshot == null || !snapshot.isSimulation || snapshot.roundId != roundId) return;
            AfterXp = simulationProfile.exp;
            AfterRank = snapshot.status == LeaderboardRoundStatus.Confirmed ? snapshot.rank : 0;
            if (snapshot.status == LeaderboardRoundStatus.Confirmed && snapshot.bonusCoins > 0 && observedAwards.Add(snapshot.roundId))
            { TestBonusCoins += snapshot.bonusCoins; AwardCount++; }
            if (CurrentScenario == RankSimulationScenario.RepeatResult && snapshot.status == LeaderboardRoundStatus.Confirmed && !repeatAlreadyShown)
            {
                repeatAlreadyShown = true;
                repeatQueued = true;
                repeatDue = EditorApplication.timeSinceStartup + 2;
            }
        }

        private static void QueuePlay(RankSimulationScenario scenario, string action, bool fixture, float seconds)
        {
            CurrentScenario = scenario;
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetBool(OwnsPlayKey, true);
            SessionState.SetInt(ScenarioKey, (int)scenario);
            SessionState.SetString(ActionKey, action);
            SessionState.SetBool(FixtureKey, fixture);
            SessionState.SetFloat(SecondsKey, Mathf.Clamp(seconds, 5f, 30f));
            queuedDeadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.isPlaying = true;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ArmedKey, false))
                queuedDeadline = EditorApplication.timeSinceStartup + 45;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (Active || SessionState.GetBool(ArmedKey, false)) SessionState.SetBool(GuardKey, true);
                Restore(true);
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(ArmedKey, false);
                SessionState.SetBool(GuardKey, false);
                SessionState.SetBool(OwnsPlayKey, false);
                SessionState.SetString(ActionKey, "");
            }
        }

        private static void Tick()
        {
            try
            {
                ClearIdleTransitionGuard();
                if (Application.isPlaying && SessionState.GetBool(ArmedKey, false) && serviceSession == null && YG2.isSDKEnabled)
                {
                    string action = SessionState.GetString(ActionKey, "");
                    CurrentScenario = (RankSimulationScenario)SessionState.GetInt(ScenarioKey, 0);
                    UseTestRoundXp = SessionState.GetBool(FixtureKey, true);
                    EnsureProfileIsolation();
                    ConfigureSession();
                    SessionState.SetString(ActionKey, "");
                    if (action == "immediate") ShowImmediate();
                    else if (action == "city") StartCityRound(SessionState.GetFloat(SecondsKey, 12f));
                }
                if (Application.isPlaying && SessionState.GetBool(ArmedKey, false) && serviceSession == null &&
                    queuedDeadline > 0 && EditorApplication.timeSinceStartup > queuedDeadline)
                    throw new InvalidOperationException("PluginYG не инициализировался за 45 секунд; проверка остановлена.");
                if (cityPending)
                {
                    if (EditorApplication.timeSinceStartup > cityDeadline) throw new InvalidOperationException("City не загрузился за 25 секунд.");
                    if (SceneManager.GetActiveScene().name == CascadeCityLayout.CitySceneName)
                    {
                        cityGame = UnityEngine.Object.FindAnyObjectByType<GamingManager>();
                        if (cityGame != null && !string.IsNullOrEmpty(cityGame.Stage2RoundId))
                        {
                            cityPending = false; cityRunning = true;
                            cityGame.totalCleaningDuration = citySeconds;
                            cityGame.timer = 0;
                            cityGame.HandleTimer(true);
                            cityStartXp = simulationProfile.exp;
                        }
                    }
                }
                if (repeatQueued && EditorApplication.timeSinceStartup >= repeatDue)
                {
                    repeatQueued = false;
                    if (!string.IsNullOrEmpty(roundId) && observedAwards.Contains(roundId)) RepeatResult();
                }
            }
            catch (Exception error) { Stop(); Debug.LogException(error); }
        }

        private static void ClearIdleTransitionGuard()
        {
            // Cancelling before Play starts may emit no EnteredEditMode callback.
            // Keep protection throughout teardown/reload, then release it only in a fully idle Editor.
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying ||
                SessionState.GetBool(ArmedKey, false) || Active) return;
            if (SessionState.GetBool(GuardKey, false)) SessionState.SetBool(GuardKey, false);
            if (SessionState.GetBool(OwnsPlayKey, false)) SessionState.SetBool(OwnsPlayKey, false);
        }

        private static void BeforeReload()
        {
            // During entry to Play, keep the queued request/early storage guard across domain reload.
            if (Active)
            {
                SessionState.SetBool(GuardKey, true);
                Restore(true);
                if (Application.isPlaying) EditorApplication.isPlaying = false;
            }
        }

        public static void Stop()
        {
            bool endPlay = EditorApplication.isPlayingOrWillChangePlaymode &&
                (cityPending || cityRunning || SessionState.GetBool(OwnsPlayKey, false));
            if (endPlay) SessionState.SetBool(GuardKey, true);
            Restore(true);
            if (endPlay) EditorApplication.isPlaying = false;
        }

        private static void Restore(bool clearQueue)
        {
            LeaderboardRoundService.Changed -= Observe;
            LeaderboardRoundPanel.Close();
            try { transport?.Dispose(); }
            finally
            {
                transport = null;
                try { serviceSession?.Dispose(); }
                finally
                {
                    serviceSession = null;
                    if (originalProfile != null) YG2.saves = originalProfile;
                    if (originalPlayer != null) YG2.player = originalPlayer;
                    OriginalProfileRestored = originalProfile == null || ReferenceEquals(YG2.saves, originalProfile);
                    originalProfile = null; simulationProfile = null; originalPlayer = null;
                    if (haveTimeScale) Time.timeScale = originalTimeScale;
                    haveTimeScale = false;
                    cityPending = false; cityRunning = false; cityGame = null; cityFixturePrepared = false; repeatQueued = false;
                    if (clearQueue)
                    {
                        SessionState.SetBool(ArmedKey, false);
                        SessionState.SetString(ActionKey, "");
                        Detail = "Симуляция выключена. Исходные профиль и сервис восстановлены.";
                    }
                }
            }
        }
    }
}
#endif
