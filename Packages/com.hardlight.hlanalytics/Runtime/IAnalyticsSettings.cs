using System;

namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x0200000d; nine true abstract APIs 0x06000076..7e.
    public interface IAnalyticsSettings
    {
        string GameID { get; }
        string Environment { get; }
        Uri ServerBaseURL { get; }
        int MaxQueueLength { get; }
        int MaxSendAttempts { get; }
        bool IsAnalyticsEventsSendingAllowed { get; }
        bool IsAnalyticsEventsCollectionAllowed { get; }
        bool PersistentEventIndex { get; }
        bool IsAnalyticsEventToManualQueueCollectionEnabled(int category);
    }
}
