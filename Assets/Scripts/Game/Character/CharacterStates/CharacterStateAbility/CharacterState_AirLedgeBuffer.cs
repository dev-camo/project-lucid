using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000355; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("AirLedgeBuffer")]
    [GraphNodeMenuFormat("Character/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterState_AirLedgeBuffer : CharacterState_Air
    {
        // 06001521 (1.10.1 RVA 0x775040)
        protected CharacterState_AirLedgeBuffer(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 06001522 (1.10.1 RVA 0x7750B0)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_AirLedgeBuffer(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        // 06001523 (1.10.1 RVA 0x775170)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Character character = user.GetAs<Character>();
            character.Storage.SetValue(ActorFSMKeys.AirLedgeBufferTimer, 0f);
        }

        // 06001524 (1.10.1 RVA 0x7752A0). Air control runs only while the buffer timer
        // is positive; the timer starts counting on the update after entry.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Character character = user.GetAs<Character>();
            CharacterAbility_MovementAir ability = GetAbility(character);
            float deltaTime = updateContext.DeltaTime;
            float bufferTimer = character.Storage.GetValue(ActorFSMKeys.AirLedgeBufferTimer, 0f, true);
            if (ability.UpdateEnabled && !ability.ValidateContactsActive && bufferTimer > 0f)
                CharacterMovementUtilities.ProcessAirControl(character, ability, ability.Definition, false, deltaTime, true);
            character.Storage.SetValue(ActorFSMKeys.AirLedgeBufferTimer, bufferTimer + deltaTime);
        }

        // 06001525 (1.10.1 RVA 0x775560)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Character character = user.GetAs<Character>();
            character.Storage.RemoveValue<bool>(ActorFSMKeys.AirLedgeBufferFromRail);
            character.Storage.RemoveValue<float>(ActorFSMKeys.AirLedgeBufferTimer);
        }

        // 06001526 (1.10.1 RVA 0x775740): a genuine empty body that suppresses the
        // base boost-suspend countdown.
        protected override void UpdateBoostSuspendTime(Character character, CharacterAbility_MovementAir ability, float deltaTime) { }
    }
}
