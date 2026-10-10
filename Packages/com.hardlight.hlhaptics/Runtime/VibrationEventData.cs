using System;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[CreateAssetMenu(menuName = "Hardlight/HLHaptics/VibrationEventData")]
public sealed class VibrationEventData : ScriptableObject
{
    [HashEnum(typeof(VibrationEvent))]
    public VibrationEvent m_vibrationEvent;
    public Vibrations[] m_vibrationEvents;

    // HLHaptics.Runtime06000001 delegates to ScriptableObject only.

    [Serializable]
    public class Vibrations
    {
        public float m_delay;
        [HashEnum(typeof(VibrationType))]
        public VibrationType m_vibrationType;
        public float m_repeat;

        // Original06000002 delegates to Object only. This is a reference class,
        // so an authored null array entry retains the original null failure.
    }
}
