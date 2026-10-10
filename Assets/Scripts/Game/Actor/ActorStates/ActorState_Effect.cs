using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Actor/{0}")]
    public class ActorState_Effect : ActorState
    {
        protected readonly ActorAnimationDefinition m_stateAnimationDefinition;
        private readonly string m_compositeId;

        // Original0600042b: the discarded App lookup precedes DataManager access.
        protected ActorState_Effect(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs)
        {
            string name = ctorArgs.StateAnimationDefinitionName;
            if (!string.IsNullOrEmpty(name))
            {
                ProcessManager.GetSystemRef<App>().Get();
                ProcessManager.GetSystemSafe<DataManager>().ActorStateAnimationDefinitions.TryGetValue(name,
                    out m_stateAnimationDefinition);
            }
            m_compositeId = string.Format("{0}_{1}", FSMId, StateId);
        }

        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            BaseJSONCtorArgs args = JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs);
            return new ActorState_Effect(fsm, transitionId, args);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            Actor actor = GetActor(user);
            Dictionary<string, ActorAnimationDefinition> lookup = actor.GetActiveAnimationLookup();
            ActorAnimationDefinition animation = GetAnimationDefinition(actor);
            actor.TriggerAnimationEnter(animation, actor.GetFullscreenEffectHandleInstances(StateId));
            lookup.Add(m_compositeId, animation);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            Actor actor = GetActor(user);
            ActorAnimationDefinition animation = GetAnimationDefinition(actor);
            Dictionary<string, ActorAnimationDefinition> lookup = actor.GetActiveAnimationLookup();
            if (lookup.TryGetValue(m_compositeId, out ActorAnimationDefinition previous))
            {
                if (previous == animation)
                {
                    actor.TriggerAnimationUpdate(animation, actor.GetFullscreenEffectHandleInstances(StateId));
                    return;
                }
                actor.TriggerAnimationLeave(previous, actor.GetFullscreenEffectHandleInstances(StateId));
            }
            // Original publishes before the enter callback; no fault cleanup.
            lookup[m_compositeId] = animation;
            actor.TriggerAnimationEnter(animation, actor.GetFullscreenEffectHandleInstances(StateId));
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Actor actor = GetActor(user);
            Dictionary<string, ActorAnimationDefinition> lookup = actor.GetActiveAnimationLookup();
            if (!lookup.TryGetValue(m_compositeId, out ActorAnimationDefinition animation)) return;
            actor.TriggerAnimationLeave(animation, actor.GetFullscreenEffectHandleInstances(StateId));
            lookup.Remove(m_compositeId);
        }

        protected void SetBossMovementSpeed(ActorAnimator animator, float speed) =>
            animator.TrySetFromAnimationType(m_stateAnimationDefinition, (ActorAnimationType)unchecked((int)0x8d0dfab4), speed);

        protected void SetBossMovementDirections(ActorAnimator animator, float xMovement, float zMovement)
        {
            animator.TrySetFromAnimationType(m_stateAnimationDefinition, (ActorAnimationType)0x7fdd921c, xMovement);
            animator.TrySetFromAnimationType(m_stateAnimationDefinition, (ActorAnimationType)0x7b284221, zMovement);
        }

        protected void SetAnimationBool(ActorAnimator animator, ActorAnimationType animType, bool setBool) =>
            animator.TrySetFromAnimationType(m_stateAnimationDefinition, animType, setBool);

        protected virtual Actor GetActor(IGraphUser user) => user.GetAs<Actor>();
        protected virtual ActorAnimationDefinition GetAnimationDefinition(Actor actor) => m_stateAnimationDefinition;
    }
}
