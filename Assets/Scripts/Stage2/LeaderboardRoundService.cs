using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using YG;
using Vodovorot.Stage3;

namespace Vodovorot.Stage2
{
    public enum LeaderboardRoundStatus { WaitingForProfile, Guest, Authenticating, Loading, Confirmed, Unavailable, Pending }

    [Serializable]
    public sealed class LeaderboardNeighbor
    {
        public int rank;
        public int score;
        public string name;
        public bool isSelf;
    }

    public sealed class LeaderboardRoundSnapshot
    {
        public string roundId;
        public LeaderboardRoundStatus status;
        public int rank;
        public int score;
        public int previousRank;
        public int bonusCoins;
        public bool improved;
        public bool firstObservation;
        public bool neighborsAvailable;
        public LeaderboardNeighbor[] neighbors = new LeaderboardNeighbor[0];
        public int targetRank;
        public long targetXp;
        public bool isSimulation;
        public string simulationLabel;
    }

    /// <summary>One network transaction and at most one persisted coin bonus per real round.</summary>
    public sealed class LeaderboardRoundService : MonoBehaviour
    {
        public static event Action<LeaderboardRoundSnapshot> Changed;
        public static int BonusCoins { get; set; } = LeaderboardRankRules.DefaultBonusCoins;
        private static LeaderboardRoundService instance;
        private readonly Dictionary<string, Round> rounds = new Dictionary<string, Round>();
        private readonly Dictionary<int, Round> pending = new Dictionary<int, Round>();
        private Round current;
        private int nextRequest;
        private bool authLoadPending;
        private bool authProfileInvalid;
        private SavesYG preLoginGuestProfile;
        private Round authRound;
        private bool authAttemptTimedOut;
        private bool authDialogOpen;
        private float lastAuthLoadAt;
        private const float AuthRetryInterval = 16f;

        private sealed class Round
        {
            public string id;
            public bool eligible;
            public bool requestStarted;
            public bool authAttempted;
            public bool celebrationConsumed;
            public SavesYG profile;
            public string playerId;
            public int xp;
            public float deadline;
            public LeaderboardRoundSnapshot snapshot;
        }

#if UNITY_WEBGL && !UNITY_EDITOR && YandexGamesPlatform_yg
        [DllImport("__Internal")] private static extern void LeaderboardStage2_AuthLoad(int state);
#endif
        public static bool IsAuthProfileWriteBlocked => instance != null &&
            (instance.authLoadPending || instance.authProfileInvalid) && YG2.player != null && YG2.player.auth;

        public static bool CanRetryProfileLoad => instance != null && instance.authProfileInvalid &&
            instance.current != null && !instance.authDialogOpen && YG2.isSDKEnabled && YG2.player != null && YG2.player.auth &&
            YG2.infoYG.Storage.saveCloud;
        public static float RetryAllowedIn => instance == null ? 0 :
            Mathf.Max(0, instance.lastAuthLoadAt + AuthRetryInterval - Time.realtimeSinceStartup);

        public static void RequestConnection()
        {
            if (CanRetryProfileLoad) RetryProfileLoad();
            else RequestLogin();
        }

        public static void RetryProfileLoad()
        {
            if (!CanRetryProfileLoad || RetryAllowedIn > 0) return;
            // Capture the live guest object again, including changes earned while the connection was unavailable.
            instance.preLoginGuestProfile = YG2.saves;
            instance.authRound = instance.current;
            instance.authLoadPending = true;
            instance.authAttemptTimedOut = false;
            instance.lastAuthLoadAt = Time.realtimeSinceStartup;
            instance.authRound.deadline = Time.realtimeSinceStartup + 25f;
            MarkBrowserAuthLoad(1);
            instance.SetStatus(instance.authRound, LeaderboardRoundStatus.Authenticating);
            // The user is already signed in; retry cloud data rather than showing another login dialog.
            YG.Insides.YGInsides.LoadCloud();
        }

