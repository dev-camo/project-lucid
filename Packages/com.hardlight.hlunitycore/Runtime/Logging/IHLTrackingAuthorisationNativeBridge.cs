namespace Hardlight
{
    // Original three abstract contracts 0x06000ef2..f4; no body credit.
    public interface IHLTrackingAuthorisationNativeBridge
    {
        bool CanDeviceRequestTrackingAuthorisation();
        void RequestTrackingAuthorisation();
        int RequestTrackingAuthorisationStatus();
    }
}
