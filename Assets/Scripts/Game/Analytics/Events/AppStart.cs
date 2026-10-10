using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000028; complete 4 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AppStart : EventContext<AppStart>
    {
        // Original 0x06000123; exact shipping event-name literal.
        protected override string EventName => "app_start";

        // Original 0x06000124; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
        }

        // Original 0x06000125; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
        }

        // Original 0x06000126; base-only construction, no own field initializers.
        public AppStart() { }
    }
}
