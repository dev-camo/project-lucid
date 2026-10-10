using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000033; complete 32 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionQuit : EventContext<MissionQuit>
    {
        // Original 0x060001eb; exact shipping event-name literal.
        protected override string EventName => "mission_quit";

        // Original 0x060001ec / 0x060001ed.
        public string ZoneName { get; set; }

        // Original 0x060001ee / 0x060001ef.
        public string ZoneIndex { get; set; }

        // Original 0x060001f0 / 0x060001f1.
        public string Act { get; set; }

        // Original 0x060001f2 / 0x060001f3.
        public string MissionName { get; set; }

        // Original 0x060001f4 / 0x060001f5.
        public string MissionIndex { get; set; }

        // Original 0x060001f6 / 0x060001f7.
        public string Character { get; set; }

        // Original 0x060001f8 / 0x060001f9.
        public int? DeathCount { get; set; }

        // Original 0x060001fa / 0x060001fb.
        public long? MissionTime { get; set; }

        // Original 0x060001fc / 0x060001fd.
        public int? RsrTotal { get; set; }

        // Original 0x060001fe / 0x060001ff.
        public int? BlueCoinsTotal { get; set; }

        // Original 0x06000200 / 0x06000201.
        public bool IsReplay { get; set; }

        // Original 0x06000202 / 0x06000203.
        public string MissionType { get; set; }

        // Original 0x06000204 / 0x06000205.
        public int Attempts { get; set; }

        // Original 0x06000206 / 0x06000207.
        public string DreamPowerSlots { get; set; }

        // Original 0x06000208; genuine base initialization precedes all resets.
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

        // Original 0x06000209; preserve authored JSON insertion order.
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

        // Original 0x0600020a; base-only construction, no own field initializers.
        public MissionQuit() { }
    }
}
