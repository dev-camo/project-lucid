using System.Collections;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbilityEvent : MonoBehaviour
    {
        [SerializeField, Tooltip("The ability to check exists on the character.")]
        private ActorAbilityType m_abilityType;
        [SerializeField, Tooltip("Whether the event is triggered if the character is currently invulnerable.")]
        private bool m_allowInvulnerable;
        [SerializeField, Tooltip("Event is only triggered if it is being homed to.")]
        private bool m_onlyIfHomingTo;
        [Min(0f), SerializeField, Tooltip("Event is triggered if time expires.")]
        private float m_expireTime = 1f;
        [SerializeField, Tooltip("The events to be triggered if all criteria succeeds.")]
        private UnityEvent<Character> m_onTriggerAbilityEvents;
        [SerializeField, Tooltip("The events to be triggered if criteria has subsequently not been met.")]
        private UnityEvent<Character> m_onDeactivateAbilityEvents;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private Coroutine m_coroutineTriggerAbility;

        public ActorAbilityType AbilityType => m_abilityType;

        public void TriggerAbilityWhenActive()
        {
            this.SafeStopCoroutine(ref m_coroutineTriggerAbility);
            m_coroutineTriggerAbility = StartCoroutine(TriggerAbilityWhenActiveCoroutine());
        }

        public void CancelTriggerAbility() => this.SafeStopCoroutine(ref m_coroutineTriggerAbility);

        // Original060012cb uses one elapsed-time local across both waits. The
        // coroutine handle is cleared after the trigger returns, before the
        // optional persistent deactivation listeners are inspected.
        private IEnumerator TriggerAbilityWhenActiveCoroutine()
        {
            Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
            if (!character.HasAbilityType(m_abilityType))
            {
                m_coroutineTriggerAbility = null;
                yield break;
            }
            float time = 0f;
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            while (!ActorAbilityUtilities.IsAbilityInUse(character, m_abilityType, 0.2f)
                && (time < m_expireTime || m_expireTime == 0f))
            {
                yield return waitForFixedUpdate;
                time += Time.fixedDeltaTime;
            }
            TriggerAbility();
            m_coroutineTriggerAbility = null;
            if (m_onDeactivateAbilityEvents == null || m_onDeactivateAbilityEvents.GetPersistentEventCount() <= 0)
                yield break;
            while (ActorAbilityUtilities.IsAbilityInUse(character, m_abilityType, 0.2f)
                && (time < m_expireTime || m_expireTime == 0f))
            {
                yield return waitForFixedUpdate;
                time += Time.fixedDeltaTime;
            }
            m_onDeactivateAbilityEvents?.Invoke(character);
        }

        public void TriggerAbility()
        {
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            if (!m_allowInvulnerable && character.IsInvulnerable()) return;
            if (!character.HasAbilityType(m_abilityType)) return;
            if (m_onlyIfHomingTo && character.HomingPool.ActiveTarget == null) return;
            m_onTriggerAbilityEvents?.Invoke(character);
        }

        public void TriggerAbilityTargetRestoreVelocity()
        {
            Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
            ActiveHomingTarget activeTarget = character.HomingPool.ActiveTarget;
            if (activeTarget != null) character.SetWorldVelocity(activeTarget.RestoreVelocity);
        }

        public void TriggerFindLightspeedDash()
        {
            Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
            if (character.HomingPool.ActiveTarget != null
                && character.TryGetAbility(ActorAbilityType.Character_LightspeedDash, out CharacterAbility_LightspeedDash ability))
                ability.FindClosestLightspeedDash();
        }

        public CharacterAbilityEvent() { }
    }
}
