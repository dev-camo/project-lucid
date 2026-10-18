// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.TimeSpanToString 0x0200007a.
// 0x06000171 Refresh: x86_64 [0x1a09c40,0x1a09ed0).
// 0x06000172 SetUpBindReferenceFields: x86_64 [0x1a09ed0,0x1a0a110).
// 0x06000173 .ctor: x86_64 [0x1a0a110,0x1a0a440).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/TimeSpan To String", int.MaxValue)]
    public class TimeSpanToString : BinderNode
    {
        [RestrictType(typeof(TimeSpan))]
        [NodeInput]
        public BindReference Input = new BindReference();

        [NodeInput]
        [RestrictType(typeof(string))]
        public BindReference GreaterThanDayFormatString = new BindReference();

        [NodeInput]
        [RestrictType(typeof(string))]
        public BindReference GreaterThanHourFormatString = new BindReference();

        [RestrictType(typeof(string))]
        [NodeInput]
        public BindReference GreaterThanMinFormatString = new BindReference();

        [RestrictType(typeof(string))]
        [NodeInput]
        public BindReference LessThanMinFormatString = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // Only the format for the largest positive component is read. An empty
        // format leaves the output empty rather than falling through to the next.
        public override void Refresh()
        {
            TimeSpan span = Input.GetValue<TimeSpan>();
            int days = span.Days;
            int hours = span.Hours;
            int minutes = span.Minutes;
            int seconds = span.Seconds;
            string text = string.Empty;
            string format;
            if (days > 0)
            {
                format = GreaterThanDayFormatString.GetValue<string>();
                if (!string.IsNullOrEmpty(format))
                    text = string.Format(format, days, hours);
            }
            else if (hours > 0)
            {
                format = GreaterThanHourFormatString.GetValue<string>();
                if (!string.IsNullOrEmpty(format))
                    text = string.Format(format, hours, minutes);
            }
            else if (minutes > 0)
            {
                format = GreaterThanMinFormatString.GetValue<string>();
                if (!string.IsNullOrEmpty(format))
                    text = string.Format(format, minutes);
            }
            else
            {
                format = LessThanMinFormatString.GetValue<string>();
                if (!string.IsNullOrEmpty(format))
                    text = string.Format(format, seconds);
            }
            Output.Value = text;
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference>
            {
                Input,
                GreaterThanDayFormatString,
                GreaterThanHourFormatString,
                GreaterThanMinFormatString,
                LessThanMinFormatString,
            };
        }
    }
}
