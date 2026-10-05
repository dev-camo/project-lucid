#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Pooling;
using UnityEngine;

namespace ProjectLucid
{
    // Expected traces come from the original named native methods. These are
    // source-based checks and do not establish original-game equivalence.
    public static class FSMInterpreterVerification
    {
        public static void Run()
        {
            VerifyStorage();
            VerifyPool();
            VerifyActions();
            VerifyUpdateOrdering();
            VerifyInitialiseClearAndHistory();
            VerifyOwnershipAndDependencies();
            FSMManagerVerification.Run();
            FSMStateVerification.Run();
            JSONClassFactoryVerification.Run();
            FSMConstructionVerification.Run();
            FSMFileLoadingVerification.Run();
            Debug.Log("FSM interpreter, manager, construction and file loading source-based verification passed. ScriptableObject acquisition and game-specific states remain unresolved.");
        }

        private static void VerifyStorage()
        {
            var storage = new FSMStorage(0);
            GraphStorageKey key = new GraphStorageKey("Lucid-verification-value");
            Check(storage.GetCollection().Count == 0 && storage.StateHistory == null && storage.StateHistoryMaxCount == 0, "disabled history");
            Check(storage.GetValue(key, 7, false) == 7 && storage.GetCollection().Count == 0, "read without default storage");
            Check(storage.GetValue(key, 8) == 8, "store default");
            var wrapper = (IGraphStorage.Var<int>)storage.GetCollection()[key];
            storage.SetValue(key, 12);
            Check(ReferenceEquals(wrapper, storage.GetCollection()[key]) && wrapper.Value == 12, "value wrapper reused");
            Throws<InvalidCastException>(() => storage.SetValue(key, 1f), "wrong value wrapper fails");
            storage.RemoveValue<string>(key);
            Check(storage.GetCollection().Count == 0, "remove ignores generic type");
            object reference = new object();
            storage.SetValue(key, reference);
            Check(ReferenceEquals(storage.GetCollection()[key], reference) && ReferenceEquals(storage.GetValue<object>(key), reference), "reference stored directly");
            storage.SetValue<object>(key, null);
            Check(storage.GetValue<object>(key, reference) == null, "stored null takes precedence");
            storage.Clear();
            var suppliedUser = new FSMUser(storage);
            Check(ReferenceEquals(suppliedUser.Storage, storage), "user retains supplied storage");
            suppliedUser.Storage.SetValue(key, 42);
            suppliedUser.DestroyUser();
            Check(ReferenceEquals(suppliedUser.Storage, storage) && storage.GetCollection().Count == 0, "destroy user clears but retains storage");
            var defaultUser = new FSMUser();
            Check(defaultUser.Storage is FSMStorage && ((FSMStorage)defaultUser.Storage).StateHistoryMaxCount == 5, "user creates default history storage");
            defaultUser.DestroyUser();
        }

        public sealed class PoolProbe { public PoolProbe() { } }

        private static void VerifyPool()
        {
            if (!ObjectPool<PoolProbe>.IsInitialised())
            {
                Check(ObjectPool<PoolProbe>.UsedObjectCount == 0, "uninitialized count");
                ObjectPool<PoolProbe>.Despawn(null);
                ObjectPool<PoolProbe>.DespawnEntirePool();
                Throws<NullReferenceException>(() => ObjectPool<PoolProbe>.Spawn(), "spawn requires initialization");
                ObjectPool<PoolProbe>.InitialisePool(2);
            }
            ObjectPool<PoolProbe>.DespawnEntirePool();
            PoolProbe first = ObjectPool<PoolProbe>.Spawn();
            PoolProbe second = ObjectPool<PoolProbe>.Spawn();
            PoolProbe third = ObjectPool<PoolProbe>.Spawn();
            Check(!ReferenceEquals(first, second) && !ReferenceEquals(second, third), "pool grows when empty");
            ObjectPool<PoolProbe>.Despawn(new PoolProbe());
            Check(ObjectPool<PoolProbe>.UsedObjectCount == 3, "unknown despawn ignored");
            ObjectPool<PoolProbe>.Despawn(second);
            ObjectPool<PoolProbe>.Despawn(second);
            Check(ObjectPool<PoolProbe>.UsedObjectCount == 2 && ReferenceEquals(ObjectPool<PoolProbe>.Spawn(), second), "duplicate despawn and LIFO reuse");
            ObjectPool<PoolProbe>.DespawnEntirePool();
            Check(ReferenceEquals(ObjectPool<PoolProbe>.Spawn(), second), "entire pool pushes used list in order");
            Check(ReferenceEquals(ObjectPool<PoolProbe>.Spawn(), third), "entire pool reuse order");
            PoolProbe toClear = ObjectPool<PoolProbe>.Spawn();
            ObjectPool<PoolProbe>.DespawnAndNullify(ref toClear);
            Check(toClear == null && ObjectPool<PoolProbe>.UsedObjectCount == 2, "despawn and nullify");
            ObjectPool<PoolProbe>.DespawnEntirePool();
        }

