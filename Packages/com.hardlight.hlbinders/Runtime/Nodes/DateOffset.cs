// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.DateOffset 0x0200003e.
// 0x060000c4 Refresh: x86_64 [0x19fb4b0,0x19fb580).
// 0x060000c5 SetUpBindReferenceFields: x86_64 [0x19fb580,0x19fb6c0).
// 0x060000c6 .ctor: x86_64 [0x19fb6c0,0x19fb8a0).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Time/Date Offset", int.MaxValue)]
    public class DateOffset : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(DateTime))]
        public BindReference Input = new BindReference();

        [RestrictType(typeof(TimeSpan))]
        [NodeInput]
        public BindReference Offset = new BindReference();

        [NodeOutput]
        public readonly Bindable<DateTime> Output = new Bindable<DateTime>();

        public override void Refresh()
        {
            Output.Value = Input.GetValue<DateTime>() + Offset.GetValue<TimeSpan>();
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input, Offset };
        }
    }
}
