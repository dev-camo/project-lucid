// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.SingleToInt 0x02000074.
// 0x0600015f Refresh: x86_64 [0x1a07d20,0x1a07d90).
// 0x06000160 SetUpBindReferenceFields: x86_64 [0x1a07d90,0x1a07e70).
// 0x06000161 .ctor: x86_64 [0x1a07e70,0x1a07fe0).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddNodeMenu("Conversions/Single To Int", int.MaxValue)]
    [AddComponentMenu("")]
    public class SingleToInt : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(float))]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<int> Output = new Bindable<int>();

        public override void Refresh()
        {
            Output.Value = (int)Input.GetValue<float>();
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