        private static void MarkBrowserAuthLoad(int state)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && YandexGamesPlatform_yg
            LeaderboardStage2_AuthLoad(state);
#endif
        }

        public static bool CanUsePlatform => LeaderboardRoundTransport.Current.IsAvailable;

        public static LeaderboardRoundSnapshot Current
        {
            get
            {
                if (instance == null || instance.current == null) return null;
                Round round = instance.current;
                return round.snapshot.status != LeaderboardRoundStatus.Confirmed || HasCurrentProfile(round) ? round.snapshot : null;
            }
        }

        private static bool HasCurrentProfile(Round round) => YG2.isSDKEnabled && YG2.player != null && YG2.player.auth &&
            ReferenceEquals(YG2.saves, round.profile) && YG2.player.id == round.playerId;
        public static int LatestConfirmedRank => TryGetConfirmedRank(out int rank) ? rank : 0;
        public static int LatestConfirmedScore
        {
            get { LeaderboardProfileRecord record = FindRecord(); return record != null ? record.lastConfirmedScore : 0; }
        }

        public static bool TryGetConfirmedRank(out int rank)
        {
            LeaderboardProfileRecord record = FindRecord();
            rank = record != null ? record.lastConfirmedRank : 0;
            return rank > 0;
        }

        private static LeaderboardProfileRecord FindRecord()
        {
            if (!YG2.isSDKEnabled || YG2.saves == null || YG2.player == null || !YG2.player.auth ||
                string.IsNullOrEmpty(YG2.player.id) || YG2.saves.stage2LeaderboardProfiles == null) return null;
            return YG2.saves.stage2LeaderboardProfiles.Find(item => item != null && item.playerId == YG2.player.id);
        }

        public static void BeginRound(string roundId, bool eligibleRound)
        {
            if (string.IsNullOrEmpty(roundId)) return;
#if UNITY_EDITOR
            RankSimulationContext.BeforeRoundBegins(roundId, ref eligibleRound);
#endif
            if (instance == null)
            {
                var go = new GameObject("Stage2LeaderboardService");
                instance = go.AddComponent<LeaderboardRoundService>();
                DontDestroyOnLoad(go);
            }
            instance.Begin(roundId, eligibleRound);
        }

        private void OnEnable() { YG2.onGetSDKData += ProfileLoaded; }
        private void OnDisable() { YG2.onGetSDKData -= ProfileLoaded; }
        private void OnDestroy() { if (instance == this) instance = null; }

        private void Begin(string roundId, bool eligibleRound)
        {
            if (!rounds.TryGetValue(roundId, out current))
            {
                current = new Round { id = roundId, eligible = eligibleRound, deadline = Time.realtimeSinceStartup + 12f,
                    snapshot = new LeaderboardRoundSnapshot { roundId = roundId, status = LeaderboardRoundStatus.WaitingForProfile,
                        isSimulation = LeaderboardRoundTransport.Current.IsSimulation,
                        simulationLabel = LeaderboardRoundTransport.Current.SimulationLabel } };
                rounds.Add(roundId, current);
                TryStart(current);
            }
            if (current.requestStarted && !HasCurrentProfile(current)) current.snapshot.status = LeaderboardRoundStatus.Unavailable;
            Publish(current);
        }

        private void Update()
        {
            if (current == null) return;
            if (current.snapshot.status == LeaderboardRoundStatus.WaitingForProfile)
            {
                if (Time.realtimeSinceStartup > current.deadline) SetStatus(current, LeaderboardRoundStatus.Unavailable);
                else TryStart(current);
            }
            // Choosing an account or entering credentials has no arbitrary cancellation timeout.
            // The cloud timer starts only after the SDK explicitly closes the dialog successfully.
            if (authLoadPending && authRound != null && !authDialogOpen && !authAttemptTimedOut &&
                Time.realtimeSinceStartup > authRound.deadline)
                NotifyAuthCloudLoadFailed();
            var expired = new List<int>();
            foreach (var item in pending)
                if (Time.realtimeSinceStartup > item.Value.deadline) expired.Add(item.Key);
            foreach (int request in expired)
            {
                Round round = pending[request]; pending.Remove(request);
                SetStatus(round, LeaderboardRoundStatus.Unavailable);
            }
        }

