using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Hardlight;
using Hardlight.Utils;

namespace ProjectLucid.Offline
{
    // Owns one genuine manager and its own completion/rollback callbacks.
    // It neither manufactures provider readiness nor replaces a foreign manager.
    public sealed class LocalAchievementManagerLease : IDisposable
    {
        public AchievementsManager Manager { get; }
        private readonly Action complete;
        private readonly Action rollback;
        private bool started;
        private bool disposed;
        public bool IsReady => !disposed && LocalAchievementRuntime.IsReady(Manager)
            && ReferenceEquals(ProcessManager.GetSystemRef(ProcessManager.GetDefaultName<AchievementsManager>(), false).GetSafe(), Manager);

        private LocalAchievementManagerLease(Action complete, Action rollback)
        {
            this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
            this.rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
            Manager = new AchievementsManager();
        }

        public static LocalAchievementManagerLease Create(HLPropertyStore store,
            IEnumerable<string> identifiers, Func<bool> trackingAllowed, Action complete, Action rollback)
        {
            // The untyped original registry predicate also rejects an incompatible
            // foreign owner under the manager's default name.
            if (!ProcessManager.IsSystemNull<AchievementsManager>())
                throw new InvalidOperationException("A foreign achievement manager occupies the default registration.");
            var lease = new LocalAchievementManagerLease(complete, rollback);
            try
            {
                ProcessManager.RegisterSystem(lease.Manager, null, false, false);
                LocalAchievementRuntime.Configure(lease.Manager, store, identifiers, trackingAllowed);
                lease.Manager.OnPlatformSyncComplete += complete;
                return lease;
            }
            catch (Exception error)
            {
                lease.ThrowAfterCleanup(error);
                throw;
            }
        }

        public void Start()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalAchievementManagerLease));
            if (started) throw new InvalidOperationException("An owned manager can initialize only once.");
            started = true;
            try
            {
                ProcessManager.ProcessSystemAction(Manager, SystemAction.AppInitialise);
                ProcessManager.ProcessSystemAction(Manager, SystemAction.Initialise);
                if (!IsReady) throw new InvalidOperationException("Genuine local achievement initialization did not complete.");
            }
            catch (Exception error)
            {
                ThrowAfterCleanup(error);
                throw;
            }
        }

        private List<Exception> Cleanup()
        {
            var errors = new List<Exception>();
            if (disposed) return errors;
            disposed = true;
            // Actual game-owned evaluator/listener removal happens while the
            // genuine context still retains its exact evaluator handles.
            try { rollback(); }
            catch (Exception error) { errors.Add(error); }
            Manager.OnPlatformSyncComplete -= complete;
            try { ProcessManager.ProcessSystemAction(Manager, SystemAction.Shutdown); }
            catch (Exception error) { errors.Add(error); }
            // Abort remains necessary if a shutdown subscriber throws before the
            // normal hook runs. It revokes owned context readiness and entries.
            try { LocalAchievementRuntime.AbortManager(Manager); }
            catch (Exception error) { errors.Add(error); }
            ProcessManager.UnsubscribeFromAllActions(Manager);
            try
            {
                string name = ProcessManager.GetDefaultName<AchievementsManager>();
                if (ReferenceEquals(ProcessManager.GetSystemRef(name, false).GetSafe(), Manager))
                    ProcessManager.UnregisterSystem(name);
            }
            catch (Exception error) { errors.Add(error); }
            return errors;
        }

        private void ThrowAfterCleanup(Exception original)
        {
            List<Exception> errors = Cleanup();
            if (errors.Count != 0)
            {
                errors.Insert(0, original);
                throw new AggregateException("Owned achievement initialization and cleanup failed.", errors);
            }
            ExceptionDispatchInfo.Capture(original).Throw();
        }

        public void Dispose()
        {
            List<Exception> errors = Cleanup();
            if (errors.Count != 0) throw new AggregateException("Owned achievement cleanup failed.", errors);
        }
    }
}
