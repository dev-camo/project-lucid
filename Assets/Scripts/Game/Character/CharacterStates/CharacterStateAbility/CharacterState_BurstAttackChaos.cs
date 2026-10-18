using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200035a; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterState_BurstAttackChaos<TAbility, TAbilityDef> : CharacterState_BurstAttack<TAbility, TAbilityDef>
        where TAbility : CharacterAbility_BurstAttackChaos<TAbilityDef>
        where TAbilityDef : CharacterAbilityDefinition_BurstAttackChaos
    {
        [Serializable]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class BurstAttackChaosJSONCtorArgs : BurstAttackJSONCtorArgs
        {
            // 06001533 (1.10.1 RVA 0xE4BF80, shared generic)
            public BurstAttackChaosJSONCtorArgs() { }
        }

        // 06001530 (1.10.1 RVA 0xE6B9A0, shared generic)
        protected CharacterState_BurstAttackChaos(FiniteStateMachine fsm, FSMIdentifier stateId, BurstAttackChaosJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 06001531 (1.10.1 RVA 0xE6BA40, shared generic). The chaos stamina check is taken before
        // the base entry spends stamina. Time dilation starts only when no chaos ability
        // refuses to stop (TryStopTimeDilation false) and the stamina was valid.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            bool chaosActive;
            Character character = user.GetAs<Character>();
            TAbility ability = GetAbility(character);
            bool enoughChaosStamina = ability.EnoughChaosStamina();
            base.DoOnEnter(user, action);
            if (PreventDeactivation())
            {
                bool timeDilationActive = false;
                character.GetAbilitiesWithInterface<IAbilityBurstAttackChaos>(chaosAbility => timeDilationActive |= chaosAbility.IsTimeDilationActive());
                if (timeDilationActive)
                {
                    return;
                }
            }
            chaosActive = false;
            character.GetAbilitiesWithInterface<IAbilityBurstAttackChaos>(chaosAbility => chaosActive |= !chaosAbility.TryStopTimeDilation(false));
            if (!chaosActive && enoughChaosStamina)
            {
                ability.TriggerTimeDilation(0);
            }
        }

        // 06001532 (1.10.1 RVA 0xE6BCF0, shared generic)
        protected virtual bool PreventDeactivation()
        {
            return false;
        }
    }
}
