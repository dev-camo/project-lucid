using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000038; complete 12 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SessionStart : EventContext<SessionStart>
    {
        // Original 0x06000249; exact shipping event-name literal.
        protected override string EventName => "session_start";

        // Original 0x0600024a / 0x0600024b.
        public int SessionNumber { get; set; }

        // Original 0x0600024c / 0x0600024d.
        public string AppInstallDate { get; set; }

        // Original 0x0600024e / 0x0600024f.
        public string CameraMode { get; set; }

        // Original 0x06000250 / 0x06000251.
        public bool CameraInverted { get; set; }

        // Original 0x06000252; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            SessionNumber = 0;
            AppInstallDate = string.Empty;
            CameraMode = string.Empty;
            CameraInverted = false;
        }

        // Original 0x06000253; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("session_number", SessionNumber);
            eventDetail.Add("app_install_date", AppInstallDate);
            eventDetail.Add("camera_mode", CameraMode);
            eventDetail.Add("camera_inverted", CameraInverted);
        }

        // Original 0x06000254; base-only construction, no own field initializers.
        public SessionStart() { }
    }
}
