using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000029; complete 6 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ButtonPressed : EventContext<ButtonPressed>
    {
        // Original 0x06000127; exact shipping event-name literal.
        protected override string EventName => "button_pressed";

        // Original 0x06000128 / 0x06000129.
        public string ButtonName { get; set; }

        // Original 0x0600012a; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            ButtonName = string.Empty;
        }

        // Original 0x0600012b; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("button_name", ButtonName);
        }

        // Original 0x0600012c; base-only construction, no own field initializers.
        public ButtonPressed() { }
    }
}
