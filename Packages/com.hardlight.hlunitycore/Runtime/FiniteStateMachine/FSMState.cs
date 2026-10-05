using System.Collections.Generic;

namespace Hardlight
{
    // Original HLUnityCore.Runtime.dll type; bodies are derived from named
    // arm64 methods. Empty base hooks and false results are present in the native
    // release, not placeholders for the still-unrecovered concrete game states.
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMState : IFSMState
    {
        public readonly FiniteStateMachine FSM;
        private readonly List<FSMStateTransition> StateTransitions;
        // 0x060003f6/0x060003f7; arm64 0x1ac62fc/0x1ac6308.
        public int FSMId => FSM.FSMId;
        public int StateId { get; }

        // 0x060003f8; arm64 0x1ac6310. Register after assigning identity
        // and creating the transition list; registration overwrites an existing ID.
        public FSMState(FiniteStateMachine fsm, FSMIdentifier stateId)
        {
            FSM = fsm;
            StateId = stateId.Id;
            StateTransitions = new List<FSMStateTransition>();
            fsm.AddState(this);
        }

        // 0x060003f9; arm64 0x1ac641c: the base factory ignores jsonCtorArgs.
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs) => new FSMState(fsm, stateId);
        // 0x060003fa; arm64 0x1ac6498; decoded original literal is empty.
        public virtual string SerialiseRuntimeToJSON() => string.Empty;

        // 0x060003fb; arm64 0x1ac64dc. Preserve insertion order, duplicates
        // and null references; the original performs no validation here.
        public void AddTransition(IFSMTransition transition, IFSMState transitionTo) => StateTransitions.Add(new FSMStateTransition(transition, transitionTo));
        // 0x060003fc; arm64 0x1ac6614: a new read-only view of the same list.
        public IReadOnlyCollection<FSMStateTransition> GetStateTransitions() => StateTransitions.AsReadOnly();

        // 0x060003fd; arm64 0x1ac6668. Transition callbacks precede the state
        // hook. The original uses a List enumerator, including mutation checks.
        public void OnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            foreach (FSMStateTransition pair in StateTransitions) pair.Transition.OnEnter(user, action);
            DoOnEnter(user, action);
        }
        // 0x060003fe; arm64 0x1ac6838: same ordering for leave.
        public void OnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            foreach (FSMStateTransition pair in StateTransitions) pair.Transition.OnLeave(user, action);
            DoOnLeave(user, action);
        }
        // 0x060003ff; arm64 0x1ac6a08: direct virtual-hook dispatch.
        public void Update(IGraphUser user, FSMUpdateContext context) => DoUpdate(user, context);
        // 0x06000400/0x06000401; arm64 0x1ac6a18/0x1ac6a20: native false.
        public virtual bool IsEndState() => false;
        public virtual bool HasFinished(IGraphUser user) => false;

        // 0x06000402; arm64 0x1ac6a28. The first true transition wins;
        // fire receives the created action before it is returned to the caller.
        // A throwing fire callback does not return its action to the pool.
        public FSMStateChangeAction ShouldTransition(IGraphUser user, FSMUpdateContext context)
        {
            foreach (FSMStateTransition pair in StateTransitions)
            {
                if (!pair.Transition.Update(user, context)) continue;
                var action = FSMStateChangeAction.Create(FSMActionReason.TransitionFired, this, pair.ToState, pair.Transition);
                pair.Transition.OnFire(user, context, action);
                return action;
            }
            return null;
        }

        // 0x06000403; arm64 0x1ac6ca8: native null dependency list.
        public virtual List<FiniteStateMachine> GetDependencies() => null;
        // 0x06000404/0x06000405; arm64 0x1ac6cb0/0x1ac6ce8.
        public GraphStorageKey GetStorageKey(string name) => new GraphStorageKey(name, StateId, FSMId);
        public GraphStorageKey GetStorageKey(int nameId) => new GraphStorageKey(nameId, StateId, FSMId);
        // 0x06000406/0x06000407/0x06000408; arm64 0x1ac6d20/24/28: RET.
        protected virtual void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { }
        protected virtual void DoOnLeave(IGraphUser user, FSMStateChangeAction action) { }
        protected virtual void DoUpdate(IGraphUser user, FSMUpdateContext context) { }
        // 0x06000409/0x0600040a; arm64 0x1ac0e10/0x1ac2748.
        public static int ConvertNameToId(string name) => GraphNameLookup.ConvertNameToId(name);
        public static string LookupNameUsingId(int id) => GraphNameLookup.LookupNameUsingId(id);
        // 0x0600040b; arm64 0x1ac6d2c.
        public override string ToString() => GraphNameLookup.LookupNameUsingId(StateId);
    }
}
