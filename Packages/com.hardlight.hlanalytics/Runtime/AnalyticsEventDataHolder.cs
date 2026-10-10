using System.Text;
using Hardlight.JSON;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x02000004; whole nine-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AnalyticsEventDataHolder : JSONObjectPooled<AnalyticsEventDataHolder>
    {
        private JSONHashtable m_data;
        private int m_eventAttempts;
        // 0x06000018
        public int EventAttempts => m_eventAttempts;
        // 0x06000019
        public override IJsonObject Clone()
        {
            AnalyticsEventDataHolder clone = Spawn();
            clone.m_data = (JSONHashtable)m_data.Clone();
            clone.m_eventAttempts = m_eventAttempts;
            return clone;
        }
        // 0x0600001a
        public static AnalyticsEventDataHolder Create(JSONHashtable data, int eventAttempts = 0)
        {
            AnalyticsEventDataHolder holder = Spawn();
            holder.m_data = data;
            holder.m_eventAttempts = eventAttempts;
            return holder;
        }
        // 0x0600001b
        public bool CheckIsValid() => m_data != null;
        // 0x0600001c: retains the original null/fault behavior and release order.
        public override void Release()
        {
            m_data.Release();
            m_data = null;
            base.Release();
        }
        // 0x0600001d
        public override bool Encode(StringBuilder builder) => m_data.Encode(builder);
        // 0x0600001e
        public void IncreaseEventAttempts() { unchecked { ++m_eventAttempts; } }
        // 0x0600001f
        protected override void Reset()
        {
            base.Reset();
            m_data = null;
            m_eventAttempts = 0;
        }
        // 0x06000020
        public AnalyticsEventDataHolder() { }
    }
}
