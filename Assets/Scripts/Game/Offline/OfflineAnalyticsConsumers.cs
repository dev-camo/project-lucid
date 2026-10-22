using Hardlight;
using Hardlight.Analytics;
using HardlightProject;

namespace ProjectLucid.Offline
{
    // Offline policy for the consumers of the analytics services that the default (offline) App.InitialiseAnalytics
    // never registers (see OfflineTelemetryStartup). The consumers keep their original bodies after a runtime guard
    // on OfflineAdapters.Active; the active adapters call these. Hook points (divergences, docs/offline-hooks.md):
    //   H-066 ApplicationTransitionCanTriggerAnalyticsConsentScreen.DoUpdate -> CanShowConsentScreenDuringBoot
    //   H-067 UIContainerSettingsLegal.OnEnable                              -> CanShowConsentScreenButtonOnSettings
    //   H-068 SaveGameUIActions.Action_ResetAnalyticsData                    -> ReloadAnalyticsConsumers
    //   H-069 NotificationManager.CanRequestNotification                     -> SessionNumber
    // UIContainerAnalyticsConsent.Start also requires the consent manager, but is unreachable offline: the boot
    // consent flow ends at Finished and the legal page never offers the consent button, so nothing opens it.
    // Each lookup is a non-registering GetSystemSafe: it neither fakes a service nor leaves an empty row behind, and a
    // service that somebody did register is still honoured exactly as the original body does.
    internal static class OfflineAnalyticsConsumers
    {
        // Offline there is no consent manager, so no consent screen is shown during boot.
        internal static bool CanShowConsentScreenDuringBoot()
        {
            AnalyticsConsentManager manager = ProcessManager.GetSystemSafe<AnalyticsConsentManager>(null, false);
            return manager != null && manager.CanShowConsentScreenDuringBoot();
        }

        // Offline the legal page offers no consent button.
        internal static bool CanShowConsentScreenButtonOnSettings()
        {
            AnalyticsConsentManager manager = ProcessManager.GetSystemSafe<AnalyticsConsentManager>(null, false);
            return manager != null && manager.CanShowConsentScreenButtonOnSettings();
        }

        // Offline there are no consumers to reload from the fresh analytics save; the consent manager precedes the
        // session manager as in the original.
        internal static void ReloadAnalyticsConsumers(SaveManager saveManager)
        {
            ProcessManager.GetSystemSafe<AnalyticsConsentManager>(null, false)?.LoadFromSaveData(saveManager);
            ProcessManager.GetSystemSafe<AnalyticsSessionManager>(null, false)?.LoadFromSaveData(saveManager);
        }

        // The session number the original compared (CanRequestNotification 0x5C7E80, compare at 0x5c7f27). The original read
        // AnalyticsSessionManager.SessionNumber, which that manager loads from SaveDataAnalytics.SessionNumber
        // (SetSessionVariablesFromSaveAnalytics) and only the manager ever writes back (SaveAnalyticsSessionData). Offline the
        // manager is not registered, so this reads the same persisted value from the save and the original comparison and its
        // second-session requirement stay unchanged. Nothing offline advances the stored number, so a fresh offline save
        // keeps it at 0 and no request is offered, the original's own answer before its second session; a save that already
        // holds a count of two or more passes. A registered manager is still consulted, and a save with no analytics data
        // yields 0, the manager's value before it has loaded.
        internal static int SessionNumber(SaveManager saveManager)
        {
            AnalyticsSessionManager manager = ProcessManager.GetSystemSafe<AnalyticsSessionManager>(null, false);
            if (manager != null) return manager.SessionNumber;
            return saveManager.HasSaveDataAnalytics() ? saveManager.GetSaveDataAnalytics().SessionNumber : 0;
        }
    }
}
