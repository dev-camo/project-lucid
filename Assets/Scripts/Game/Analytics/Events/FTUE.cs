using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200002f; complete 10 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FTUE : EventContext<FTUE>
    {
        // Original 0x0600018f; exact shipping event-name literal.
        protected override string EventName => "ftue";

        // Original 0x06000190 / 0x06000191.
        public string StepName { get; set; }

        // Original 0x06000192 / 0x06000193.
        public string StepID { get; set; }

        // Original 0x06000194 / 0x06000195.
        public string UserInput { get; set; }

        // Original 0x06000196; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            StepName = string.Empty;
            StepID = string.Empty;
            UserInput = string.Empty;
        }

        // Original 0x06000197; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("step_name", StepName);
            eventDetail.Add("step_id", StepID);
            eventDetail.Add("user_input", UserInput);
        }

        // Original 0x06000198; base-only construction, no own field initializers.
        public FTUE() { }
    }
}
