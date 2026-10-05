#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Pooling;

namespace ProjectLucid
{
    // Expected lifecycle order is derived from the original named arm64 base
    // methods. Test subclasses expose virtual dispatch; they are not game states.
    public static class FSMStateVerification
    {
        public static void Run()
        {
            VerifyRegistrationAndDefaults();
            VerifyLifecycleAndSelection();
            VerifyFailureAndMutation();
        }

        private static void VerifyRegistrationAndDefaults()
        {
            var fsm = Machine("defaults");
            var state = new FSMState(fsm, "state");
            var transition = new FSMTransition(fsm, "transition");
            Check(ReferenceEquals(state.FSM, fsm) && state.FSMId == fsm.FSMId, "state owner");
            Check(fsm.StateExists(state.StateId, out IFSMState found) && ReferenceEquals(found, state), "state constructor registers");
            Check(fsm.TransitionExists(transition.TransitionId, out IFSMTransition foundTransition) && ReferenceEquals(foundTransition, transition), "transition constructor registers");
            var replacement = new FSMState(fsm, "state");
            Check(fsm.States.Count == 1 && fsm.StateExists(state.StateId, out found) && ReferenceEquals(found, replacement), "state constructor overwrites ID");
            var transitionReplacement = new FSMTransition(fsm, "transition");
            Check(fsm.Transitions.Count == 1 && fsm.TransitionExists(transition.TransitionId, out foundTransition) && ReferenceEquals(foundTransition, transitionReplacement), "transition constructor overwrites ID");
            IFSMState factory = FSMState.ConstructInstance(fsm, "factory", "invalid JSON ignored by native base factory");
            Check(factory is FSMState && fsm.StateExists(factory.StateId, out found) && ReferenceEquals(found, factory), "base factory ignores constructor JSON and registers");
            Check(state.SerialiseRuntimeToJSON() == string.Empty && transition.SerialiseRuntimeToJSON() == string.Empty, "base JSON is empty string");
            var user = new FSMUser(new FSMStorage(0));
            Check(!state.HasFinished(user) && !state.IsEndState() && state.GetDependencies() == null && transition.GetDependencies() == null, "native base defaults");
            Check(!transition.Update(user, new FSMUpdateContext(1f)) && state.ShouldTransition(user, new FSMUpdateContext(1f)) == null, "no base transition fires");
            state.OnEnter(user, null); state.OnLeave(user, null); state.Update(user, new FSMUpdateContext(1f));
            transition.OnEnter(user, null); transition.OnLeave(user, null); transition.OnFire(user, new FSMUpdateContext(1f), null);
            GraphStorageKey key = state.GetStorageKey("value");
            Check(key.GraphId == fsm.FSMId && key.NodeId == state.StateId && key.NameId == GraphNameLookup.ConvertNameToId("value"), "state storage key scope");
            Check(state.GetStorageKey(key.NameId).Equals(key), "state integer key overload");
            key = transition.GetStorageKey("value");
            Check(key.GraphId == fsm.FSMId && key.NodeId == transition.TransitionId && transition.GetStorageKey(key.NameId).Equals(key), "transition storage key scope");
            Check(state.ToString() == "state" && transition.ToString() == "transition", "original ID name formatting");
            var attribute = (GraphNodeMenuFormatAttribute)Attribute.GetCustomAttribute(typeof(FSMState), typeof(GraphNodeMenuFormatAttribute));
            Check(attribute != null && attribute.Format == "Core/{0}", "original menu attribute");
            Check(Attribute.GetCustomAttribute(typeof(TraceState), typeof(GraphNodeMenuFormatAttribute)) == null, "menu attribute does not inherit");
            user.DestroyUser();
        }

        private static void VerifyLifecycleAndSelection()
        {
            var fsm = Machine("lifecycle");
            var trace = new List<string>();
            var from = new TraceState(fsm, "from", trace);
            var to = new TraceState(fsm, "to", trace);
            var first = new TraceTransition(fsm, "first", trace);
            var second = new TraceTransition(fsm, "second", trace) { Fires = true };
            var third = new TraceTransition(fsm, "third", trace) { Fires = true };
            var view = from.GetStateTransitions();
            from.AddTransition(first, to);
            from.AddTransition(second, to);
            from.AddTransition(third, from);
            Check(view.Count == 3 && !ReferenceEquals(view, from.GetStateTransitions()), "read-only views reflect list changes");
            Throws<NotSupportedException>(() => ((ICollection<FSMStateTransition>)view).Add(new FSMStateTransition(first, to)), "view prohibits mutation");
            var user = new FSMUser(new FSMStorage(0));
            var original = FSMStateChangeAction.Create(FSMActionReason.Forced);
            from.OnEnter(user, original);
            Trace(trace, "first.enter", "second.enter", "third.enter", "from.enter");
            trace.Clear();
            from.OnLeave(user, original);
            Trace(trace, "first.leave", "second.leave", "third.leave", "from.leave");
            trace.Clear();
            var context = new FSMUpdateContext(3f, FSMUpdateType.FixedUpdate);
            from.Update(user, context);
            Trace(trace, "from.update:3:FixedUpdate");
            trace.Clear();
            var action = from.ShouldTransition(user, context);
            Trace(trace, "first.check:3:FixedUpdate", "second.check:3:FixedUpdate", "second.fire");
            Check(action != null && ReferenceEquals(action.FromState, from) && ReferenceEquals(action.ToState, to) && ReferenceEquals(action.TransitionThatFired, second), "first true transition action identity");
            Check(ReferenceEquals(second.FiredAction, action) && ReferenceEquals(second.FiredUser, user) && second.FiredContext.DeltaTime == 3f && second.FiredContext.UpdateType == FSMUpdateType.FixedUpdate, "fire callback receives original context and action");
            Check(action.Reason == FSMActionReason.TransitionFired && action.ParentAction == null && action.Context == null, "native action defaults");
            Check(from.FinishChecks == 0 && from.EndChecks == 0, "transition checks do not consult completion hooks");
            action.Destroy();
            first.Fires = false; second.Fires = false; third.Fires = false;
            trace.Clear();
            Check(from.ShouldTransition(user, context) == null, "no true transition returns null");
            Trace(trace, "first.check:3:FixedUpdate", "second.check:3:FixedUpdate", "third.check:3:FixedUpdate");
            from.AddTransition(first, null);
            trace.Clear();
            from.OnEnter(user, original);
            Trace(trace, "first.enter", "second.enter", "third.enter", "first.enter", "from.enter");
            Check(view.Count == 4, "duplicate transition attachment retained");
            var nullDestination = new TraceState(fsm, "null-destination", trace);
            nullDestination.AddTransition(second, null); second.Fires = true;
            action = nullDestination.ShouldTransition(user, context);
            Check(action != null && action.ToState == null, "true transition may target null");
            action.Destroy(); original.Destroy(); user.DestroyUser();
        }

