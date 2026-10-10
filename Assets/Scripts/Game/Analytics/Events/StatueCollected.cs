using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200003a; complete 20 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class StatueCollected : EventContext<StatueCollected>
    {
        // Original 0x0600025f; exact shipping event-name literal.
        protected override string EventName => "statue_collected";

        // Original 0x06000260 / 0x06000261.
        public string StatueName { get; set; }

        // Original 0x06000262 / 0x06000263.
        public string Source { get; set; }

        // Original 0x06000264 / 0x06000265.
        public string ZoneName { get; set; }

        // Original 0x06000266 / 0x06000267.
        public string ZoneIndex { get; set; }

        // Original 0x06000268 / 0x06000269.
        public string Act { get; set; }

        // Original 0x0600026a / 0x0600026b.
        public string MissionName { get; set; }

        // Original 0x0600026c / 0x0600026d.
        public string MissionIndex { get; set; }

        // Original 0x0600026e / 0x0600026f.
        public int? XpThreshold { get; set; }

        // Original 0x06000270; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            StatueName = string.Empty;
            Source = string.Empty;
            ZoneName = string.Empty;
            ZoneIndex = string.Empty;
            Act = string.Empty;
            MissionName = string.Empty;
            MissionIndex = string.Empty;
            XpThreshold = null;
        }

        // Original 0x06000271; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("statue_name", StatueName);
            eventDetail.Add("source", Source);
            eventDetail.Add("zone_name", ZoneName);
            eventDetail.Add("zone_index", ZoneIndex);
            eventDetail.Add("act", Act);
            eventDetail.Add("mission_name", MissionName);
            eventDetail.Add("mission_index", MissionIndex);
            if (XpThreshold.HasValue)
                eventDetail.Add("xp_threshold", XpThreshold.Value);
        }

        // Original 0x06000272; base-only construction, no own field initializers.
        public StatueCollected() { }
    }
}
