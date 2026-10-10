using Hardlight.JSON;

namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x0200000c; four true abstract APIs 0x06000072..75.
    public interface IAnalyticsEvent
    {
        bool IsLateDataReady { get; }
        void SetLateData();
        void ReleaseEvent();
        JSONHashtable CreateData();
    }
}
