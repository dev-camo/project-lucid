// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.EnumToString 0x02000043.
// 0x060000d3 Refresh: x86_64 [0x19fc8e0,0x19fc9b0).
// 0x060000d4 SetUpBindReferenceFields: x86_64 [0x19fc9b0,0x19fca90).
// 0x060000d5 .ctor: x86_64 [0x19fca90,0x19fcc00).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/EnumToString", int.MaxValue)]
    public class EnumToString : BinderNode
    {
        [NodeInput]
        public BindReference First = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // A null First reference publishes an empty string; otherwise the value is
        // read once for the empty check and again for the output.
        public override void Refresh()
        {
            if (First == null || string.IsNullOrEmpty(First.GetValue<string>()))
                Output.Value = string.Empty;
            else
                Output.Value = First.GetValue<string>();
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { First };
        }
    }
}
