// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.BindableExtn 0x02000012; translated from the 1.10.1 x86-64 binary.
// 0x06000037 SetListValue: open MethodDef pointer none; fully shared body 0x8E3870 (method RGCTX 14 entries).

using System.Collections.Generic;

namespace Hardlight.UI.Binding
{
    public static class BindableExtn
    {
        // Publishes a copy of value unless the current list already holds equal elements in order.
        // A null value compares as an empty list and is published as a new empty list.
        public static void SetListValue<ValueT>(this Bindable<List<ValueT>> source, List<ValueT> value)
        {
            List<ValueT> current = source.Value;
            if (current != null && current.Count == (value != null ? value.Count : 0))
            {
                bool changed = false;
                if (value != null)
                {
                    for (int i = 0; i < current.Count; i++)
                    {
                        if (!EqualityComparer<ValueT>.Default.Equals(current[i], value[i]))
                        {
                            changed = true;
                            break;
                        }
                    }
                }
                if (!changed) return;
            }
            source.Value = value != null ? new List<ValueT>(value) : new List<ValueT>();
        }
    }
}