        private void ProfileLoaded()
        {
            if (current == null) return;
            foreach (Round round in rounds.Values)
                if (round.requestStarted && !HasCurrentProfile(round)) round.snapshot.status = LeaderboardRoundStatus.Unavailable;
            Publish(current);
            // The plugin also emits this event immediately after starting an async cloud load.
            // Only local loads are synchronous; cloud readiness uses the explicit storage hook below.
            if (authLoadPending && YG2.player != null && YG2.player.auth && !YG2.infoYG.Storage.saveCloud)
                CompleteAuthProfileLoad();
        }

        public static void RequestLogin()
        {
            if (instance == null || instance.current == null || !CanUsePlatform || !YG2.isSDKEnabled ||
                YG2.player == null || YG2.player.auth || instance.current.authAttempted) return;
            Round round = instance.current;
            round.authAttempted = true;
            instance.authLoadPending = true;
            instance.authDialogOpen = true;
            instance.preLoginGuestProfile = YG2.saves;
            instance.authRound = round;
            instance.authAttemptTimedOut = false;
            instance.lastAuthLoadAt = Time.realtimeSinceStartup;
            MarkBrowserAuthLoad(1);
            round.deadline = Time.realtimeSinceStartup + 25f;
            instance.SetStatus(round, LeaderboardRoundStatus.Authenticating);
            // This method is called only by the explicit "Sign in with Yandex ID" button.
            YG2.OpenAuthDialog();
        }

        public static void NotifyAuthDialogClosed(bool authenticated)
        {
            if (instance == null || !instance.authDialogOpen) return;
            instance.authDialogOpen = false;
            if (authenticated)
            {
                // A synchronous/local or very fast cloud result may already have resolved this profile.
                // Do not reopen the gate or repeat the rank transaction in that case.
                if (!instance.authLoadPending) return;
                instance.lastAuthLoadAt = Time.realtimeSinceStartup;
                if (instance.authProfileInvalid) { instance.Publish(instance.current); return; }
                instance.authAttemptTimedOut = false;
                if (instance.authRound != null)
                {
                    instance.authRound.deadline = Time.realtimeSinceStartup + 25f;
                    instance.SetStatus(instance.authRound, LeaderboardRoundStatus.Authenticating);
                }
                return;
            }
            Round waiting = instance.authRound;
            instance.authLoadPending = false;
            instance.authProfileInvalid = false;
            instance.authAttemptTimedOut = false;
            instance.preLoginGuestProfile = null;
            instance.authRound = null;
            MarkBrowserAuthLoad(0);
            if (waiting != null)
            {
                waiting.authAttempted = false;
                instance.SetStatus(waiting, LeaderboardRoundStatus.Guest);
            }
        }

        private void TryStart(Round round)
        {
            if (authLoadPending) return;
            if (authProfileInvalid) { SetStatus(round, LeaderboardRoundStatus.Unavailable); return; }
            if (!YG2.isSDKEnabled || YG2.saves == null) return;
            if (!CanUsePlatform || !YG2.infoYG.Leaderboards.enable) { SetStatus(round, LeaderboardRoundStatus.Unavailable); return; }
            if (YG2.player == null || !YG2.player.auth || string.IsNullOrEmpty(YG2.player.id))
            { SetStatus(round, LeaderboardRoundStatus.Guest); return; }
            if (round.requestStarted) return;
            round.requestStarted = true;
            round.profile = YG2.saves;
            round.playerId = YG2.player.id;
            round.xp = Mathf.Max(0, round.profile.exp);
            round.deadline = Time.realtimeSinceStartup + 45f;
            int request = ++nextRequest;
            pending.Add(request, round);
            SetStatus(round, LeaderboardRoundStatus.Loading);
            LeaderboardRoundTransport.Current.Request(gameObject.name, request, round.playerId, round.xp, OnLeaderboardResponse);
        }

