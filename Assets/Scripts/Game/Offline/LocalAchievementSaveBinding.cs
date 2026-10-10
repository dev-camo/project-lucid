using System;
using HardlightProject;

namespace ProjectLucid.Offline
{
    // Owns a genuine listener. Removal requested inside a save callback is
    // delayed until a caller boundary outside the original SaveManager foreach.
    internal sealed class LocalAchievementSaveBinding : ISaveGameListener, IDisposable
    {
        private readonly SaveManager manager;
        private readonly Action failed;
        private Action bind;
        private bool registered;
        private bool dispatching;
        private bool removalRequested;
        public bool IsOpen { get; private set; }
        internal LocalAchievementSaveBinding(SaveManager manager, Action bind, Action failed)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.bind = bind ?? throw new ArgumentNullException(nameof(bind));
            this.failed = failed ?? throw new ArgumentNullException(nameof(failed));
        }
        internal void Start()
        {
            if (registered || removalRequested) throw new InvalidOperationException("Save listener cannot start twice.");
            registered = true;
            // AddListener may invoke an already-open save immediately. Its
            // call returns before this finally drains any requested removal.
            try { manager.AddListener(this, true); }
            finally { DrainPendingRemoval(); }
        }
        public void OnSaveGameOpen(SaveDataGame save)
        {
            if (!registered || removalRequested) return;
            dispatching = true;
            try
            {
                if (save == null || !ReferenceEquals(manager.CurrentSave, save))
                    throw new InvalidOperationException("Save-open callback does not match the genuine current save.");
                IsOpen = true;
                if (bind == null) return;
                Action callback = bind;
                bind = null;
                callback();
            }
            catch (Exception original)
            {
                IsOpen = false;
                try { failed(); }
                catch (Exception cleanup) { throw new AggregateException("Save-open binding and owned cleanup failed.", original, cleanup); }
                throw;
            }
            finally { dispatching = false; }
        }
        public void OnSaveGameClose(SaveDataGame save) { IsOpen = false; }
        internal void DrainPendingRemoval()
        {
            if (dispatching) throw new InvalidOperationException("Cannot remove a listener during save callback dispatch.");
            if (removalRequested && registered) { manager.RemoveListener(this); registered = false; }
        }
        public void Dispose()
        {
            IsOpen = false;
            bind = null;
            removalRequested = true;
            if (!dispatching) DrainPendingRemoval();
        }
    }
}
