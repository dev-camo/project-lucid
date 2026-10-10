using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TrackerExitDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TrackerExitDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TrackerExitDefinition : TrackerEndDefinition
    {
        // Original 0x040014d9; offset 0x20.
        [Tooltip("Curve to determine normalised speed on approach to the tracker exit.")]
        [SerializeField]
        private AnimationCurve m_normalisedSpeedFromDistance;
        // Original 0x040014da; offset 0x28.
        [Tooltip("Speed to exit tracker.")]
        [SerializeField]
        private float m_exitSpeed;
        // Original 0x040014db; offset 0x2c.
        [Tooltip("Rotation to apply to character around the local up axis on exit.")]
        [SerializeField]
        private float m_exitRotation;
        // Original 0x040014dc; offset 0x30.
        [Tooltip("Definition to override ability if active next on exit.")]
        [SerializeField]
        private CharacterAbilityDefinition m_abilityDefinitionOverride;

        // Original 06001fa0: capture exit speed before the curve call.
        public float EvaluateSpeed(float distance, float startSpeed)
        {
            float exitSpeed = m_exitSpeed;
            float difference = startSpeed - exitSpeed;
            return exitSpeed + difference * m_normalisedSpeedFromDistance.Evaluate(distance);
        }
        // Original 06001fa1: the curve is captured; an empty curve retains index -1.
        public float ExitDistance
        {
            get
            {
                AnimationCurve curve = m_normalisedSpeedFromDistance;
                return curve[curve.length - 1].time;
            }
        }
        public float ExitRotation => m_exitRotation;
        public CharacterAbilityDefinition AbilityDefinitionOverride => m_abilityDefinitionOverride;
        public TrackerExitDefinition() { }
    }
}
