using System;
using System.Collections.Generic;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Declared out of value order, with one duplicate value, so callers can tell the
    // Enum.GetValues order and a first-match search apart from other orders and searches.
    public enum EnumSelectorProbeValue
    {
        Seven = 7,
        Zero = 0,
        Two = 2,
        AlsoTwo = 2,
    }

    // Test-only concrete host for the abstract original HLEnumSelector`1. Editor-only
    // assemblies cannot supply attachable components. It records every Compare and
    // Validate call into a caller-owned list; Comparison overrides the default equality.
    public sealed class EnumSelectorProbe : HLEnumSelector<EnumSelectorProbeValue>
    {
        public List<string> Log;
        public Func<EnumSelectorProbeValue, EnumSelectorProbeValue, bool> Comparison;

        protected override void Validate()
        {
            if (Log != null) Log.Add("Validate " + (int)m_currentValue + " values=" + (m_values == null ? "null" : m_values.Length.ToString()));
        }

        protected override bool Compare(EnumSelectorProbeValue a, EnumSelectorProbeValue b)
        {
            if (Log != null) Log.Add("Compare " + (int)a + "," + (int)b);
            return Comparison != null ? Comparison(a, b) : a == b;
        }
    }
}
