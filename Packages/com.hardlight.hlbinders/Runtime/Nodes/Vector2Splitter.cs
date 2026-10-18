// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.Vector2Splitter 0x0200007d.
// 0x0600017a Refresh: x86_64 [0x1a0ab30,0x1a0abc0).
// 0x0600017b SetUpBindReferenceFields: x86_64 [0x1a0abc0,0x1a0aca0).
// 0x0600017c .ctor: x86_64 [0x1a0aca0,0x1a0ae40).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/Vector2 Splitter", int.MaxValue)]
    public class Vector2Splitter : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(Vector2))]
        public BindReference First = new BindReference();

        [NodeOutput]
        public readonly Bindable<float> X = new Bindable<float>();

        [NodeOutput]
        public readonly Bindable<float> Y = new Bindable<float>();

        public override void Refresh()
        {
            Vector2 value = First.GetValue<Vector2>();
            X.Value = value.x;
            Y.Value = value.y;
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { First };
        }
    }
}
