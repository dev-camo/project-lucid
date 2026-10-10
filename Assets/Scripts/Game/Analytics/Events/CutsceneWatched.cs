using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200002e; complete 10 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CutsceneWatched : EventContext<CutsceneWatched>
    {
        // Original 0x06000185; exact shipping event-name literal.
        protected override string EventName => "cutscene_watched";

        // Original 0x06000186 / 0x06000187.
        public string CutsceneName { get; set; }

        // Original 0x06000188 / 0x06000189.
        public bool CutsceneSkipped { get; set; }

        // Original 0x0600018a / 0x0600018b.
        public float WatchTime { get; set; }

        // Original 0x0600018c; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            CutsceneName = string.Empty;
            CutsceneSkipped = false;
            WatchTime = 0f;
        }

        // Original 0x0600018d; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("cutscene_name", CutsceneName);
            eventDetail.Add("cutscene_skipped", CutsceneSkipped);
            eventDetail.Add("watch_time", WatchTime);
        }

        // Original 0x0600018e; base-only construction, no own field initializers.
        public CutsceneWatched() { }
    }
}