        private static void VerifyActions()
        {
            int before = ObjectPool<FSMStateChangeAction>.UsedObjectCount;
            DateTime fixedTime = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            object context = new object();
            var grandparent = FSMStateChangeAction.Create(FSMActionReason.InitialiseUser, context: context, actionTime: fixedTime);
            var parent = FSMStateChangeAction.Create(FSMActionReason.Forced, parentAction: grandparent, context: context, actionTime: fixedTime);
            var action = FSMStateChangeAction.Create(FSMActionReason.TransitionFired, parentAction: parent, context: context, actionTime: fixedTime);
            var clone = action.Clone();
            Check(ObjectPool<FSMStateChangeAction>.UsedObjectCount == before + 6, "clone allocates complete parent chain");
            Check(!ReferenceEquals(clone, action) && !ReferenceEquals(clone.ParentAction, parent) && !ReferenceEquals(clone.ParentAction.ParentAction, grandparent), "parent chain deep clone");
            Check(ReferenceEquals(clone.Context, context) && clone.ActionTime == fixedTime && clone.ParentAction.Reason == FSMActionReason.Forced, "clone shares context and timestamp");
            action.Destroy(true);
            Check(ReferenceEquals(action.ParentAction, parent) && ObjectPool<FSMStateChangeAction>.UsedObjectCount == before + 3, "destroy parents without clearing fields");
            var reused = FSMStateChangeAction.Create(FSMActionReason.ClearUser, actionTime: fixedTime);
            Check(ReferenceEquals(reused, action) && reused.ParentAction == null && reused.Context == null, "parent-first destruction and field reset");
            reused.Destroy();
            clone.Destroy(true);
            DateTime lower = DateTime.Now;
            var now = FSMStateChangeAction.Create(FSMActionReason.Forced);
            Check(now.ActionTime >= lower && now.ActionTime <= DateTime.Now, "default timestamp selects local Now");
            now.Destroy();
            Check(ObjectPool<FSMStateChangeAction>.UsedObjectCount == before, "action pool balanced");
        }

        private static void VerifyUpdateOrdering()
        {
            var trace = new List<string>();
            var fsm = Machine("ordering");
            var user = new User(new FSMStorage(0));
            var a = new State("A", fsm, trace);
            var b = new State("B", fsm, trace);
            var c = new State("C", fsm, trace);
            fsm.DefaultState = a;
            Check(fsm.DoMultipleTransitions && !fsm.UpdateTransientStates, "constructor flags");
            fsm.Update(user, new FSMUpdateContext(2f));
            Check(trace.Count == 0 && user.Storage.GetCollection().Count == 1, "empty active state is stored and update stops");
            fsm.InitialiseUser(user);
            trace.Clear();
            a.Next = b; b.Next = c;
            fsm.UpdateTransientStates = true;
            fsm.Update(user, new FSMUpdateContext(2f, FSMUpdateType.FixedUpdate));
            Trace(trace, "A.check:2:FixedUpdate", "A.leave", "B.enter", "B.update:0:FixedUpdate", "B.check:0:FixedUpdate", "B.leave", "C.enter", "C.update:0:FixedUpdate", "C.check:0:FixedUpdate", "C.update:2:FixedUpdate");
            Check(ReferenceEquals(fsm.GetActiveState(user), c), "final active state");

            fsm.InitialiseUser(user, toState: a); trace.Clear();
            fsm.DoMultipleTransitions = false;
            fsm.Update(user, new FSMUpdateContext(2f));
            Trace(trace, "A.check:2:Update", "A.leave", "B.enter", "B.update:2:Update");

            fsm.InitialiseUser(user, toState: a); trace.Clear();
            fsm.DoMultipleTransitions = true; b.Next = a;
            fsm.Update(user, new FSMUpdateContext(2f));
            Trace(trace, "A.check:2:Update", "A.leave", "B.enter", "B.update:0:Update", "B.check:0:Update", "B.leave", "A.enter", "A.update:2:Update");

            fsm.UpdateTransientStates = false; b.Next = c;
            trace.Clear();
            fsm.Update(user, new FSMUpdateContext(2f));
            Trace(trace, "A.check:2:Update", "A.leave", "B.enter", "B.check:0:Update", "B.leave", "C.enter", "C.check:0:Update", "C.update:2:Update");

            fsm.InitialiseUser(user, toState: a); trace.Clear();
            var d = new State("D", fsm, trace);
            var forced = FSMStateChangeAction.Create(FSMActionReason.Forced, d, c);
            fsm.SetActiveState(user, forced);
            Trace(trace, "D.leave", "C.enter");
            forced.Destroy();

            fsm.InitialiseUser(user, toState: a); trace.Clear(); a.Next = null; a.ReturnNullDestination = true;
            int used = ObjectPool<FSMStateChangeAction>.UsedObjectCount;
            fsm.Update(user, new FSMUpdateContext(2f));
            Trace(trace, "A.check:2:Update", "A.update:2:Update");
            Check(ObjectPool<FSMStateChangeAction>.UsedObjectCount == used + 1, "action with null destination is not destroyed by Update");
            a.LastAction.Destroy();
        }

