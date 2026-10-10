using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_Knockback", menuName = "HardlightProject/DefinitionData/AbilityDef/Character_Knockback", order = 1)]
    public class CharacterAbilityDefinition_Knockback : CharacterAbilityDefinition
    {
        [Min(0)]
        [SerializeField]
        [Header("Knockback")]
        private float m_minDurationSeconds = 0.5f;
        [Min(0)]
        [SerializeField]
        private float m_maxDurationSeconds = 1f;
        [Min(0)]
        [SerializeField]
        private float m_recoveryDurationSeconds = 0.2f;
        [SerializeField]
        [Tooltip("Local velocity to add to character movement on knockback. This will be on top of the velocity applied from the hazard RigidBodyForceTrigger.")]
        private Vector3 m_knockbackAdditiveVelocity = Vector3.zero;
        [SerializeField]
        [Min(0)]
        [Tooltip("Deceleration towards zero in the z-axis when in the recovery state")]
        private float m_recoveryDeceleration;

        public override Type ScriptType => typeof(CharacterAbility_Knockback);
        public float MinDurationSeconds => m_minDurationSeconds;
        public float MaxDurationSeconds => m_maxDurationSeconds;
        public float RecoveryDurationSeconds => m_recoveryDurationSeconds;
        public Vector3 KnockbackAdditiveVelocity => m_knockbackAdditiveVelocity;
        public float RecoveryDeceleration => m_recoveryDeceleration;
        protected override void OnValidate()
        {
            base.OnValidate();
            if (m_maxDurationSeconds < m_minDurationSeconds) m_maxDurationSeconds = m_minDurationSeconds;
        }
        public CharacterAbilityDefinition_Knockback() { }
    }
}
