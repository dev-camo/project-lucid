// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.IntToString 0x02000046.
// 0x060000dc Refresh: x86_64 [0x19fd290,0x19fd310).
// 0x060000dd SetUpBindReferenceFields: x86_64 [0x19fd310,0x19fd3f0).
// 0x060000de .ctor: x86_64 [0x19fd3f0,0x19fd560).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/Int To String", int.MaxValue)]
    public class IntToString : BinderNode
    {
        [RestrictType(typeof(int))]
        [NodeInput]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        public override void Refresh()
        {
            Output.Value = Input.GetValue<int>().ToString();
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
