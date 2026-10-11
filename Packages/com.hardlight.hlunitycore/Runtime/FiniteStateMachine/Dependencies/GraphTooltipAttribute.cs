// Original HLUnityCore.Runtime TypeDef0x020001d9 from supplied release1.10.1.
// Original ctor0x06000c4f: both complete native slices call System.Attribute's
// constructor before storing the supplied tooltip reference at field offset0x10.
// Original field0x04000657 is public readonly; null and reference identity remain.
// Original AttributeUsage payload260 permits Class|Field, AllowMultiple=false,
// Inherited=true. Exact original CSharp spelling and current emission remain held.
using System;

namespace Hardlight
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field,
        AllowMultiple = false, Inherited = true)]
    public class GraphTooltipAttribute : Attribute
    {
        public readonly string Tooltip;

        public GraphTooltipAttribute(string tooltip) : base()
        {
            Tooltip = tooltip;
        }
    }
}
