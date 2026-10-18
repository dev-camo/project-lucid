// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.NumberDeltaFormatter 0x02000070.
// 0x06000153 Refresh: x86_64 [0x1a06950,0x1a06b40).
// 0x06000154 SetUpBindReferenceFields: x86_64 [0x1a06b40,0x1a06c20).
// 0x06000155 .ctor: x86_64 [0x1a06c20,0x1a06d90).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Text/Number Delta Formatter", int.MaxValue)]
    public class NumberDeltaFormatter : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(float))]
        public BindReference Number = new BindReference();

        [SerializeField]
        private bool withSpace = true;

        [SerializeField]
        private bool integer;

        [SerializeField]
        private bool prefixOnZero = true;

        [SerializeField]
        private bool hideIfZero;

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        // Original quirk: the non-integer format "{2:1}" is a custom format whose
        // only character is a literal, so the magnitude always prints as "1".
        public override void Refresh()
        {
            float number = Number.GetValue<float>();
            if (integer)
                number = (int)number;
            if (hideIfZero && number == 0f)
            {
                Output.Value = string.Empty;
                return;
            }

            string format = integer ? "{0}{1}{2:0}" : "{0}{1}{2:1}";
            string sign = string.Empty;
            string space = string.Empty;
            if (number != 0f || prefixOnZero)
            {
                sign = number >= 0f ? "+" : "-";
                if (withSpace)
                    space = " ";
            }
            Output.Value = string.Format(format, sign, space, Mathf.Abs(number));
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Number };
        }
    }
}
