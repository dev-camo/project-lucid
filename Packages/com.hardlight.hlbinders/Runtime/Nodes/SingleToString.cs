// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.Nodes.SingleToString 0x02000075.
// 0x06000162 Refresh: x86_64 [0x1a07fe0,0x1a08120).
// 0x06000163 SetUpBindReferenceFields: x86_64 [0x1a08120,0x1a08200).
// 0x06000164 .ctor: x86_64 [0x1a08200,0x1a08370).
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding.Nodes
{
    [AddComponentMenu("")]
    [AddNodeMenu("Conversions/Single To String", int.MaxValue)]
    public class SingleToString : BinderNode
    {
        [NodeInput]
        [RestrictType(typeof(float))]
        public BindReference Input = new BindReference();

        [NodeOutput]
        public readonly Bindable<string> Output = new Bindable<string>();

        [SerializeField]
        private bool m_floor;

        [SerializeField]
        private bool m_ceil;

        [Range(0f, 4f)]
        [SerializeField]
        private int m_numberOfDecimalPlaces;

        // Floor and ceil apply in that order when both are set; any decimal-place
        // count outside 1..4 uses the general ToString().
        public override void Refresh()
        {
            float value = Input.GetValue<float>();
            if (m_floor)
                value = Mathf.Floor(value);
            if (m_ceil)
                value = Mathf.Ceil(value);

            string text;
            switch (m_numberOfDecimalPlaces)
            {
                case 1:
                    text = value.ToString("F1");
                    break;
                case 2:
                    text = value.ToString("F2");
                    break;
                case 3:
                    text = value.ToString("F3");
                    break;
                case 4:
                    text = value.ToString("F4");
                    break;
                default:
                    text = value.ToString();
                    break;
            }
            Output.Value = text;
        }

        protected override void SetUpBindReferenceFields()
        {
            bindReferences = new List<BindReference> { Input };
        }
    }
}
