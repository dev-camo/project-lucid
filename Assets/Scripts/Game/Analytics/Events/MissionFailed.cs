using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000032; complete 32 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionFailed : EventContext<MissionFailed>
    {
        // Original 0x060001cb; exact shipping event-name literal.
        protected override string EventName => "mission_failed";

        // Original 0x060001cc / 0x060001cd.
        public string ZoneName { get; set; }

        // Original 0x060001ce / 0x060001cf.
        public string ZoneIndex { get; set; }

        // Original 0x060001d0 / 0x060001d1.
        public string Act { get; set; }

        // Original 0x060001d2 / 0x060001d3.
        public string MissionName { get; set; }

        // Original 0x060001d4 / 0x060001d5.
        public string MissionIndex { get; set; }

        // Original 0x060001d6 / 0x060001d7.
        public string Character { get; set; }

        // Original 0x060001d8 / 0x060001d9.
        public int? DeathCount { get; set; }

        // Original 0x060001da / 0x060001db.
        public long? MissionTime { get; set; }

        // Original 0x060001dc / 0x060001dd.
        public int? RsrTotal { get; set; }

        // Original 0x060001de / 0x060001df.
        public int? BlueCoinsTotal { get; set; }

        // Original 0x060001e0 / 0x060001e1.
        public bool IsReplay { get; set; }

        // Original 0x060001e2 / 0x060001e3.
        public string MissionType { get; set; }

        // Original 0x060001e4 / 0x060001e5.
        public int Attempts { get; set; }

        // Original 0x060001e6 / 0x060001e7.
        public string DreamPowerSlots { get; set; }

        // Original 0x060001e8; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            ZoneName = string.Empty;
            ZoneIndex = string.Empty;
            Act = string.Empty;
            MissionName = string.Empty;
            MissionIndex = string.Empty;
            Character = string.Empty;
            DeathCount = null;
            MissionTime = null;
            RsrTotal = null;
            BlueCoinsTotal = null;
            IsReplay = false;
            MissionType = string.Empty;
            Attempts = 0;
            DreamPowerSlots = string.Empty;
        }

        // Original 0x060001e9; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("zone_name", ZoneName);
            eventDetail.Add("zone_index", ZoneIndex);
            eventDetail.Add("act", Act);
            eventDetail.Add("mission_name", MissionName);
            eventDetail.Add("mission_index", MissionIndex);
            eventDetail.Add("character", Character);
            if (DeathCount.HasValue)
                eventDetail.Add("death_count", DeathCount.Value);
            if (MissionTime.HasValue)
                eventDetail.Add("mission_time", MissionTime.Value);
            if (RsrTotal.HasValue)
                eventDetail.Add("rsr_total", RsrTotal.Value);
            if (BlueCoinsTotal.HasValue)
                eventDetail.Add("blue_coins_total", BlueCoinsTotal.Value);
            eventDetail.Add("is_replay", IsReplay);
            eventDetail.Add("mission_type", MissionType);
            eventDetail.Add("attempts", Attempts);
            eventDetail.Add("dream_power_slots", DreamPowerSlots);
        }

        // Original 0x060001ea; base-only construction, no own field initializers.
        public MissionFailed() { }
    }
}
