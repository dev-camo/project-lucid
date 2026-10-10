using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200002d; complete 18 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeStart : EventContext<ChallengeStart>
    {
        // Original 0x06000173; exact shipping event-name literal.
        protected override string EventName => "challenge_start";

        // Original 0x06000174 / 0x06000175.
        public string ChallengeID { get; set; }

        // Original 0x06000176 / 0x06000177.
        public string ChallengeName { get; set; }

        // Original 0x06000178 / 0x06000179.
        public int ChallengeIndex { get; set; }

        // Original 0x0600017a / 0x0600017b.
        public int ChallengeSetIndex { get; set; }

        // Original 0x0600017c / 0x0600017d.
        public bool IsReplay { get; set; }

        // Original 0x0600017e / 0x0600017f.
        public int Attempts { get; set; }

        // Original 0x06000180 / 0x06000181.
        public string DreamPowerSlots { get; set; }

        // Original 0x06000182; genuine base initialization precedes all resets.
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

        // Original 0x06000183; preserve authored JSON insertion order.
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

        // Original 0x06000184; base-only construction, no own field initializers.
        public ChallengeStart() { }
    }
}