        private static void VerifyInitialiseClearAndHistory()
        {
            var trace = new List<string>();
            var fsm = Machine("history");
            var storage = new FSMStorage(2);
            var user = new User(storage);
            var other = new User(new FSMStorage(0));
            var a = new State("A", fsm, trace);
            var b = new State("B", fsm, trace);
            fsm.DefaultState = a; fsm.UpdateTransientStates = true;
            int before = ObjectPool<FSMStateChangeAction>.UsedObjectCount;
            fsm.InitialiseUser(user);
            Trace(trace, "A.enter", "A.update:0:InitialiseUser");
            Check(fsm.GetActiveState(other) == null, "active state belongs to user storage");
            var history = storage.StateHistory[fsm.FSMId];
            Check(history.Count == 1 && history.Peek().Reason == FSMActionReason.InitialiseUser, "initial action cloned into history");
            var context = new object();
            var parent = FSMStateChangeAction.Create(FSMActionReason.Forced, context: context);
            var transition = FSMStateChangeAction.Create(FSMActionReason.TransitionFired, toState: b, parentAction: parent, context: context);
            fsm.SetActiveState(user, transition);
            transition.Destroy(true);
            Check(history.Count == 2, "history bounded");
            var snapshot = history.ToArray()[1];
            Check(!ReferenceEquals(snapshot, transition) && !ReferenceEquals(snapshot.ParentAction, parent) && ReferenceEquals(snapshot.Context, context), "history owns independent action chain");
            trace.Clear(); fsm.ClearUser(user);
            Trace(trace, "B.leave");
            Check(b.LastLeaveAction == null && fsm.GetActiveState(user) == null, "clear passes caller null action");
            Check(history.Count == 2, "native identifier comparison skips removal history");
            storage.ClearStateHistory();
            Check(history.Count == 0 && storage.StateHistory.ContainsKey(fsm.FSMId), "clear history retains empty queues");
            Check(ObjectPool<FSMStateChangeAction>.UsedObjectCount == before, "history returns parent clones to pool");
            storage.Clear();
            Check(storage.GetCollection().Count == 0 && storage.StateHistoryMaxCount == 0 && storage.StateHistory == null, "clear does not reinitialize history");
            storage.Initialise();
            Check(storage.StateHistoryMaxCount == 2 && storage.StateHistory.Count == 0, "explicit reinitialize");
        }