        private static void VerifyFailureAndMutation()
        {
            var fsm = Machine("failure");
            var trace = new List<string>();
            var state = new TraceState(fsm, "state", trace);
            var transition = new TraceTransition(fsm, "throw", trace) { Fires = true, ThrowOnFire = true };
            state.AddTransition(transition, state);
            var user = new FSMUser(new FSMStorage(0));
            int before = ObjectPool<FSMStateChangeAction>.UsedObjectCount;
            Throws<InvalidOperationException>(() => state.ShouldTransition(user, new FSMUpdateContext(1f)), "fire failure propagates");
            Check(ObjectPool<FSMStateChangeAction>.UsedObjectCount == before + 1, "fire failure leaves action allocated");
            transition.FiredAction.Destroy();
            var changing = new TraceState(fsm, "changing", trace);
            var mutate = new TraceTransition(fsm, "mutate", trace);
            changing.AddTransition(mutate, state);
            mutate.OnEntering = () => changing.AddTransition(transition, state);
            trace.Clear();
            Throws<InvalidOperationException>(() => changing.OnEnter(user, null), "list mutation during lifecycle follows original enumerator checks");
            Trace(trace, "mutate.enter");
            var invalid = new TraceState(fsm, "invalid", trace);
            invalid.AddTransition(null, null);
            Check(invalid.GetStateTransitions().Count == 1, "null attachment accepted");
            Throws<NullReferenceException>(() => invalid.OnEnter(user, null), "null transition fails when used");
            user.DestroyUser();
        }

        private sealed class TraceState : FSMState
        {
            private readonly string name;
            private readonly List<string> trace;
            public int FinishChecks, EndChecks;
            public TraceState(FiniteStateMachine fsm, string name, List<string> trace) : base(fsm, name) { this.name = name; this.trace = trace; }
            protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) => trace.Add(name + ".enter");
            protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action) => trace.Add(name + ".leave");
            protected override void DoUpdate(IGraphUser user, FSMUpdateContext context) => trace.Add(name + ".update:" + context.DeltaTime + ":" + context.UpdateType);
            public override bool HasFinished(IGraphUser user) { FinishChecks++; return false; }
            public override bool IsEndState() { EndChecks++; return false; }
        }
        private sealed class TraceTransition : FSMTransition
        {
            private readonly string name;
            private readonly List<string> trace;
            public bool Fires, ThrowOnFire;
            public Action OnEntering;
            public FSMStateChangeAction FiredAction;
            public IGraphUser FiredUser;
            public FSMUpdateContext FiredContext;
            public TraceTransition(FiniteStateMachine fsm, string name, List<string> trace) : base(fsm, name) { this.name = name; this.trace = trace; }
            protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { trace.Add(name + ".enter"); OnEntering?.Invoke(); }
            protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action) => trace.Add(name + ".leave");
            protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context) { trace.Add(name + ".check:" + context.DeltaTime + ":" + context.UpdateType); return Fires; }
            protected override void DoOnFire(IGraphUser user, FSMUpdateContext context, FSMStateChangeAction action)
            {
                FiredAction = action; FiredUser = user; FiredContext = context; trace.Add(name + ".fire");
                if (ThrowOnFire) throw new InvalidOperationException("verification fire failure");
            }
        }
        private static FiniteStateMachine Machine(string suffix) => new FiniteStateMachine("ProjectLucid-verification-base-" + suffix, skipAddToManager: true);
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FSM base-state verification failed: " + name);
        }
        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("FSM base-state verification failed: " + name);
        }
        private static void Trace(List<string> actual, params string[] expected) =>
            Check(string.Join("|", actual) == string.Join("|", expected), "order expected " + string.Join("|", expected) + " got " + string.Join("|", actual));
    }
}
#endif
