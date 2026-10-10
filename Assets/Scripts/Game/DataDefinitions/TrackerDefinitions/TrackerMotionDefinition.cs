using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TrackerMotionDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TrackerMotionDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TrackerMotionDefinition : ScriptableObject
    {
        // Original 0x040014e4; offset 0x18.
        [Tooltip("Motion to be overriden for the duration of tracking a surface.")]
        [SerializeField]
        private CharacterAbilityDefinition_Movement.SlopeMotion m_motionOverride;

        public CharacterAbilityDefinition_Movement.SlopeMotion MotionOverride => m_motionOverride;
        public TrackerMotionDefinition() { }
    }
}
