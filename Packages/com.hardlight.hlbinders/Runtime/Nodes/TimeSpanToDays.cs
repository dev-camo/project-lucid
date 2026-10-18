// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.TimeSpanToDays 0x02000078.
// 0x0600016b Refresh: x86_64 [0x1a09640,0x1a096f0).
// 0x0600016c SetUpBindReferenceFields: x86_64 [0x1a096f0,0x1a097d0).
// 0x0600016d .ctor: x86_64 [0x1a097d0,0x1a09940).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddNodeMenu("Conversions/TimeSpan To Days", int.MaxValue)]
    [AddComponentMenu("")]
    public class TimeSpanToDays : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(TimeSpan))]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<int> Output = new Bindable<int>();

        // Adds 0.5 then truncates (cvttsd2si), so negative values move up: -1 becomes 0.
        public override void Refresh()
        {
            Output.Value = (int)(Input.GetValue<TimeSpan>().TotalDays + 0.5);
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
