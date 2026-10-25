namespace Vodovorot.Stage2
{
    public enum LeaderboardAuthProfileChoice { Ignore, LoadedAccount, GuestForEmptyAccount, RejectBrokenCloud }

    public static class LeaderboardProfileLoadRules
    {
        public static LeaderboardAuthProfileChoice Choose(bool deliberateLoginPending, bool cloudExplicitlyMissing, bool cloudBroken)
        {
            if (!deliberateLoginPending) return LeaderboardAuthProfileChoice.Ignore;
            if (cloudBroken) return LeaderboardAuthProfileChoice.RejectBrokenCloud;
            return cloudExplicitlyMissing ? LeaderboardAuthProfileChoice.GuestForEmptyAccount : LeaderboardAuthProfileChoice.LoadedAccount;
        }
    }
}
