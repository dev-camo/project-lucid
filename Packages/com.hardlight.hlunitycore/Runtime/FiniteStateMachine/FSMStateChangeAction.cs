using System;
using Hardlight.Pooling;

namespace Hardlight
{
    // Original type and method identities are in HLUnityCore.Runtime.dll.
    public class FSMStateChangeAction
    {
        // 0x06000420; arm64 0x1ac71f4.
        public static int InitialObjectPoolSize = 40;

        // 0x0600040c..0x06000419: native auto-property accessors.
        public FSMActionReason Reason { get; private set; }
        public IFSMState FromState { get; private set; }
        public IFSMState ToState { get; private set; }
        public IFSMTransition TransitionThatFired { get; private set; }
        public FSMStateChangeAction ParentAction { get; private set; }
        public DateTime ActionTime { get; private set; }
        public object Context { get; private set; }

        // 0x0600041a; arm64 0x1abd10c.
        public static FSMStateChangeAction Create(FSMActionReason reason, IFSMState fromState = null,
            IFSMState toState = null, IFSMTransition transitionThatFired = null,
            FSMStateChangeAction parentAction = null, object context = null, DateTime actionTime = default(DateTime))
        {
            if (!ObjectPool<FSMStateChangeAction>.IsInitialised())
                ObjectPool<FSMStateChangeAction>.InitialisePool(InitialObjectPoolSize);
            FSMStateChangeAction action = ObjectPool<FSMStateChangeAction>.Spawn();
            action.Initialise(reason, fromState, toState, transitionThatFired, parentAction, context, actionTime);
            return action;
        }

        // 0x0600041b; arm64 0x1abcc24. Parents return to the pool first;
        // fields are deliberately retained until the next Initialise call.
        public void Destroy(bool destroyParents = false)
        {
            if (destroyParents && ParentAction != null) ParentAction.Destroy(true);
            ObjectPool<FSMStateChangeAction>.Despawn(this);
        }

        // 0x0600041c; arm64 0x1abeef0. Parent chains are deep-cloned while
        // state, transition and context references and timestamps are shared.
        public FSMStateChangeAction Clone()
        {
            FSMStateChangeAction parent = ParentAction == null ? null : ParentAction.Clone();
            return Create(Reason, FromState, ToState, TransitionThatFired, parent, Context, ActionTime);
        }

        // 0x0600041d; arm64 0x1ac6f54. Retail code performs only name
        // lookups; logging output was stripped but lookup side effects remain.
        public void ConsoleLog()
        {
            if (FromState != null) GraphNameLookup.LookupNameUsingId(FromState.StateId);
            if (ToState != null) GraphNameLookup.LookupNameUsingId(ToState.StateId);
            if (TransitionThatFired != null) GraphNameLookup.LookupNameUsingId(TransitionThatFired.TransitionId);
        }

        // 0x0600041e; arm64 0x1ac6e24. A default DateTime selects local Now.
        private void Initialise(FSMActionReason reason, IFSMState fromState = null, IFSMState toState = null,
            IFSMTransition transitionThatFired = null, FSMStateChangeAction parentAction = null,
            object context = null, DateTime actionTime = default(DateTime))
        {
            Reason = reason;
            FromState = fromState;
            ToState = toState;
            TransitionThatFired = transitionThatFired;
            ParentAction = parentAction;
            Context = context;
            ActionTime = actionTime == default(DateTime) ? DateTime.Now : actionTime;
        }
    }
}
