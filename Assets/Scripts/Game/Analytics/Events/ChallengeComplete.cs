using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200002a; complete 34 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ChallengeComplete : EventContext<ChallengeComplete>
    {
        // Original 0x0600012d; exact shipping event-name literal.
        protected override string EventName => "challenge_complete";

        // Original 0x0600012e / 0x0600012f.
        public string ChallengeID { get; set; }

        // Original 0x06000130 / 0x06000131.
        public string ChallengeName { get; set; }

        // Original 0x06000132 / 0x06000133.
        public int ChallengeIndex { get; set; }

        // Original 0x06000134 / 0x06000135.
        public int ChallengeSetIndex { get; set; }

        // Original 0x06000136 / 0x06000137.
        public int XpGained { get; set; }

        // Original 0x06000138 / 0x06000139.
        public int BonusXpGained { get; set; }

        // Original 0x0600013a / 0x0600013b.
        public int XpTotal { get; set; }

        // Original 0x0600013c / 0x0600013d.
        public int Score { get; set; }

        // Original 0x0600013e / 0x0600013f.
        public int BestScore { get; set; }

        // Original 0x06000140 / 0x06000141.
        public int TotalScore { get; set; }

        // Original 0x06000142 / 0x06000143.
        public float ChallengeTime { get; set; }

        // Original 0x06000144 / 0x06000145.
        public float BestTime { get; set; }

        // Original 0x06000146 / 0x06000147.
        public bool IsReplay { get; set; }

        // Original 0x06000148 / 0x06000149.
        public int Attempts { get; set; }

        // Original 0x0600014a / 0x0600014b.
        public string DreamPowerSlots { get; set; }

        // Original 0x0600014c; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            ChallengeID = string.Empty;
            ChallengeName = string.Empty;
            ChallengeIndex = 0;
            ChallengeSetIndex = 0;
            XpGained = 0;
            BonusXpGained = 0;
            XpTotal = 0;
            Score = 0;
            BestScore = 0;
            TotalScore = 0;
            ChallengeTime = 0f;
            BestTime = 0f;
            IsReplay = false;
            Attempts = 0;
            DreamPowerSlots = string.Empty;
        }

        // Original 0x0600014d; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("challenge_id", ChallengeID);
            eventDetail.Add("challenge_name", ChallengeName);
            eventDetail.Add("challenge_index", ChallengeIndex);
            eventDetail.Add("challenge_set_index", ChallengeSetIndex);
            eventDetail.Add("xp_gained", XpGained);
            eventDetail.Add("bonus_xp_gained", BonusXpGained);
            eventDetail.Add("xp_total", XpTotal);
            eventDetail.Add("score", Score);
            eventDetail.Add("best_score", BestScore);
            eventDetail.Add("total_score", TotalScore);
            eventDetail.Add("challenge_time", ChallengeTime);
            eventDetail.Add("best_time", BestTime);
            eventDetail.Add("is_replay", IsReplay);
            eventDetail.Add("attempts", Attempts);
            eventDetail.Add("dream_power_slots", DreamPowerSlots);
        }

        // Original 0x0600014e; base-only construction, no own field initializers.
        public ChallengeComplete() { }
    }
}
