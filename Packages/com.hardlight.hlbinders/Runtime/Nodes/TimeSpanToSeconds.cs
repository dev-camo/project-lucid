// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.TimeSpanToSeconds 0x02000079.
// 0x0600016e Refresh: x86_64 [0x1a09940,0x1a099f0).
// 0x0600016f SetUpBindReferenceFields: x86_64 [0x1a099f0,0x1a09ad0).
// 0x06000170 .ctor: x86_64 [0x1a09ad0,0x1a09c40).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/TimeSpan To Seconds", int.MaxValue)]
    public class TimeSpanToSeconds : BinderNode
    {
        [RestrictType(typeof(TimeSpan))]
        [NodeInput]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<int> Output = new Bindable<int>();

        // Adds 0.5 and truncates toward zero, so negative spans round toward zero.
        public override void Refresh()
        {
            Output.Value = (int)(Input.GetValue<TimeSpan>().TotalSeconds + 0.5);
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
