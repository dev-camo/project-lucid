using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000030; complete 16 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class JukeboxTrackEnd : EventContext<JukeboxTrackEnd>
    {
        // Original 0x06000199; exact shipping event-name literal.
        protected override string EventName => "jukebox_track_end";

        // Original 0x0600019a / 0x0600019b.
        public int SongIDX { get; set; }

        // Original 0x0600019c / 0x0600019d.
        public string MissionRef { get; set; }

        // Original 0x0600019e / 0x0600019f.
        public int SongLength { get; set; }

        // Original 0x060001a0 / 0x060001a1.
        public int ListenTime { get; set; }

        // Original 0x060001a2 / 0x060001a3.
        public bool IdleStart { get; set; }

        // Original 0x060001a4 / 0x060001a5.
        public string Reason { get; set; }

        // Original 0x060001a6; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            SongIDX = 0;
            MissionRef = string.Empty;
            SongLength = 0;
            ListenTime = 0;
            IdleStart = false;
            Reason = string.Empty;
        }

        // Original 0x060001a7; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("song_idx", SongIDX);
            eventDetail.Add("mission_ref", MissionRef);
            eventDetail.Add("song_length", SongLength);
            eventDetail.Add("listen_time", ListenTime);
            eventDetail.Add("idle_start", IdleStart);
            eventDetail.Add("reason", Reason);
        }

        // Original 0x060001a8; base-only construction, no own field initializers.
        public JukeboxTrackEnd() { }
    }
}
