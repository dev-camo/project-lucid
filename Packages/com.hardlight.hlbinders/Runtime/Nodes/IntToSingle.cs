// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.IntToSingle 0x02000045.
// 0x060000d9 Refresh: x86_64 [0x19fcfd0,0x19fd040).
// 0x060000da SetUpBindReferenceFields: x86_64 [0x19fd040,0x19fd120).
// 0x060000db .ctor: x86_64 [0x19fd120,0x19fd290).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/Int To Single", int.MaxValue)]
    public class IntToSingle : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(int))]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<float> Output = new Bindable<float>();

        public override void Refresh()
        {
            Output.Value = Input.GetValue<int>();
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