        private static void VerifyOwnershipAndDependencies()
        {
            var trace = new List<string>();
            var fsm = Machine("ownership");
            var dependency = Machine("dependency");
            var first = new State("same", fsm, trace) { Dependencies = new List<FiniteStateMachine> { dependency, dependency } };
            var replacement = new State("same", fsm, trace) { Dependencies = first.Dependencies };
            fsm.AddState(first); fsm.AddState(replacement);
            Check(fsm.States.Count == 1 && fsm.StateExists(first.StateId, out IFSMState found) && ReferenceEquals(found, replacement), "state overwrite uses indexer");
            var firstTransition = new Transition(31, new List<FiniteStateMachine> { fsm });
            var replacementTransition = new Transition(31, firstTransition.Dependencies);
            fsm.AddTransition(firstTransition); fsm.AddTransition(replacementTransition);
            Check(fsm.Transitions.Count == 1 && fsm.TransitionExists(31, out IFSMTransition foundTransition) && ReferenceEquals(foundTransition, replacementTransition), "transition overwrite uses indexer");
            var dependencies = fsm.GetDependencies();
            Check(dependencies.Count == 3 && ReferenceEquals(dependencies[0], dependency) && ReferenceEquals(dependencies[1], dependency) && ReferenceEquals(dependencies[2], fsm), "dependencies retain duplicates and state-before-transition order");
            var pair = new FSMStateTransition(firstTransition, first);
            Check(ReferenceEquals(pair.Transition, firstTransition) && ReferenceEquals(pair.ToState, first), "transition pair");
        }

        private static FiniteStateMachine Machine(string suffix) => new FiniteStateMachine("Lucid-verification-" + suffix, skipAddToManager: true);
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FSM interpreter verification failed: " + name);
        }
        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("FSM interpreter verification failed: " + name);
        }
        private static void Trace(List<string> actual, params string[] expected)
        {
            string result = string.Join("|", actual);
            string wanted = string.Join("|", expected);
            Check(result == wanted, "native event ordering: expected " + wanted + " got " + result);
        }

        private sealed class User : IGraphUser
        {
            public IGraphStorage Storage { get; }
            public User(IGraphStorage storage) { Storage = storage; }
            public void DestroyUser() { Storage.Clear(); }
        }
        private sealed class State : IFSMState
        {
            private readonly string name;
            private readonly FiniteStateMachine fsm;
            private readonly List<string> trace;
            public IFSMState Next;
            public bool ReturnNullDestination;
            public FSMStateChangeAction LastAction;
            public FSMStateChangeAction LastLeaveAction;
            public List<FiniteStateMachine> Dependencies;
            public int FSMId => fsm.FSMId;
            public int StateId { get; }
            public State(string name, FiniteStateMachine fsm, List<string> trace)
            {
                this.name = name; this.fsm = fsm; this.trace = trace;
                StateId = GraphNameLookup.ConvertNameToId("Lucid-verification-state-" + name);
            }
            public void OnEnter(IGraphUser user, FSMStateChangeAction action)
            {
                Check(ReferenceEquals(fsm.GetActiveState(user), this), "storage assigned before OnEnter");
                trace.Add(name + ".enter");
            }
            public void OnLeave(IGraphUser user, FSMStateChangeAction action) { LastLeaveAction = action; trace.Add(name + ".leave"); }
            public void Update(IGraphUser user, FSMUpdateContext context) { trace.Add(name + ".update:" + context.DeltaTime + ":" + context.UpdateType); }
            public FSMStateChangeAction ShouldTransition(IGraphUser user, FSMUpdateContext context)
            {
                trace.Add(name + ".check:" + context.DeltaTime + ":" + context.UpdateType);
                LastAction = Next != null || ReturnNullDestination ? FSMStateChangeAction.Create(FSMActionReason.TransitionFired, this, Next) : null;
                return LastAction;
            }
            public bool HasFinished(IGraphUser user) => false;
            public bool IsEndState() => false;
            public void AddTransition(IFSMTransition transition, IFSMState transitionTo) { throw new NotSupportedException("verification fake"); }
            public IReadOnlyCollection<FSMStateTransition> GetStateTransitions() => Array.Empty<FSMStateTransition>();
            public string SerialiseRuntimeToJSON() => "{}";
            public List<FiniteStateMachine> GetDependencies() => Dependencies;
        }
        private sealed class Transition : IFSMTransition
        {
            public int FSMId => 0;
            public int TransitionId { get; }
            public List<FiniteStateMachine> Dependencies;
            public Transition(int id, List<FiniteStateMachine> dependencies) { TransitionId = id; Dependencies = dependencies; }
            public void OnEnter(IGraphUser user, FSMStateChangeAction action) { }
            public void OnLeave(IGraphUser user, FSMStateChangeAction action) { }
            public void OnFire(IGraphUser user, FSMUpdateContext context, FSMStateChangeAction action) { }
            public bool Update(IGraphUser user, FSMUpdateContext context) => false;
            public string SerialiseRuntimeToJSON() => "{}";
            public List<FiniteStateMachine> GetDependencies() => Dependencies;
        }
    }
}
#endif
