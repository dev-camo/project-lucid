using System.Collections.Generic;

namespace ProjectLucid.Tests.UISelectionShared
{
    public enum EmptyEnumSelectorProbeValue
    {
    }

    // Test-only host for HLEnumSelector`1 over an enum with no values.
    public sealed class EmptyEnumSelectorProbe : HLEnumSelector<EmptyEnumSelectorProbeValue>
    {
        public List<string> Log;

        protected override void Validate()
        {
            if (Log != null) Log.Add("Validate");
        }

        protected override bool Compare(EmptyEnumSelectorProbeValue a, EmptyEnumSelectorProbeValue b)
        {
            if (Log != null) Log.Add("Compare");
            return a == b;
        }
    }
}
