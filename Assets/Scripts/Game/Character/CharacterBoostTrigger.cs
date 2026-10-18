using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000736. Resets or cools down the character's boost stamina.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterBoostTrigger : CharacterTrigger
    {
        // 02000737
        private enum TriggerType
        {
            Reset = 0,
            Cancel = 1
        }

        [Tooltip("Boost type that will be triggered.")]
        [SerializeField]
        private TriggerType m_triggerType;
        [SerializeField]
        [Tooltip("If true, trigger will be fired while inside collision.")]
        private bool m_continuous;
        [Min(0)]
        [ShowIf("TriggerType", TriggerType.Cancel)]
        [Tooltip("Period of time that boost is disabled.")]
        [SerializeField]
        private float m_cooldownTime = 1f;

        // 060028ab
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            DoTrigger(character);
        }

        // 060028ac
        protected override void DoTriggerStay(Character character)
        {
            base.DoTriggerStay(character);
            if (m_continuous)
                DoTrigger(character);
        }

        // 060028ad
        private void DoTrigger(Character character)
        {
            switch (m_triggerType)
            {
                case TriggerType.Reset:
                    character.BoostStamina.Reset(false);
                    break;
                case TriggerType.Cancel:
                    character.BoostStamina.ActivateCooldown(m_cooldownTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("TriggerType", m_triggerType, null);
            }
        }

        // 060028ae
        public CharacterBoostTrigger() { }
    }
}
