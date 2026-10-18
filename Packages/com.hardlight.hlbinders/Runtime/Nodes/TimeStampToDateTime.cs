// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.TimeStampToDateTime 0x0200007b.
// 0x06000174 Refresh: x86_64 [0x1a0a440,0x1a0a4e0).
// 0x06000175 SetUpBindReferenceFields: x86_64 [0x1a0a4e0,0x1a0a5c0).
// 0x06000176 .ctor: x86_64 [0x1a0a5c0,0x1a0a730).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/TimeStamp to DateTime", int.MaxValue)]
    public class TimeStampToDateTime : BinderNode
    {
        [NodeInput]
        public BindReference Ulong = new BindReference();

        [NodeOutput]
        public readonly Bindable<DateTime> Output = new Bindable<DateTime>();

        // The time stamp is Unix milliseconds (TimeUtils.FromUnixTime(ulong)).
        public override void Refresh()
        {
            Output.Value = TimeUtils.FromUnixTime(Ulong.GetValue<ulong>());
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Ulong };
        }
    }
}
