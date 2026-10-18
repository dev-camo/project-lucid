// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.DateStringFormatter 0x0200003f.
// 0x060000c7 Refresh: x86_64 [0x19fb8a0,0x19fbbb0).
// 0x060000c8 SetUpBindReferenceFields: x86_64 [0x19fbbb0,0x19fbd90).
// 0x060000c9 .ctor: x86_64 [0x19fbd90,0x19fc050).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Text/Date String Formatter", int.MaxValue)]
    public class DateStringFormatter : BinderNode
    {
        [NodeInput]
        public BindReference String = new BindReference();

        [NodeInput]
        public BindReference dateTime1 = new BindReference();

        [NodeInput]
        public BindReference dateTime2 = new BindReference();

        [NodeInput]
        public BindReference dateTime3 = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // The three dates are converted to local time before the format is read;
        // a null or empty format skips string.Format.
        public override void Refresh()
        {
            List<object> arguments = new List<object>();
            arguments.Add(dateTime1.GetValue<DateTime>().ToLocalTime());
            arguments.Add(dateTime2.GetValue<DateTime>().ToLocalTime());
            arguments.Add(dateTime3.GetValue<DateTime>().ToLocalTime());
            string format = String.GetValue<string>();
            if (string.IsNullOrEmpty(format))
                Output.Value = "";
            else
                Output.Value = string.Format(format, arguments.ToArray());
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { String, dateTime1, dateTime2, dateTime3 };
        }
    }
}
