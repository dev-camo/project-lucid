using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_Shield : CharacterAbility<CharacterAbilityDefinition_Shield>
    {
        private int m_damageHitPoints;
        private readonly List<FullscreenShaderManager.ParametersHandle> m_effectHandleInstances = new List<FullscreenShaderManager.ParametersHandle>();
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();

        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_levelManagerRef.Get().InvokeOnLevelActivated(level =>
                CoroutineUtils.OnNextFrame(() => OnLevelActivated(level)), true);
        }

        // Original06001211 captures the initial gate before decrement/callbacks.
        // The final hit therefore still reports success, and negative counts
        // remain nonzero rather than being clamped.
        public bool TryTakeDamage(DamageAction damageAction)
        {
            bool takesDamage = m_damageHitPoints != 0 && damageAction != DamageAction.None
                && damageAction != DamageAction.ReturnDamage;
            if (takesDamage)
            {
                m_damageHitPoints = unchecked(m_damageHitPoints - 1);
                m_character.Storage.SetValue(ActorFSMKeys.ShieldDamageHitPoints, m_damageHitPoints);
                if (m_damageHitPoints == 0) TriggerAnimationLeave();
            }
            return takesDamage;
        }

        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            TriggerAnimationLeave();
            m_damageHitPoints = 0;
            m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
        }

        private void OnLevelActivated(LevelManagerLevel level)
        {
            if (m_damageHitPoints > 0) return;
            if (m_character.Storage.TryGetValue(ActorFSMKeys.ShieldDamageHitPoints, out m_damageHitPoints))
            {
                m_damageHitPoints = Mathf.Min(m_damageHitPoints, Definition.DamageHitPoints);
                if (m_damageHitPoints > 0) TriggerAnimationEnter();
            }
            else
            {
                m_damageHitPoints = Definition.DamageHitPoints;
                level.Data.RegisterForIntroSequenceComplete(TriggerAnimationEnter);
            }
            m_character.Storage.SetValue(ActorFSMKeys.ShieldDamageHitPoints, m_damageHitPoints);
        }

        private void TriggerAnimationEnter() => m_character.TriggerAnimationEnter(Definition.ActivationAnimationDefinition, m_effectHandleInstances);
        private void TriggerAnimationLeave() => m_character.TriggerAnimationLeave(Definition.ActivationAnimationDefinition, m_effectHandleInstances);
        public CharacterAbility_Shield() { }
    }
}
