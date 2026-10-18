// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.NumberPercentFormatter 0x02000072.
// 0x06000159 Refresh: x86_64 [0x1a07060,0x1a073c0).
// 0x0600015a SetUpBindReferenceFields: x86_64 [0x1a073c0,0x1a074a0).
// 0x0600015b .ctor: x86_64 [0x1a074a0,0x1a07610).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddNodeMenu("Text/Number Percent Formatter", int.MaxValue)]
    [AddComponentMenu("")]
    public class NumberPercentFormatter : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(float))]
        public BindReference Number = new BindReference();

        [SerializeField]
        private bool integer = true;

        [SerializeField]
        private bool prefix;

        [SerializeField]
        private bool withSpace;

        [SerializeField]
        private bool multiplyBy100;

        [SerializeField]
        private bool floor;

        [SerializeField]
        private int decimalPlaces = 1;

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // The space is only emitted together with the sign prefix.
        public override void Refresh()
        {
            float number = Number.GetValue<float>();
            if (multiplyBy100)
                number *= 100f;
            if (floor)
                number = Mathf.Floor(number);

            string format = integer ? "{0}{1}{2:0}{3}" : "{0}{1}{2:F" + decimalPlaces.ToString() + "}{3}";
            Output.Value = string.Format(format,
                prefix ? (number >= 0f ? "+" : "-") : string.Empty,
                withSpace && prefix ? " " : string.Empty,
                Mathf.Abs(number),
                '%');
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Number };
        }
    }
}
