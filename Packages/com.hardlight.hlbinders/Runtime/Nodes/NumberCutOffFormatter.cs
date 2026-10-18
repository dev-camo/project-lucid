// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.NumberCutOffFormatter 0x0200006f.
// 0x06000150 Refresh: x86_64 [0x1a06630,0x1a06700).
// 0x06000151 SetUpBindReferenceFields: x86_64 [0x1a06700,0x1a067e0).
// 0x06000152 .ctor: x86_64 [0x1a067e0,0x1a06950).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddNodeMenu("Text/Number Cut-Off Formatter", int.MaxValue)]
    [AddComponentMenu("")]
    public class NumberCutOffFormatter : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(int))]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        [SerializeField]
        private int cutOff;

        // A non-positive cut-off disables capping; a value equal to it is shown as is.
        public override void Refresh()
        {
            int value = Input.GetValue<int>();
            Output.Value = cutOff > 0 && value > cutOff ? string.Format("{0}+", cutOff) : value.ToString();
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
