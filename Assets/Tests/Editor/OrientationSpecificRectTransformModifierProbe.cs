using System;
using System.Collections.Generic;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Records the two virtual slots of Hardlight.OrientationSpecificRectTransformModifier
    // (slot 4 InitialiseAndSetValues 0x06000543, slot 5 InitialiseValues 0x06000544)
    // so tests can observe that OnEnable, the coroutine and InitialiseAndSetValues
    // dispatch through the vtable. Both overrides call the original base bodies
    // unless a test injects its own InitialiseValues content.
    public sealed class OrientationSpecificRectTransformModifierProbe : OrientationSpecificRectTransformModifier
    {
        [NonSerialized] public readonly List<string> Calls = new List<string>();
        [NonSerialized] public float? InjectedWidth;

        protected override void InitialiseAndSetValues()
        {
            Calls.Add("InitialiseAndSetValues");
            base.InitialiseAndSetValues();
        }

        protected override void InitialiseValues()
        {
            Calls.Add("InitialiseValues");
            if (!InjectedWidth.HasValue)
            {
                base.InitialiseValues();
                return;
            }
            m_desiredSetting.Add(new OrientationSpecificRectSettings
            {
                Setting = OrientationSpecificRectSettings.SettingType.Width,
                SettingValue = InjectedWidth.Value
            });
        }
    }
}