        /// <summary>Called only with the actual parsed cloud outcome during a deliberate post-round login.</summary>
        public static bool TryResolveAuthCloudProfile(SavesYG cloudProfile, bool explicitlyMissing, bool broken,
            out SavesYG resolvedProfile, out bool carryGuest)
        {
            resolvedProfile = null; carryGuest = false;
            bool pendingLogin = instance != null && instance.authLoadPending && YG2.player != null && YG2.player.auth;
            LeaderboardAuthProfileChoice choice = LeaderboardProfileLoadRules.Choose(pendingLogin, explicitlyMissing, broken);
            if (choice == LeaderboardAuthProfileChoice.Ignore) return false;
            if (choice == LeaderboardAuthProfileChoice.RejectBrokenCloud)
            {
                NotifyAuthCloudLoadFailed();
                return false;
            }
            carryGuest = choice == LeaderboardAuthProfileChoice.GuestForEmptyAccount;
            resolvedProfile = carryGuest ? instance.preLoginGuestProfile : cloudProfile;
            // Existing account XP does not contain this guest round; its rank cannot earn that round's bonus.
            if (resolvedProfile != null && !carryGuest && instance.authRound != null) instance.authRound.eligible = false;
            return resolvedProfile != null;
        }

        public static bool NotifyAuthCloudLoadFailed()
        {
            if (instance == null || !instance.authLoadPending) return false;
            instance.authProfileInvalid = true;
            // Keep the auth context and original guest object until a real successful cloud reply.
            // A late successful response can still complete safely; a retry gets a new browser generation.
            instance.authAttemptTimedOut = true;
            MarkBrowserAuthLoad(-1);
            if (instance.authRound != null) instance.SetStatus(instance.authRound, LeaderboardRoundStatus.Unavailable);
            return true;
        }

        /// <summary>The storage hook calls this after assigning the resolved account profile, before its generic event.</summary>
        public static void NotifyAuthCloudProfileReady()
        {
            if (instance != null && instance.authLoadPending) instance.CompleteAuthProfileLoad();
        }

        private void CompleteAuthProfileLoad()
        {
            if (YG2.player == null || !YG2.player.auth || YG2.saves == null) return;
            authLoadPending = false;
            authProfileInvalid = false;
            authAttemptTimedOut = false;
            MarkBrowserAuthLoad(0);
            preLoginGuestProfile = null;
            Round waiting = authRound;
            authRound = null;
            if (waiting != null) TryStart(waiting);
        }

