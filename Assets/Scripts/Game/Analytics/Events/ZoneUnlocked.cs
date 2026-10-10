using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200003b; complete 12 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ZoneUnlocked : EventContext<ZoneUnlocked>
    {
        // Original 0x06000273; exact shipping event-name literal.
        protected override string EventName => "zone_unlocked";

        // Original 0x06000274 / 0x06000275.
        public string ZoneName { get; set; }

        // Original 0x06000276 / 0x06000277.
        public string ZoneIndex { get; set; }

        // Original 0x06000278 / 0x06000279.
        public string Act { get; set; }

        // Original 0x0600027a / 0x0600027b.
        public long TotalPlayTime { get; set; }

        // Original 0x0600027c; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            ZoneName = string.Empty;
            ZoneIndex = string.Empty;
            Act = string.Empty;
            TotalPlayTime = 0L;
        }

        // Original 0x0600027d; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("zone_name", ZoneName);
            eventDetail.Add("zone_index", ZoneIndex);
            eventDetail.Add("act", Act);
            eventDetail.Add("total_play_time", TotalPlayTime);
        }

        // Original 0x0600027e; base-only construction, no own field initializers.
        public ZoneUnlocked() { }
    }
}
