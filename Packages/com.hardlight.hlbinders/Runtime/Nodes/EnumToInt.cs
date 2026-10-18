// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.EnumToInt 0x02000042.
// 0x060000d0 Refresh: x86_64 [0x19fc610,0x19fc690).
// 0x060000d1 SetUpBindReferenceFields: x86_64 [0x19fc690,0x19fc770).
// 0x060000d2 .ctor: x86_64 [0x19fc770,0x19fc8e0).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/Enum To Int", int.MaxValue)]
    public class EnumToInt : BinderNode
    {
        [RestrictType(typeof(Enum))]
        [NodeInput]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<int> Output = new Bindable<int>();

        // A null Input reference publishes 0 instead of faulting (0x19fc65a).
        public override void Refresh()
        {
            Output.Value = Input != null ? Input.GetValue<int>() : 0;
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