        // Public for Unity SendMessage; request identity and profile reference gate every mutation.
        [UnityEngine.Scripting.Preserve]
        public void OnLeaderboardResponse(string json)
        {
            LeaderboardTransportReply reply;
            try { reply = JsonUtility.FromJson<LeaderboardTransportReply>(json); }
            catch { return; }
            if (reply == null || !pending.TryGetValue(reply.requestId, out Round round)) return;
            pending.Remove(reply.requestId);
            if (authLoadPending || authProfileInvalid || !YG2.isSDKEnabled || !ReferenceEquals(YG2.saves, round.profile) || YG2.player == null ||
                !YG2.player.auth || YG2.player.id != round.playerId || reply.playerId != round.playerId ||
                round.profile.exp < round.xp)
            { SetStatus(round, LeaderboardRoundStatus.Unavailable); return; }
            if (reply.status != "confirmed")
            {
                SetStatus(round, reply.status == "guest" ? LeaderboardRoundStatus.Guest :
                    reply.status == "pending" || reply.status == "no_entry" ? LeaderboardRoundStatus.Pending : LeaderboardRoundStatus.Unavailable);
                return;
            }
            if (round.profile.stage2LeaderboardProfiles == null)
                round.profile.stage2LeaderboardProfiles = new List<LeaderboardProfileRecord>();
            LeaderboardProfileRecord record = round.profile.stage2LeaderboardProfiles.Find(item => item != null && item.playerId == round.playerId);
            bool newRecord = record == null;
            if (newRecord) record = new LeaderboardProfileRecord { playerId = round.playerId };
            LeaderboardRankDecision decision = LeaderboardRankRules.Apply(record, round.playerId, round.id,
                round.eligible, round.xp, reply.score, reply.rank, reply.submitted, BonusCoins);
            if (!decision.accepted) { SetStatus(round, LeaderboardRoundStatus.Pending); return; }
            if (newRecord) round.profile.stage2LeaderboardProfiles.Add(record);
            int paid = (int)Math.Min(Math.Max(0L, (long)int.MaxValue - round.profile.goldCoins), decision.bonusCoins);
            round.profile.goldCoins += paid;
            YG2.SaveProgress();
            round.snapshot = new LeaderboardRoundSnapshot { roundId = round.id, status = LeaderboardRoundStatus.Confirmed,
                rank = reply.rank, score = reply.score, previousRank = decision.previousRank, improved = decision.improved,
                firstObservation = decision.firstObservation, bonusCoins = paid, neighborsAvailable = reply.neighborsAvailable,
                neighbors = reply.entries ?? new LeaderboardNeighbor[0],
                isSimulation = LeaderboardRoundTransport.Current.IsSimulation,
                simulationLabel = LeaderboardRoundTransport.Current.SimulationLabel };
            foreach (LeaderboardNeighbor neighbor in round.snapshot.neighbors)
                if (neighbor != null && !neighbor.isSelf && neighbor.rank == reply.rank - 1 && neighbor.score >= reply.score)
                { round.snapshot.targetRank = neighbor.rank; round.snapshot.targetXp = LeaderboardRankRules.XpToPass(reply.score, neighbor.score); break; }
            Publish(round);
        }

        public static bool TryConsumeCelebration(string roundId)
        {
            if (instance == null || !instance.rounds.TryGetValue(roundId, out Round round) ||
                round.celebrationConsumed || !round.snapshot.improved) return false;
            round.celebrationConsumed = true;
            return true;
        }

        private void SetStatus(Round round, LeaderboardRoundStatus status)
        {
            if (round.snapshot.status == status) return;
            round.snapshot.status = status; Publish(round);
        }
        private void Publish(Round round) { if (current == round) Changed?.Invoke(round.snapshot); }

#if UNITY_EDITOR
        /// <summary>Suspends the live service without clearing its records, callbacks or pending requests.</summary>
        internal static IDisposable IsolateEditorSession(ILeaderboardRoundTransport transport)
        {
            return new EditorSession(transport);
        }

        private sealed class EditorSession : IDisposable
        {
            private readonly LeaderboardRoundService previous = instance;
            private readonly ILeaderboardRoundTransport previousTransport = LeaderboardRoundTransport.EditorOverride;
            private readonly int previousBonus = BonusCoins;
            private readonly float startedAt = Time.realtimeSinceStartup;
            private readonly bool wasEnabled;
            private bool disposed;

            internal EditorSession(ILeaderboardRoundTransport transport)
            {
                wasEnabled = previous != null && previous.enabled;
                if (previous != null) previous.enabled = false;
                instance = null;
                LeaderboardRoundTransport.EditorOverride = transport;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                LeaderboardRoundService simulation = instance;
                instance = previous;
                LeaderboardRoundTransport.EditorOverride = previousTransport;
                BonusCoins = previousBonus;
                if (simulation != null && simulation != previous)
                {
                    simulation.enabled = false;
                    Destroy(simulation.gameObject);
                }
                if (previous != null)
                {
                    float suspendedFor = Time.realtimeSinceStartup - startedAt;
                    foreach (Round round in previous.rounds.Values) round.deadline += suspendedFor;
                    previous.lastAuthLoadAt += suspendedFor;
                    previous.enabled = wasEnabled;
                }
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; Changed = null; BonusCoins = LeaderboardRankRules.DefaultBonusCoins; }
    }
}
