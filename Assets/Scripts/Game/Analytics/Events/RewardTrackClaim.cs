using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000036; complete 10 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RewardTrackClaim : EventContext<RewardTrackClaim>
    {
        // Original 0x06000237; exact shipping event-name literal.
        protected override string EventName => "reward_track_claim";

        // Original 0x06000238 / 0x06000239.
        public int RewardIDX { get; set; }

        // Original 0x0600023a / 0x0600023b.
        public int XpThreshold { get; set; }

        // Original 0x0600023c / 0x0600023d.
        public string ItemID { get; set; }

        // Original 0x0600023e; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            RewardIDX = 0;
            XpThreshold = 0;
            ItemID = string.Empty;
        }

        // Original 0x0600023f; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("reward_idx", RewardIDX);
            eventDetail.Add("xp_threshold", XpThreshold);
            eventDetail.Add("item_id", ItemID);
        }

        // Original 0x06000240; base-only construction, no own field initializers.
        public RewardTrackClaim() { }
    }
}
