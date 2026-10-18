using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000356; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterState_BurstAttack<T, TDef> : ActorState_Ability<T>
        where T : CharacterAbility_BurstAttack<TDef>
        where TDef : CharacterAbilityDefinition_BurstAttack
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected abstract class BurstAttackJSONCtorArgs : AbilityJSONCtorArgs
        {
            public int MaxTargets = 10;

            // 0600152c (1.10.1 RVA 0xE4BFA0, shared generic)
            protected BurstAttackJSONCtorArgs() { }
        }

        private readonly Collider[] m_temporaryOverlapResults;

        // 06001527 (1.10.1 RVA 0xE6BD00, shared generic)
        protected CharacterState_BurstAttack(FiniteStateMachine fsm, FSMIdentifier stateId, BurstAttackJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs)
        {
            m_temporaryOverlapResults = new Collider[ctorArgs.MaxTargets];
        }

        // 06001528 (1.10.1 RVA 0xE6BDA0, shared generic). Entry keeps only the horizontal
        // velocity, restarts the duration timer and spends the configured stamina.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Character character = user.GetAs<Character>();
            TDef definition = GetAbility(character).Definition;
            character.SetLocalVelocity(character.LocalVelocity.XZ());
            character.Storage.SetValue(ActorFSMKeys.BurstAttackDurationTimer, 0f);
            ActivateStamina(definition.CostsBoostStamina, character.BoostStamina, definition.BoostStaminaCostDefinition, character, definition);
            ActivateStamina(definition.CostsChaosStamina, character.ChaosStamina, definition.ChaosStaminaCostDefinition, character, definition);
        }

        // 06001529 (1.10.1 RVA 0xE6C040, shared generic). A drain-to-empty definition does not
        // restart while the stamina is below full; the unordered (NaN) case does activate.
        private void ActivateStamina(bool costsStamina, CharacterStamina stamina, CharacterStamina.Definition staminaDef, Character character, TDef abilityDef)
        {
            if (!costsStamina)
            {
                return;
            }
            if (staminaDef.StaminaDrainsToEmpty && stamina.Stamina < 1f)
            {
                return;
            }
            stamina.Activate(abilityDef.AbilityType, staminaDef);
            stamina.ExternalConsumeStaminaCost(staminaDef);
        }

        // 0600152a (1.10.1 RVA 0xE6C0A0, shared generic). Hits every damageable collider in the burst
        // sphere, then removes the velocity component into the ground contact while grounded.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Character character = user.GetAs<Character>();
            T ability = GetAbility(character);
            TDef definition = ability.Definition;
            int hitCount = Physics.OverlapSphereNonAlloc(ability.GetTargetPosition(), definition.RangeRadius,
                m_temporaryOverlapResults, definition.CollisionLayerMask.value, QueryTriggerInteraction.UseGlobal);
            for (int i = 0; i < hitCount; i++)
            {
                if (m_temporaryOverlapResults[i].TryGetComponent(out IPlayerDamageable damageable))
                {
                    damageable.ReceiveHit(character.ColliderCollision);
                }
            }
            character.Storage.AdjustValue(ActorFSMKeys.BurstAttackDurationTimer, updateContext.DeltaTime, 0f);
            CharacterTracking tracking = character.ActiveTracking;
            if (tracking.IsOnGround())
            {
                Vector3 contactNormal = tracking.GetContactNormal();
                Vector3 worldVelocity = character.WorldVelocity;
                character.SetWorldVelocity(worldVelocity - contactNormal * Vector3.Dot(contactNormal, worldVelocity));
                character.OrientateToPlane(contactNormal);
            }
        }

        // 0600152b (1.10.1 RVA 0xE6C480, shared generic)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Character character = user.GetAs<Character>();
            GetAbility(character).TryStartCooldown();
            character.Storage.RemoveValue<float>(ActorFSMKeys.BurstAttackDurationTimer);
        }
    }
}
