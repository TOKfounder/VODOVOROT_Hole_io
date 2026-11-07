using System;
using System.Runtime.InteropServices;

namespace Vodovorot.Stage3
{
    /// <summary>The rank policy receives the same acknowledged reply from either transport.</summary>
    public interface ILeaderboardRoundTransport
    {
        bool IsAvailable { get; }
        bool IsSimulation { get; }
        string SimulationLabel { get; }
        void Request(string receiver, int requestId, string playerId, int xp, Action<string> reply);
    }

    [Serializable]
    public sealed class LeaderboardTransportReply
    {
        public int requestId;
        public string status;
        public string playerId;
        public int rank;
        public int score;
        public bool submitted;
        public bool neighborsAvailable;
        public Vodovorot.Stage2.LeaderboardNeighbor[] entries;
    }

    public static class LeaderboardRoundTransport
    {
        private static readonly ILeaderboardRoundTransport web = new WebGlTransport();
        public static ILeaderboardRoundTransport Current
        {
            get
            {
#if UNITY_EDITOR
                if (EditorOverride != null) return EditorOverride;
#endif
                return web;
            }
        }

#if UNITY_EDITOR
        // Set only by the explicit, in-memory Editor simulation session.
        internal static ILeaderboardRoundTransport EditorOverride;
#endif

        private sealed class WebGlTransport : ILeaderboardRoundTransport
        {
            public bool IsAvailable
            {
                get
                {
#if UNITY_WEBGL && !UNITY_EDITOR && YandexGamesPlatform_yg
                    return true;
#else
                    return false;
#endif
                }
            }
            public bool IsSimulation => false;
            public string SimulationLabel => null;
#if UNITY_WEBGL && !UNITY_EDITOR && YandexGamesPlatform_yg
            [DllImport("__Internal")] private static extern void LeaderboardStage2_Request(string objectName, int requestId, string playerId, int xp);
#endif
            public void Request(string receiver, int requestId, string playerId, int xp, Action<string> reply)
            {
#if UNITY_WEBGL && !UNITY_EDITOR && YandexGamesPlatform_yg
                // The existing bridge still owns the production write/read transaction and callback.
                LeaderboardStage2_Request(receiver, requestId, playerId, xp);
#endif
            }
        }
    }
}
