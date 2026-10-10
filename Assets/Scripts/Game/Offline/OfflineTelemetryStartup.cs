namespace ProjectLucid.Offline
{
    // Offline policy: no Game analytics settings/session/consent construction.
    // Requires the independently reviewed default LocalAnalytics selection.
    internal static class OfflineTelemetryStartup
    {
        internal static void Initialise() => Hardlight.Analytics.Analytics.Initialise(null);
        internal static void ApplicationFocus(bool focused) => Hardlight.Analytics.Analytics.ApplicationPause(!focused);
        internal static void Shutdown() => Hardlight.Analytics.Analytics.Shutdown();
    }
}
