using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000744: complete seven-method API.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterTargetingModifier : CharacterCollisionModifier
    {
        [SerializeField]
        [Tooltip("Flags whether this collider is valid for targeting.")]
        private bool m_isValid;

        [Tooltip("Overrides the targeting aim type.")]
        [SerializeField]
        private AbilityAimTargetType m_aimType;

        [Tooltip("Overrides the targeting ground type.")]
        [SerializeField]
        private AbilityAimTargetType m_groundType;

        [SerializeField]
        [Tooltip("Additional local rotation to the aim target.")]
        private Quaternion m_aimRotationOffset = Quaternion.identity;

        [Tooltip("Additional local rotation to the UI target.")]
        [SerializeField]
        private Quaternion m_uiRotationOffset = Quaternion.identity;

        [Tooltip("Targeting is snapped on completion.")]
        [SerializeField]
        private bool m_snapToTarget;

        // 0x060028e1.
        public bool IsValid => m_isValid;
        // 0x060028e2.
        public AbilityAimTargetType AimType => m_aimType;
        // 0x060028e3.
        public AbilityAimTargetType GroundType => m_groundType;
        // 0x060028e4.
        public Quaternion AimRotationOffset => m_aimRotationOffset;
        // 0x060028e5.
        public Quaternion UIRotationOffset => m_uiRotationOffset;
        // 0x060028e6.
        public bool SnapToTarget => m_snapToTarget;

        // 0x060028e7. Two separate identity reads precede the real base initializer.
        // Native construction has no true assignment to either boolean field.
        public CharacterTargetingModifier() { }
    }
}
