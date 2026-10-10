namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x0200000b; ten true abstract APIs 0x06000068..71.
    public interface IAnalytics
    {
        bool IsSupported { get; }
        void Initialise(IAnalyticsSettings analyticsSettings);
        void ResetEventIndex();
        void ProcessManualQueue(int category);
        void PushAnalyticsEventToManualQueue(IAnalyticsEvent analyticsEvent, int category);
        IAnalyticsSettings GetAnalyticsSettings();
        int GetEventIndex();
        void ApplicationPause(bool paused);
        void Shutdown();
        void ClearAnalyticsEventsCached();
    }
}
