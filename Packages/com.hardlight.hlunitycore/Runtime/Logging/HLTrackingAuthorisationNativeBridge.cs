namespace Hardlight
{
    public class HLTrackingAuthorisationNativeBridge
    {
        // 0x06000eec; original retail ARM64 0x1b201fc ignores the owner and
        // constructs the genuine original Stub. No replacement type is added.
        public static IHLTrackingAuthorisationNativeBridge Initialise(HLUnityCore componentOwner) => new HLTrackingAuthorisationNativeBridgeStub();

        // Original Object base constructor 0x06000eed, ARM64 0x1b2025c.
        public HLTrackingAuthorisationNativeBridge() { }
    }

    public class HLTrackingAuthorisationNativeBridgeStub : IHLTrackingAuthorisationNativeBridge
    {
        // Original retail 0x06000eee, ARM64 0x1b20264.
        public bool CanDeviceRequestTrackingAuthorisation() => false;

        // Original retail 0x06000eef, ARM64 0x1b2026c.
        public void RequestTrackingAuthorisation() => HLOutput.LogError("This device does not support tracking authorisation requests");

        // Original retail 0x06000ef0, ARM64 0x1b20308. Native result is -1.
        public int RequestTrackingAuthorisationStatus() => -1;

        // Original Object base constructor 0x06000ef1, ARM64 0x1b20254.
        public HLTrackingAuthorisationNativeBridgeStub() { }
    }
}
