namespace Hardlight
{
    // HLCrashReport.Runtime 0200000d. The supplied macOS ARM64 and x86_64 player
    // contains genuine empty implementations for all twelve operations. These
    // preserve that release; unavailable mobile/native implementations are unresolved.
    public sealed class HLCrashReportNativeBridge : IHLCrashReportNativeBridge
    {
        // 0600003b..42: original signatures and implicit interface implementation.
        public void Initialise(HLCrashReport componentOwner) { }
        public void SetUserID(string id) { }
        public void LogError(string errorType, string errorMessage, int errorCode, string stackTrace) { }
        public void Breadcrumb(string message) { }
        public void CustomKey(string key, string value) { }
        public void CustomKey(string key, int value) { }
        public void CustomKey(string key, float value) { }
        public void CustomKey(string key, bool value) { }
        // 06000043..46: concrete-owner order differs from the interface's order.
        public void SetAppKey(string key) { }
        public void GenerateTestCrashReport() { }
        public void StartService() { }
        public void StopService() { }
        // 06000047: ordinary Object base constructor.
        public HLCrashReportNativeBridge() { }
    }
}
