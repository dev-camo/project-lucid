using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000034; complete 30 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionStart : EventContext<MissionStart>
    {
        // Original 0x0600020b; exact shipping event-name literal.
        protected override string EventName => "mission_start";

        // Original 0x0600020c / 0x0600020d.
        public string ZoneName { get; set; }

        // Original 0x0600020e / 0x0600020f.
        public string ZoneIndex { get; set; }

        // Original 0x06000210 / 0x06000211.
        public string Act { get; set; }

        // Original 0x06000212 / 0x06000213.
        public string MissionName { get; set; }

        // Original 0x06000214 / 0x06000215.
        public string MissionIndex { get; set; }

        // Original 0x06000216 / 0x06000217.
        public string Character { get; set; }

        // Original 0x06000218 / 0x06000219.
        public int RsrTotal { get; set; }

        // Original 0x0600021a / 0x0600021b.
        public int BlueCoinsTotal { get; set; }

        // Original 0x0600021c / 0x0600021d.
        public bool IsReplay { get; set; }

        // Original 0x0600021e / 0x0600021f.
        public string MissionType { get; set; }

        // Original 0x06000220 / 0x06000221.
        public int Attempts { get; set; }

        // Original 0x06000222 / 0x06000223.
        public int? MusicNotesTotal { get; set; }

        // Original 0x06000224 / 0x06000225.
        public string DreamPowerSlots { get; set; }

        // Original 0x06000226; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            ZoneName = string.Empty;
            ZoneIndex = string.Empty;
            Act = string.Empty;
            MissionName = string.Empty;
            MissionIndex = string.Empty;
            Character = string.Empty;
            RsrTotal = 0;
            BlueCoinsTotal = 0;
            IsReplay = false;
            MissionType = string.Empty;
            Attempts = 0;
            MusicNotesTotal = null;
            DreamPowerSlots = string.Empty;
        }

        // Original 0x06000227; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("zone_name", ZoneName);
            eventDetail.Add("zone_index", ZoneIndex);
            eventDetail.Add("act", Act);
            eventDetail.Add("mission_name", MissionName);
            eventDetail.Add("mission_index", MissionIndex);
            eventDetail.Add("character", Character);
            eventDetail.Add("rsr_total", RsrTotal);
            eventDetail.Add("blue_coins_total", BlueCoinsTotal);
            eventDetail.Add("is_replay", IsReplay);
            eventDetail.Add("mission_type", MissionType);
            eventDetail.Add("attempts", Attempts);
            if (MusicNotesTotal.HasValue)
                eventDetail.Add("music_notes_total", MusicNotesTotal.Value);
            eventDetail.Add("dream_power_slots", DreamPowerSlots);
        }

        // Original 0x06000228; base-only construction, no own field initializers.
        public MissionStart() { }
    }
}
