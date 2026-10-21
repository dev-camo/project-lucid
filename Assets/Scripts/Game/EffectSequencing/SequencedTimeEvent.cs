using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime SequencedTimeEvent (TypeDef 0x02000629, x86-64 0x56AC20..0x56AC9F): blends the game's
    // time scales to a configured setting along a curve.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SequencedTimeEvent : SequencedEffect
    {
        [SerializeField] private TimeSettings_SDT m_toSetting;
        [SerializeField] private AnimationCurve m_timeScaling;
        [SerializeField] private TimeManipulator m_timeManipulator;
        [SerializeField] private bool m_resetAfter;

        // 060020fa: the setting's category-enum overrides are resolved to category objects on every trigger.
        protected override void OnEffectTriggered(IEffectData data)
        {
            m_toSetting.ApplyTimeCategoryObjectConversion(SystemConfiguration.GetConfig<TimeCategoryLookup>().Dictionary);
            m_timeManipulator.ApplyTimeScaling(m_toSetting, m_timeScaling, m_onComplete, m_resetAfter);
        }

        public SequencedTimeEvent() { } // 060020fb
    }
}
