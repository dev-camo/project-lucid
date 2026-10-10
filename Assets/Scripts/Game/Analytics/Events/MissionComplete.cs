using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000031; complete 34 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionComplete : EventContext<MissionComplete>
    {
        // Original 0x060001a9; exact shipping event-name literal.
        protected override string EventName => "mission_complete";

        // Original 0x060001aa / 0x060001ab.
        public string ZoneName { get; set; }

        // Original 0x060001ac / 0x060001ad.
        public string ZoneIndex { get; set; }

        // Original 0x060001ae / 0x060001af.
        public string Act { get; set; }

        // Original 0x060001b0 / 0x060001b1.
        public string MissionName { get; set; }

        // Original 0x060001b2 / 0x060001b3.
        public string MissionIndex { get; set; }

        // Original 0x060001b4 / 0x060001b5.
        public string Character { get; set; }

        // Original 0x060001b6 / 0x060001b7.
        public int? DeathCount { get; set; }

        // Original 0x060001b8 / 0x060001b9.
        public long? MissionTime { get; set; }

        // Original 0x060001ba / 0x060001bb.
        public int? RsrTotal { get; set; }

        // Original 0x060001bc / 0x060001bd.
        public int? BlueCoinsTotal { get; set; }

        // Original 0x060001be / 0x060001bf.
        public string MissionType { get; set; }

        // Original 0x060001c0 / 0x060001c1.
        public bool IsReplay { get; set; }

        // Original 0x060001c2 / 0x060001c3.
        public int Attempts { get; set; }

        // Original 0x060001c4 / 0x060001c5.
        public int? MusicNotesTotal { get; set; }

        // Original 0x060001c6 / 0x060001c7.
        public string DreamPowerSlots { get; set; }

        // Original 0x060001c8; genuine base initialization precedes all resets.
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
            MissionType = string.Empty;
            IsReplay = false;
            Attempts = 0;
            MusicNotesTotal = null;
            DreamPowerSlots = string.Empty;
        }

        // Original 0x060001c9; preserve authored JSON insertion order.
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
            eventDetail.Add("mission_type", MissionType);
            eventDetail.Add("is_replay", IsReplay);
            eventDetail.Add("attempts", Attempts);
            if (MusicNotesTotal.HasValue)
                eventDetail.Add("music_notes_total", MusicNotesTotal.Value);
            eventDetail.Add("dream_power_slots", DreamPowerSlots);
        }

        // Original 0x060001ca; base-only construction, no own field initializers.
        public MissionComplete() { }
    }
}
