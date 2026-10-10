using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;

namespace ProjectLucid.Offline
{
    // Owns only the normal Game leaderboard constructed for this initialization.
    // Readiness remains the actual local-record predicate; no provider is fabricated.
    internal sealed class LocalGameCenterProviderLease : IDisposable
    {
        private LeaderboardManager leaderboard;
        private bool started;
        private bool disposed;

        internal void Start()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalGameCenterProviderLease));
            if (started) throw new InvalidOperationException("Owned Game providers cannot start twice.");
            if (!ProcessManager.IsSystemNull<LeaderboardManager>())
                throw new InvalidOperationException("A foreign leaderboard manager occupies the default registration.");
            started = true;
            try
            {
                // The genuine constructor registers itself. Retain ownership before
                // dispatching the same two actions used by App's original generic API.
                leaderboard = new LeaderboardManager();
                ProcessManager.ProcessSystemAction(leaderboard, SystemAction.AppInitialise);
                ProcessManager.ProcessSystemAction(leaderboard, SystemAction.Initialise);
            }
            catch (Exception original)
            {
                try { Dispose(); }
                catch (Exception cleanup)
                {
                    throw new AggregateException("Local leaderboard initialization and owned cleanup failed.", original, cleanup);
                }
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            LeaderboardManager owned = leaderboard;
            leaderboard = null;
            if (owned == null) return;
            var errors = new List<Exception>();
            try { ProcessManager.UnsubscribeFromAllActions(owned); }
            catch (Exception error) { errors.Add(error); }
            try
            {
                string name = ProcessManager.GetDefaultName<LeaderboardManager>();
                SystemRef reference = ProcessManager.GetSystemRef(name, false);
                if (reference != null && ReferenceEquals(reference.GetSafe(), owned))
                    ProcessManager.UnregisterSystem(name);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Owned local leaderboard cleanup failed.", errors);
        }
    }
}
