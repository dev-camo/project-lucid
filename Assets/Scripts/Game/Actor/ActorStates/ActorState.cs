using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class ActorState : FSMState
    {
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        protected class BaseJSONCtorArgs
        {
            public string StateAnimationDefinitionName;

            // Original 0x06000417: the field retains its CLR default.
            public BaseJSONCtorArgs() { }
        }

        // Original 0x06000410: the authored args are not consumed here.
        // Registration and identity initialization belong to the genuine FSM base.
        protected ActorState(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs)
            : base(fsm, stateId) { }

        // Original 0x06000411: evaluate the class display name before the state
        // name, then append the original five pieces as a single line.
        public virtual void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
        {
            string className = GetType().ToString().Replace("HardlightProject.", "");
            stringInfoBuilder.AppendLine(string.Concat(new[] { "State = ", className, " (", ToString(), ")" }));
        }

        // Original 0x06000412: per-state fullscreen handles are resolved before
        // dispatching the actor's ordered animation/audio/PFX/effect entry path.
        protected void ActivateStateEffect(Actor actor, ActorAnimationDefinition animationDefinition)
        {
            var effectHandleInstances = actor.GetFullscreenEffectHandleInstances(StateId);
            actor.TriggerAnimationEnter(animationDefinition, effectHandleInstances);
        }

        // Original 0x06000413: retrieval occurs even though the Actor update
        // callee does not consume the resulting list in this supplied release.
        protected void UpdateStateEffect(Actor actor, ActorAnimationDefinition animationDefinition)
        {
            var effectHandleInstances = actor.GetFullscreenEffectHandleInstances(StateId);
            actor.TriggerAnimationUpdate(animationDefinition, effectHandleInstances);
        }

        // Original 0x06000414: leave receives that same per-state handle list.
        protected void DeactivateStateEffect(Actor actor, ActorAnimationDefinition animationDefinition)
        {
            var effectHandleInstances = actor.GetFullscreenEffectHandleInstances(StateId);
            actor.TriggerAnimationLeave(animationDefinition, effectHandleInstances);
        }

        // Original 0x06000415/16 only initialize native Actor type metadata and
        // return. They perform no base-hook call or actor callback in this release.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { }
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext) { }
    }
}
