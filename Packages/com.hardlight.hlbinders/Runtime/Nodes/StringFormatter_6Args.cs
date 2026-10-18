// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.StringFormatter_6Args 0x02000077.
// 0x06000168 Refresh: x86_64 [0x1a08ba0,0x1a08f40).
// 0x06000169 SetUpBindReferenceFields: x86_64 [0x1a08f40,0x1a09220).
// 0x0600016a .ctor: x86_64 [0x1a09220,0x1a09640).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddNodeMenu("Text/String Formatter (6 args)", int.MaxValue)]
    [AddComponentMenu("")]
    public class StringFormatter_6Args : BinderNode
    {
        [NodeInput]
        public BindReference String = new BindReference();

        [NodeInput]
        public BindReference Arg1 = new BindReference();

        [NodeInput]
        public BindReference Arg2 = new BindReference();

        [NodeInput]
        public BindReference Arg3 = new BindReference();

        [NodeInput]
        public BindReference Arg4 = new BindReference();

        [NodeInput]
        public BindReference Arg5 = new BindReference();

        [NodeInput]
        public BindReference Arg6 = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // All six argument getters run before the format getter; only a null
        // format becomes empty.
        public override void Refresh()
        {
            List<object> arguments = new List<object>();
            arguments.Add(Arg1.GetValue());
            arguments.Add(Arg2.GetValue());
            arguments.Add(Arg3.GetValue());
            arguments.Add(Arg4.GetValue());
            arguments.Add(Arg5.GetValue());
            arguments.Add(Arg6.GetValue());
            Output.Value = string.Format(String.GetValue<string>() ?? string.Empty, arguments.ToArray());
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { String, Arg1, Arg2, Arg3, Arg4, Arg5, Arg6 };
        }
    }
}
