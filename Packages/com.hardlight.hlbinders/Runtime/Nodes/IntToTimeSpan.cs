// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.IntToTimeSpan 0x02000047.
// 0x060000df Refresh: x86_64 [0x19fd560,0x19fd5f0).
// 0x060000e0 SetUpBindReferenceFields: x86_64 [0x19fd5f0,0x19fd6d0).
// 0x060000e1 .ctor: x86_64 [0x19fd6d0,0x19fd840).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/Int to TimeSpan", int.MaxValue)]
    public class IntToTimeSpan : BinderNode
    {
        [NodeInput]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<TimeSpan> Output = new Bindable<TimeSpan>();

        // The input is a number of seconds (TimeSpan(int hours, int minutes, int seconds)).
        public override void Refresh()
        {
            Output.Value = new TimeSpan(0, 0, Input.GetValue<int>());
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
