using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000039; complete 10 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SettingChanged : EventContext<SettingChanged>
    {
        // Original 0x06000255; exact shipping event-name literal.
        protected override string EventName => "setting_changed";

        // Original 0x06000256 / 0x06000257.
        public string SettingName { get; set; }

        // Original 0x06000258 / 0x06000259.
        public string OldValue { get; set; }

        // Original 0x0600025a / 0x0600025b.
        public string NewValue { get; set; }

        // Original 0x0600025c; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            SettingName = string.Empty;
            OldValue = string.Empty;
            NewValue = string.Empty;
        }

        // Original 0x0600025d; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("setting_name", SettingName);
            eventDetail.Add("old_value", OldValue);
            eventDetail.Add("new_value", NewValue);
        }

        // Original 0x0600025e; base-only construction, no own field initializers.
        public SettingChanged() { }
    }
}
