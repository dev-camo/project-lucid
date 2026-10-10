using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ActorState_Ability<T> : ActorState_Ability where T : ActorAbility
    {
        protected ActorState_Ability(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Actor actor = GetActor(user);
            T ability = GetAbility(actor);
            if (ability == null) return;
            ActorFormType form = ability.GetFormType();
            if (form != (ActorFormType)unchecked((int)0xb6fa1284)) actor.SwitchToForm(form);
            ability.RegisterForCollisions();
            AddAbilityInUse(actor);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Actor actor = GetActor(user);
            T ability = GetAbility(actor);
            if (ability == null) return;
            RemoveAbilityInUse(actor);
            ability.UnregisterForCollisions();
        }

        public override void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
        {
            base.DebugInfo(actor, stringInfoBuilder);
            GetAbility(actor).GetUIText(stringInfoBuilder);
        }

        protected void EnableGravity(Character character, bool enabled)
        {
            StackableDataHandle handle = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle, null, true);
            if (enabled)
            {
                if (handle == null) return;
                character.RemoveModifierOverrides(handle);
                character.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle);
            }
            else if (handle == null)
            {
                handle = character.AddModifierOverride(0x64bd855e, false);
                character.Storage.SetValue(ActorFSMKeys.GravityDisabledModifierHandle, handle);
            }
        }

        protected bool IsGravityEnabled(Character character) =>
            character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle, null, true) == null;

        protected void SetGravityMultiplier(Character character, float multiplier)
        {
            character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityMultiplierModifierHandle, null, true);
            StackableDataHandle handle = character.AddModifierOverride(unchecked((int)0xe2ed1fc7), multiplier);
            character.Storage.SetValue(ActorFSMKeys.GravityMultiplierModifierHandle, handle);
        }

        protected void AdjustGravityMultiplier(Character character, float multiplier)
        {
            StackableDataHandle handle = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityMultiplierModifierHandle, null, true);
            character.AdjustModifierOverrides(handle, unchecked((int)0xe2ed1fc7), multiplier);
        }

        protected void ClearGravityMultiplier(Character character)
        {
            StackableDataHandle handle = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityMultiplierModifierHandle, null, true);
            character.RemoveModifierOverrides(handle);
            character.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.GravityMultiplierModifierHandle);
        }

        protected virtual void OrientateToGravity(Actor actor, Vector3 forwardDirection, Vector3 gravityNormalised)
        {
            Vector3 forward = forwardDirection - gravityNormalised * Vector3.Dot(forwardDirection, gravityNormalised);
            if (forward.sqrMagnitude < 0.0001f) forward = -actor.UpDirection;
            actor.SetWorldRotation(Quaternion.LookRotation(forward, -gravityNormalised));
        }

        protected T GetAbility(Actor actor) => GetAbility<T>(actor);

        protected override ActorAnimationDefinition GetAnimationDefinition(Actor actor)
        {
            ActorAnimationDefinition animation = GetAbility(actor).GetAnimationDefinition();
            return animation != null ? animation : m_stateAnimationDefinition;
        }
    }
}
