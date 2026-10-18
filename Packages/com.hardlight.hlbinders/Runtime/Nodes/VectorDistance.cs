// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.VectorDistance 0x0200007f.
// 0x06000180 Refresh: x86_64 [0x1a0b470,0x1a0b580).
// 0x06000181 SetUpBindReferenceFields: x86_64 [0x1a0b580,0x1a0b6c0).
// 0x06000182 .ctor: x86_64 [0x1a0b6c0,0x1a0b8a0).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Math/Vector Distance", int.MaxValue)]
    public class VectorDistance : BinderNode
    {
        [RestrictType(typeof(Vector2))]
        [NodeInput]
        public BindReference First = new BindReference();

        [NodeInput]
        [RestrictType(typeof(Vector2))]
        public BindReference Second = new BindReference();

        [NodeOutput]
        public readonly Bindable<float> Distance = new Bindable<float>();

        public override void Refresh()
        {
            Distance.Value = Vector2.Distance(First.GetValue<Vector2>(), Second.GetValue<Vector2>());
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { First, Second };
        }
    }
}
