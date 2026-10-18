// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.SimpleBinder 0x02000010; bodies translated from the 1.10.1 x86-64 binary.
// 0x06000023 get_ShowInNodeEditor 0x19F4D60; 0x06000024 Refresh 0x19F4D70; 0x06000025 .ctor 0x19F4F50.

using UnityEngine;

namespace Hardlight.UI.Binding
{
    [AddComponentMenu("Hardlight UI/Binding/Binder (Simple)")]
    public class SimpleBinder : BinderOutput
    {
        [SerializeField]
        private bool invertIfBool;

        public override bool ShowInNodeEditor
        {
            get { return false; }
        }

        public override void Refresh()
        {
            if (Source.IsSet && Target != null && enabled)
            {
                // Only a bool source is inverted; any other source passes through unchanged.
                if (invertIfBool && Source.ValueType == typeof(bool))
                    Target.SetValue(!Source.GetValue<bool>());
                else
                    Target.SetValue(Source.GetValue<object>());
            }
        }
    }
}
