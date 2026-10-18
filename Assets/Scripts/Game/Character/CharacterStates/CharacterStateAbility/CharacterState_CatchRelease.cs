using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000364; bodies translated from the 1.10.1 x86-64 binary.
    [GraphNodeDefaultName("CatchRelease")]
    [GraphNodeMenuFormat("Character/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterState_CatchRelease : CharacterState_Ability<CharacterAbility_CatchRelease>
    {
        // 06001542 (1.10.1 RVA 0x775D20)
        protected CharacterState_CatchRelease(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 06001543 (1.10.1 RVA 0x775D90)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_CatchRelease(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        // 06001544 (1.10.1 RVA 0x775E50). The visual proxy stops smoothing while the ability drives the
        // character to the catch point.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Character character = user.GetAs<Character>();
            GetAbility(character).OnAttach();
            character.VisualProxy.ToggleSmoothing(false);
            character.TriggerBurstAttackAir(false);
            character.UpdateConnectionTrackingValues();
        }

        // 06001545 (1.10.1 RVA 0x775F10)
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Character character = user.GetAs<Character>();
            GetAbility(character).OnUpdate(updateContext.DeltaTime);
            UpdateRemainTimer(character, updateContext.DeltaTime);
        }

        // 06001546 (1.10.1 RVA 0x775FE0)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Character character = user.GetAs<Character>();
            CharacterAbility_CatchRelease ability = GetAbility(character);
            character.VisualProxy.ToggleSmoothing(true);
            ability.OnRelease();
        }
    }
}
