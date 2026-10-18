// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.BinderOutput 0x0200000e; bodies translated from the 1.10.1 x86-64 binary.
// 0x0600001e Refresh 0x19F4700; 0x0600001f SetUpBindReferenceFields 0x19F4B00; 0x06000020 .ctor 0x19F4BE0.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hardlight.UI.Binding
{
    [AddNodeMenu("Output", 1)]
    [AddComponentMenu("")]
    public class BinderOutput : BinderNode
    {
        [FormerlySerializedAs("source")]
        [NodeInput]
        public BindReference Source = new BindReference();

        [FormerlySerializedAs("target")]
        public BindTarget Target = new BindTarget();

        public override void Refresh()
        {
            if (Source.IsSet && Target != null && enabled)
                Target.SetValue(Source.GetValue<object>());
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Source };
        }
    }
}
