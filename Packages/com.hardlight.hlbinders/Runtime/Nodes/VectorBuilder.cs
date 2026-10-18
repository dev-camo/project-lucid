// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.VectorBuilder 0x0200007e.
// 0x0600017d Refresh: x86_64 [0x1a0ae40,0x1a0af30).
// 0x0600017e SetUpBindReferenceFields: x86_64 [0x1a0af30,0x1a0b110).
// 0x0600017f .ctor: x86_64 [0x1a0b110,0x1a0b470).
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [AddNodeMenu("Conversions/Vector Builder", int.MaxValue)]
    [AddComponentMenu("")]
    public class VectorBuilder : BinderNode
    {
        [RestrictType(typeof(float))]
        [NodeInput]
        public BindReference X = new BindReference();

        [RestrictType(typeof(float))]
        [NodeInput]
        public BindReference Y = new BindReference();

        [RestrictType(typeof(float))]
        [NodeInput]
        public BindReference Z = new BindReference();

        [RestrictType(typeof(float))]
        [NodeInput]
        public BindReference W = new BindReference();

        [NodeOutput]
        public readonly Bindable<Vector2> Vector2Output = new Bindable<Vector2>();

        [NodeOutput]
        public readonly Bindable<Vector3> Vector3Output = new Bindable<Vector3>();

        [NodeOutput]
        public readonly Bindable<Vector4> Vector4Output = new Bindable<Vector4>();

        // All four inputs are read before any output is published.
        public override void Refresh()
        {
            float x = X.GetValue<float>();
            float y = Y.GetValue<float>();
            float z = Z.GetValue<float>();
            float w = W.GetValue<float>();
            Vector2Output.Value = new Vector2(x, y);
            Vector3Output.Value = new Vector3(x, y, z);
            Vector4Output.Value = new Vector4(x, y, z, w);
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { X, Y, Z, W };
        }
    }
}
