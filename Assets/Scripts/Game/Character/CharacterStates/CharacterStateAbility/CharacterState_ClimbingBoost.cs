using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000366; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("ClimbingBoost")]
    [GraphNodeMenuFormat("Character/{0}")]
    public class CharacterState_ClimbingBoost : CharacterState_Climbing<CharacterAbility_MovementClimbingBoost, CharacterAbilityDefinition_MovementClimbingBoost>
    {
        // 06001556 (1.10.1 RVA 0x776090)
        protected CharacterState_ClimbingBoost(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 06001557 (1.10.1 RVA 0x776100)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_ClimbingBoost(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        // 06001558 (1.10.1 RVA 0x7761C0): entering spends the boost stamina for the ability's definition.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Character character = user.GetAs<Character>();
            CharacterAbilityDefinition_MovementClimbingBoost definition = GetAbility(character).Definition;
            character.BoostStamina.Activate(definition.AbilityType, definition.BoostDefinition);
        }

        // 06001559 (1.10.1 RVA 0x776290): a triggered boost leaves the stamina suspended for the
        // definition's suspend time. The definition is read before the trigger test.
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Character character = user.GetAs<Character>();
            CharacterAbility_MovementClimbingBoost ability = GetAbility(character);
            CharacterStamina.Definition boostDefinition = ability.Definition.BoostDefinition;
            if (ability.IsTriggered && boostDefinition.SuspendTime > 0f)
                character.BoostStamina.SuspendTimer = boostDefinition.SuspendTime;
        }
    }
}
