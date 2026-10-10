namespace Hardlight
{
    // HLCrashReport.Runtime 0200000e/06000048..53. Keep original declaration order.
    public interface IHLCrashReportNativeBridge
    {
        void Initialise(HLCrashReport componentOwner);
        void SetUserID(string id);
        void LogError(string errorType, string errorMessage, int errorCode, string stackTrace);
        void Breadcrumb(string message);
        void CustomKey(string key, string value);
        void CustomKey(string key, int value);
        void CustomKey(string key, float value);
        void CustomKey(string key, bool value);
        void StartService();
        void StopService();
        void SetAppKey(string key);
        void GenerateTestCrashReport();
    }
}
