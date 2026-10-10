using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000035; complete 14 own APIs.
    // Native/source intent remains private pending complete base/provider binding and review.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ResourceChange : EventContext<ResourceChange>
    {
        // Original 0x06000229; exact shipping event-name literal.
        protected override string EventName => "resource_change";

        // Original 0x0600022a / 0x0600022b.
        public bool CurrencyIsSpend { get; set; }

        // Original 0x0600022c / 0x0600022d.
        public string CurrencyType { get; set; }

        // Original 0x0600022e / 0x0600022f.
        public int CurrencyDelta { get; set; }

        // Original 0x06000230 / 0x06000231.
        public string CurrencyReason { get; set; }

        // Original 0x06000232 / 0x06000233.
        public string ItemID { get; set; }

        // Original 0x06000234; genuine base initialization precedes all resets.
        protected override void Initialise()
        {
            base.Initialise();
            CurrencyIsSpend = false;
            CurrencyType = string.Empty;
            CurrencyDelta = 0;
            CurrencyReason = string.Empty;
            ItemID = string.Empty;
        }

        // Original 0x06000235; preserve authored JSON insertion order.
        // Nullable fields are omitted when absent; no guard is added for eventDetail.
        protected override void FillEventDetail(JSONHashtable eventDetail)
        {
            eventDetail.Add("currency_is_spend", CurrencyIsSpend);
            eventDetail.Add("currency_type", CurrencyType);
            eventDetail.Add("currency_delta", CurrencyDelta);
            eventDetail.Add("currency_reason", CurrencyReason);
            eventDetail.Add("item_id", ItemID);
        }

        // Original 0x06000236; base-only construction, no own field initializers.
        public ResourceChange() { }
    }
}
