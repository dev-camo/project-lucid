// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.MemoryFormatter 0x0200006c.
// 0x06000149 Refresh: x86_64 [0x1a06320,0x1a063a0).
// 0x0600014a SetUpBindReferenceFields: x86_64 [0x1a063a0,0x1a06480).
// 0x0600014b .ctor: x86_64 [0x1a06480,0x1a065f0).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Text/Memory Size Formatter", int.MaxValue)]
    public class MemoryFormatter : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(int))]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // The int widens to the long parameter without sign extension
        // (mov edi, eax at 0x1a06361), so negative sizes format as large ones.
        public override void Refresh()
        {
            uint size = (uint)Input.GetValue<int>();
            Output.Value = HLDebug.FormatMemorySize(size);
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
