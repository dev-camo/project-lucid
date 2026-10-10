using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000037; complete 8 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SessionEnd : EventContext<SessionEnd>
    {
        // Original 0x06000241; exact shipping event-name literal.
        protected override string EventName => "session_end";

        // Original 0x06000242 / 0x06000243.
        public int SessionNumber { get; set; }

        // Original 0x06000244 / 0x06000245.
        public long SessionLength { get; set; }

        // Original 0x06000246; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            SessionNumber = 0;
            SessionLength = 0L;
        }

        // Original 0x06000247; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("session_number", SessionNumber);
            eventDetail.Add("session_length", SessionLength);
        }

        // Original 0x06000248; base-only construction, no own field initializers.
        public SessionEnd() { }
    }
}
