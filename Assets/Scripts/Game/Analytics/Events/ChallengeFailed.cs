using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200002b; complete 18 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeFailed : EventContext<ChallengeFailed>
    {
        // Original 0x0600014f; exact shipping event-name literal.
        protected override string EventName => "challenge_failed";

        // Original 0x06000150 / 0x06000151.
        public string ChallengeID { get; set; }

        // Original 0x06000152 / 0x06000153.
        public string ChallengeName { get; set; }

        // Original 0x06000154 / 0x06000155.
        public int ChallengeIndex { get; set; }

        // Original 0x06000156 / 0x06000157.
        public int ChallengeSetIndex { get; set; }

        // Original 0x06000158 / 0x06000159.
        public bool IsReplay { get; set; }

        // Original 0x0600015a / 0x0600015b.
        public int Attempts { get; set; }

        // Original 0x0600015c / 0x0600015d.
        public string DreamPowerSlots { get; set; }

        // Original 0x0600015e; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            ChallengeID = string.Empty;
            ChallengeName = string.Empty;
            ChallengeIndex = 0;
            ChallengeSetIndex = 0;
            IsReplay = false;
            Attempts = 0;
            DreamPowerSlots = string.Empty;
        }

        // Original 0x0600015f; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("challenge_id", ChallengeID);
            eventDetail.Add("challenge_name", ChallengeName);
            eventDetail.Add("challenge_index", ChallengeIndex);
            eventDetail.Add("challenge_set_index", ChallengeSetIndex);
            eventDetail.Add("is_replay", IsReplay);
            eventDetail.Add("attempts", Attempts);
            eventDetail.Add("dream_power_slots", DreamPowerSlots);
        }

        // Original 0x06000160; base-only construction, no own field initializers.
        public ChallengeFailed() { }
    }
}
