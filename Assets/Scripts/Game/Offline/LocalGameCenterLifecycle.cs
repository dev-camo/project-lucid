using System;
using Hardlight;
using HardlightProject;

namespace ProjectLucid.Offline
{
    internal sealed class LocalGameCenterLifecycle
    {
        private Action complete;
        private bool stopped;
        internal void Begin(Action callback)
        {
            if (stopped) throw new InvalidOperationException("Local account lifecycle is stopped.");
            if (complete != null) return;
            complete = callback ?? throw new ArgumentNullException(nameof(callback));
            Poll();
        }
        internal void Poll()
        {
            if (stopped || complete == null) return;
            // GetSystemSafe(false) does not create or fabricate missing providers.
            App app = ProcessManager.GetSystemSafe<App>(null, false);
            DataManager data = ProcessManager.GetSystemSafe<DataManager>(null, false);
            HLPropertyStore store = ProcessManager.GetSystemSafe<HLPropertyStore>(null, false);
            if (app == null || data == null || store == null || !store.IsLoaded || !store.CanSave) return;
            if (store.GetSaveIdentifier() != LocalAchievementRuntime.SaveIdentifier) return;
            if (data.AchievementDefinitions.Count == 0 || data.MissionDefinitionLevelLookup.Count == 0) return;
            LocalPersonalRecords.Initialise(data, store);
            Action callback = complete;
            complete = null;
            // Exceptions stop publication: the original GameCenter caller sets its
            // SystemIsReady only after both genuine provider initialization calls return.
            try { callback(); }
            catch
            {
                LocalPersonalRecords.Shutdown();
                LocalProfilePresentation.UnbindAchievements();
                stopped = true;
                throw;
            }
        }
        internal void Stop()
        {
            stopped = true;
            complete = null;
            LocalProfilePresentation.UnbindAchievements();
        }
    }
}
