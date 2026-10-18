// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.TraversalTime 0x0200007c.
// 0x06000177 Refresh: x86_64 [0x1a0a730,0x1a0a810).
// 0x06000178 SetUpBindReferenceFields: x86_64 [0x1a0a810,0x1a0a950).
// 0x06000179 .ctor: x86_64 [0x1a0a950,0x1a0ab30).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Math/Traversal Time", int.MaxValue)]
    public class TraversalTime : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(float))]
        public BindReference Distance = new BindReference();

        [RestrictType(typeof(float))]
        [NodeInput]
        public BindReference UnitsPerSecond = new BindReference();

        [NodeOutput]
        public readonly Bindable<TimeSpan> Time = new Bindable<TimeSpan>();

        // The quotient is taken in single precision before widening to seconds.
        public override void Refresh()
        {
            Time.Value = TimeSpan.FromSeconds(Distance.GetValue<float>() / UnitsPerSecond.GetValue<float>());
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Distance, UnitsPerSecond };
        }
    }
}
