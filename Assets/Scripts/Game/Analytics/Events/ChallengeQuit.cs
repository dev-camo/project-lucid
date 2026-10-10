using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200002c; complete 18 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ChallengeQuit : EventContext<ChallengeQuit>
    {
        // Original 0x06000161; exact shipping event-name literal.
        protected override string EventName => "challenge_quit";

        // Original 0x06000162 / 0x06000163.
        public string ChallengeID { get; set; }

        // Original 0x06000164 / 0x06000165.
        public string ChallengeName { get; set; }

        // Original 0x06000166 / 0x06000167.
        public int ChallengeIndex { get; set; }

        // Original 0x06000168 / 0x06000169.
        public int ChallengeSetIndex { get; set; }

        // Original 0x0600016a / 0x0600016b.
        public bool IsReplay { get; set; }

        // Original 0x0600016c / 0x0600016d.
        public int Attempts { get; set; }

        // Original 0x0600016e / 0x0600016f.
        public string DreamPowerSlots { get; set; }

        // Original 0x06000170; genuine base initialization precedes all resets.
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

        // Original 0x06000171; preserve authored JSON insertion order.
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

        // Original 0x06000172; base-only construction, no own field initializers.
        public ChallengeQuit() { }
    }
}
