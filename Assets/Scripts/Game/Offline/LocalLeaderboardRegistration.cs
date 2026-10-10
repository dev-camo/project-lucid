using System;
using Hardlight;
using HardlightProject;

namespace ProjectLucid.Offline
{
    // Default-only constructor rollback; the original constructor remains whole.
    internal static class LocalLeaderboardRegistration
    {
        internal static void Initialise(LeaderboardManager owner, Action initialise, Action<object> shutdown)
        {
            try
            {
                ProcessManager.RegisterSystem(owner, null, false, false);
                initialise();
                ProcessManager.SubscribeToAction(owner, SystemAction.Shutdown, shutdown);
            }
            catch (Exception original)
            {
                ProcessManager.UnsubscribeFromAllActions(owner);
                try
                {
                    string name = ProcessManager.GetDefaultName<LeaderboardManager>();
                    if (ReferenceEquals(ProcessManager.GetSystemRef(name, false).GetSafe(), owner))
                        ProcessManager.UnregisterSystem(name);
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Local leaderboard constructor and owned rollback failed.", original, cleanup);
                }
                throw;
            }
        }
    }
}
