// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.NumberOrdinalFormatter 0x02000071.
// 0x06000156 Refresh: x86_64 [0x1a06d90,0x1a06e10).
// 0x06000157 SetUpBindReferenceFields: x86_64 [0x1a06e10,0x1a06ef0).
// 0x06000158 .ctor: x86_64 [0x1a06ef0,0x1a07060).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Text/Number Ordinal Formatter", int.MaxValue)]
    public class NumberOrdinalFormatter : BinderNode
    {
        [RestrictType(typeof(int))]
        [NodeInput]
        public BindReference Number = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // The retail body appends no ordinal suffix: it publishes Int32.ToString only.
        public override void Refresh()
        {
            string text = Number.GetValue<int>().ToString();
            Output.Value = text;
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Number };
        }
    }
}
