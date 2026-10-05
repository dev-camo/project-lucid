#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Synthetic factories exercise original construction ordering and failure
    // boundaries; these fixtures are not replacements for authored game states.
    public static class FSMConstructionVerification
    {
        private static readonly List<string> Trace = new List<string>();

        public static void Run()
        {
            VerifyMetadata();
            VerifyDiscoveryAndHelpers();
            VerifyRetryOrder();
            VerifyFatalErrorsAndLimits();
            VerifyDefaultsAndRouting();
            VerifyJSONAndRegistration();
            Debug.Log("Original synchronous FSM construction source-based verification passed. Asynchronous loading and authored gameplay remain unresolved.");
        }

        private static void VerifyMetadata()
        {
            var position = new Vector2(1.5f, -2f);
            var guid = new Guid("7b242c46-b29a-4db9-82af-bba9b1b42831");
            var node = new NodeMetadata(position, guid, 3, 8);
            Check(node.Position == position && node.Guid == guid.ToString() && node.InDirection == 3 && node.OutDirection == 8 && !node.Flipped, "node constructor stores values");
            node.Guid = guid.ToString("N").ToUpperInvariant();
            Check(node.GetGuid() == guid && node.Guid == guid.ToString("N").ToUpperInvariant(), "valid GUID text preserved");
            node.Guid = "invalid";
            Guid generated = node.GetGuid();
            Check(generated != Guid.Empty && node.Guid == generated.ToString() && node.GetGuid() == generated, "invalid GUID replaced once");
            var states = new[] { node };
            var transitions = new[] { new NodeMetadata(default, guid, 0, 0) };
            var routing = new FSMClassFactory.NodeMetadataWithRouting(position, guid, states, transitions, -1, 9);
            Check(ReferenceEquals(routing.StateRoutingMetadata, states) && ReferenceEquals(routing.TransitionRoutingMetadata, transitions) && routing.InDirection == -1 && routing.OutDirection == 9, "routing arrays retained");
            var colour = new Color(0.2f, 0.3f, 0.4f, 0.5f);
            var background = new BackgroundMetadata(position, guid, "title", Vector2.one, colour, true);
            Check(background.Title == "title" && background.Size == Vector2.one && background.Colour == colour && background.MoveOverlaidNodes && background.InDirection == 0 && background.OutDirection == 0 && !background.Flipped, "background values and zero directions");
            var view = new ViewMetadata();
            Check(view.Translation == Vector2.zero && view.Zoom == 1f, "view constructor defaults");
            view = new ViewMetadata(position, -4f);
            Check(view.Translation == position && view.Zoom == -4f, "view preserves explicit zoom");
            var dto = new FSMClassFactory.JSONFiniteStateMachineClass();
            Check(dto.Name == null && dto.States == null && dto.Transitions == null && !dto.DoMultipleTransitions && !dto.UpdateTransientStates, "DTO constructor creates no lists or options");
            Check(new FSMClassFactory.JSONMultipleFSMs().FSMs == null && new FSMClassFactory.JSONStateClass().StateTransitions == null && new FSMClassFactory.JSONStateTransition().NodeMetadata == null && new FSMClassFactory.JSONTransitionClass().Class == null, "remaining DTO constructors retain native defaults");
        }

        private static void VerifyDiscoveryAndHelpers()
        {
            FSMClassFactory.Initialise();
            Check(ReferenceEquals(FSMClassFactory.States, FSMClassFactory.States), "states expose original lookup");
            Check(ReferenceEquals(FSMClassFactory.Transitions, FSMClassFactory.Transitions), "transitions expose original lookup");
            Check(FSMClassFactory.States[nameof(FSMState)].ClassType == typeof(FSMState), "assembly scan discovers original base state");
            Check(FSMClassFactory.States[nameof(FactoryState)].ClassType == typeof(FactoryState) && FSMClassFactory.Transitions[nameof(FactoryTransition)].ClassType == typeof(FactoryTransition), "assembly scan discovers explicit static factories");
            var fsm = Machine();
            Trace.Clear();
            var state = FSMClassFactory.ConstructState(fsm, nameof(FactoryState), "raw-state", "raw:{");
            var transition = FSMClassFactory.ConstructTransition(fsm, nameof(FactoryTransition), "raw-transition", "raw:[");
            Check(state.StateId == GraphNameLookup.ConvertNameToId("raw-state") && transition.TransitionId == GraphNameLookup.ConvertNameToId("raw-transition"), "boxed identifiers retain names");
            Check(Trace.SequenceEqual(new[] { "state:raw-state:raw:{", "transition:raw-transition:raw:[" }), "construction forwards raw JSON and argument order");
            int count = fsm.States.Count;
            var unregistered = FSMClassFactory.ConstructState(fsm, nameof(ContractState), "unregistered", "no-register");
            Check(unregistered != null && fsm.States.Count == count, "factory helper does not add unregistered result");
        }

        private static void VerifyRetryOrder()
        {
            var fsm = Machine();
            var json = Setup();
            json.DoMultipleTransitions = false;
            json.UpdateTransientStates = true;
            json.States.Add(State("dependent", "require:ready"));
            json.States.Add(State("ready"));
            json.Transitions.Add(Transition("transition", "require:dependent"));
            json.DefaultState = "dependent";
            json.States[0].StateTransitions = new List<FSMClassFactory.JSONStateTransition> { Link("transition", "ready"), Link("transition", "ready") };
            Trace.Clear();
            var errors = new List<string>();
            Check(ReferenceEquals(FSMClassFactory.ConstructFSM(json, errors, fsm), fsm), "dependency retries finish");
            Check(errors.Count == 0 && Trace.SequenceEqual(new[] { "state:dependent:require:ready", "state:ready:", "transition:transition:require:dependent", "state:dependent:require:ready", "transition:transition:require:dependent" }), "pending state then transition order across passes");
            Check(!fsm.DoMultipleTransitions && fsm.UpdateTransientStates && ReferenceEquals(fsm.DefaultState, fsm.States[GraphNameLookup.ConvertNameToId("dependent")]), "supplied machine options and default assigned");
            Check(fsm.DefaultState.GetStateTransitions().Count == 2, "authored duplicate links retained");
            var user = new FSMUser(new FSMStorage(0));
            fsm.InitialiseUser(user);
            Check(ReferenceEquals(fsm.GetActiveState(user), fsm.DefaultState), "constructed default uses original interpreter initialization");
            user.DestroyUser();
        }

        private static void VerifyFatalErrorsAndLimits()
        {
            var json = Setup();
            json.States.Add(State("never", "null"));
            json.Transitions.Add(Transition("never-transition", "null"));
            var fsm = Machine();
            var errors = new List<string>();
            Trace.Clear();
            Check(FSMClassFactory.ConstructFSM(json, errors, fsm) == null && Trace.Count == 20, "ten passes for null state and transition");
            Check(Trace.Where((value, index) => index % 2 == 0).All(value => value == "state:never:null") && Trace.Where((value, index) => index % 2 != 0).All(value => value == "transition:never-transition:null"), "retry queues preserve per-pass ordering");
            Check(errors.SequenceEqual(new[] { "Failed to create state 'FactoryState - never'. Check dependencies are setup correctly.", "Failed to create transition 'FactoryTransition - never-transition'. Check dependencies are setup correctly." }), "only final null pass adds dependency errors");

            json = Setup();
            json.States.Add(State("broken", "throw"));
            json.States.Add(State("remaining-state"));
            json.Transitions.Add(Transition("broken-transition", "throw"));
            json.Transitions.Add(Transition("remaining-transition"));
            errors.Clear();
            Trace.Clear();
            fsm = Machine();
            Check(FSMClassFactory.ConstructFSM(json, errors, fsm) == null && Trace.Count == 4, "exceptions abort future passes only");
            Check(fsm.StateExists("remaining-state", out _) && fsm.TransitionExists("remaining-transition", out _), "remaining current-pass callbacks run and mutations remain");
            Check(errors.Count == 3 && errors[0].StartsWith("Exception when creating state 'FactoryState - broken'. '", StringComparison.Ordinal) && errors[1].StartsWith("Exception when creating transition 'FactoryTransition - broken-transition'. '", StringComparison.Ordinal) && errors[2] == "transition body failure", "transition inner exception added after outer errors");
            Check(!errors.Contains("state body failure"), "state catch reports outer reflection message only");

            json = Setup();
            json.States.Add(new FSMClassFactory.JSONStateClass { Class = "MissingFactory", Name = "missing" });
            json.States.Add(State("after-missing"));
            errors.Clear();
            Trace.Clear();
            Check(FSMClassFactory.ConstructFSM(json, errors, Machine()) == null && errors.Count == 1 && Trace.Count == 1, "unregistered class exception is fatal rather than retried");
            Check(errors[0] == "Exception when creating state 'MissingFactory - missing'. 'Failed to construct with class name 'MissingFactory' as no class with that name is registered in the class-factory.'.", "original missing-class diagnostic format");
        }

        private static void VerifyDefaultsAndRouting()
        {
            var fsm = Machine();
            var existing = new FSMState(fsm, "existing");
            fsm.DefaultState = existing;
            var json = Setup();
            int originalId = fsm.FSMId;
            Check(ReferenceEquals(FSMClassFactory.ConstructFSM(json, null, fsm), fsm) && fsm.FSMId == originalId && ReferenceEquals(fsm.DefaultState, existing), "supplied identity/dictionaries and empty default retained");
            json.Name = "";
            var errors = new List<string>();
            Check(FSMClassFactory.ConstructFSM(json, errors, fsm) == null && errors.Single() == "Failed to construct FSM as the name is null or empty.", "empty name rejects even supplied machine");
            json.Name = " ";
            Check(ReferenceEquals(FSMClassFactory.ConstructFSM(json, null, fsm), fsm), "whitespace name accepted");

            json = Setup();
            json.States.Add(State("source"));
            json.DefaultState = "absent-default";
            errors.Clear();
            Throws<KeyNotFoundException>(() => FSMClassFactory.ConstructFSM(json, errors, Machine()));
            Check(errors.Single() == "Failed to find default state 'absent-default'.", "missing default log precedes escaping indexer");

            json = Setup();
            json.States.Add(State("source"));
            json.States.Add(State("destination"));
            json.Transitions.Add(Transition("ok"));
            json.States[0].StateTransitions = new List<FSMClassFactory.JSONStateTransition> { Link("ok", "destination"), Link("missing-transition", "missing-destination"), Link("ok", "destination") };
            errors.Clear();
            fsm = Machine();
            Check(FSMClassFactory.ConstructFSM(json, errors, fsm) == null, "routing references fail result");
            Check(errors.SequenceEqual(new[] { "Failed to find state 'missing-destination' when creating state transitions for state 'source'.", "Failed to find transition 'missing-transition' when creating state transitions for state 'source'." }), "both routing failures reported in order");
            Check(fsm.States[GraphNameLookup.ConvertNameToId("source")].GetStateTransitions().Count == 1, "earlier links kept and later valid links suppressed");

            json = Setup();
            json.States.Add(new FSMClassFactory.JSONStateClass { Class = nameof(ContractState), Name = "not-registered", CtorArgs = "no-register", StateTransitions = new List<FSMClassFactory.JSONStateTransition>() });
            Throws<KeyNotFoundException>(() => FSMClassFactory.ConstructFSM(json, null, Machine()));
            json.States[0].StateTransitions = null;
            Check(FSMClassFactory.ConstructFSM(json, null, Machine()) != null, "null links skip source lookup, empty links do not");
            json = Setup();
            json.States.Add(new FSMClassFactory.JSONStateClass { Class = nameof(ContractState), Name = "throwing-link", CtorArgs = "link-throw", StateTransitions = new List<FSMClassFactory.JSONStateTransition> { Link("ok", "destination") } });
            json.States.Add(State("destination"));
            json.Transitions.Add(Transition("ok"));
            Check(Throws<InvalidOperationException>(() => FSMClassFactory.ConstructFSM(json, null, Machine())).Message == "link attachment failure", "AddTransition failure escapes outer constructor catch");
        }

        private static void VerifyJSONAndRegistration()
        {
            var errors = new List<string>();
            Check(FSMClassFactory.ConstructFSM("{broken", errors, Machine()) == null && errors.Count == 1, "JSON parse error reported once");
            string valid = "{\"Name\":\"parsed\",\"DefaultState\":\"source\",\"DoMultipleTransitions\":true,\"Transitions\":[{\"Class\":\"FactoryTransition\",\"Name\":\"ok\",\"CtorArgs\":\"raw:[\"}],\"States\":[{\"Class\":\"FactoryState\",\"Name\":\"source\",\"CtorArgs\":\"raw:{\",\"StateTransitions\":[{\"Transition\":\"ok\",\"ToState\":\"destination\"}]},{\"Class\":\"FactoryState\",\"Name\":\"destination\"}]}";
            errors.Clear();
            var fsm = Machine();
            Check(ReferenceEquals(FSMClassFactory.ConstructFSM(valid, errors, fsm), fsm) && errors.Count == 0 && fsm.DefaultState.GetStateTransitions().Count == 1, "Unity JSON parses original field names and raw constructor text");
            string missingDefault = valid.Replace("\"DefaultState\":\"source\"", "\"DefaultState\":\"absent\"");
            Throws<KeyNotFoundException>(() => FSMClassFactory.ConstructFSM(missingDefault, null, Machine()));
            var missingLists = Setup();
            missingLists.States = null;
            Throws<ArgumentNullException>(() => FSMClassFactory.ConstructFSM(missingLists, null, Machine()));

            var setup = Setup();
            setup.Name = "Lucid-factory-registered-" + Guid.NewGuid();
            var manager = FSMManager.GetManager();
            FiniteStateMachine registered = FSMClassFactory.ConstructFSM(setup);
            try
            {
                Check(registered != null && manager.TryGetFSM(setup.Name, out FiniteStateMachine stored) && ReferenceEquals(registered, stored), "new machine registers with original manager");
                errors.Clear();
                Check(FSMClassFactory.ConstructFSM(setup, errors) == null && errors.Count == 1 && errors[0].Contains(setup.Name), "new-machine duplicate error is caught and reported");
            }
            finally { if (registered != null) manager.ReleaseFSM(registered.FSMId); }
        }

        private static FiniteStateMachine Machine() => new FiniteStateMachine("Lucid-factory-provided-" + Guid.NewGuid(), skipAddToManager: true);
        private static FSMClassFactory.JSONFiniteStateMachineClass Setup() => new FSMClassFactory.JSONFiniteStateMachineClass { Name = "factory-input", States = new List<FSMClassFactory.JSONStateClass>(), Transitions = new List<FSMClassFactory.JSONTransitionClass>() };
        private static FSMClassFactory.JSONStateClass State(string name, string args = null) => new FSMClassFactory.JSONStateClass { Class = nameof(FactoryState), Name = name, CtorArgs = args };
        private static FSMClassFactory.JSONTransitionClass Transition(string name, string args = null) => new FSMClassFactory.JSONTransitionClass { Class = nameof(FactoryTransition), Name = name, CtorArgs = args };
        private static FSMClassFactory.JSONStateTransition Link(string transition, string destination) => new FSMClassFactory.JSONStateTransition { Transition = transition, ToState = destination };

        public sealed class FactoryState : FSMState
        {
            private FactoryState(FiniteStateMachine fsm, FSMIdentifier id) : base(fsm, id) { }
            public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier id, string json)
            {
                Trace.Add("state:" + id.Name + ":" + json);
                if (json == "null") return null;
                if (json == "throw") throw new InvalidOperationException("state body failure");
                if (json != null && json.StartsWith("require:", StringComparison.Ordinal) && !fsm.StateExists(json.Substring(8), out _)) return null;
                return new FactoryState(fsm, id);
            }
        }

        public sealed class FactoryTransition : FSMTransition
        {
            private FactoryTransition(FiniteStateMachine fsm, FSMIdentifier id) : base(fsm, id) { }
            public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier id, string json)
            {
                Trace.Add("transition:" + id.Name + ":" + json);
                if (json == "null") return null;
                if (json == "throw") throw new InvalidOperationException("transition body failure");
                if (json != null && json.StartsWith("require:", StringComparison.Ordinal) && !fsm.StateExists(json.Substring(8), out _)) return null;
                return new FactoryTransition(fsm, id);
            }
        }

        // Test-only contract fixture for absent registration and throwing links.
        public sealed class ContractState : IFSMState
        {
            public int FSMId { get; }
            public int StateId { get; }
            private readonly bool throwsOnLink;
            private ContractState(FiniteStateMachine fsm, FSMIdentifier id, string json)
            {
                FSMId = fsm.FSMId; StateId = id.Id; throwsOnLink = json == "link-throw";
                if (json != "no-register") fsm.AddState(this);
            }
            public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier id, string json) => new ContractState(fsm, id, json);
            public void AddTransition(IFSMTransition transition, IFSMState destination) { if (throwsOnLink) throw new InvalidOperationException("link attachment failure"); }
            public void OnEnter(IGraphUser user, FSMStateChangeAction action) { }
            public void OnLeave(IGraphUser user, FSMStateChangeAction action) { }
            public void Update(IGraphUser user, FSMUpdateContext context) { }
            public bool HasFinished(IGraphUser user) => false;
            public bool IsEndState() => false;
            public FSMStateChangeAction ShouldTransition(IGraphUser user, FSMUpdateContext context) => null;
            public IReadOnlyCollection<FSMStateTransition> GetStateTransitions() => Array.Empty<FSMStateTransition>();
            public string SerialiseRuntimeToJSON() => "";
            public List<FiniteStateMachine> GetDependencies() => null;
        }

        private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("FSM construction verification failed: " + label); }
        private static TException Throws<TException>(Action action) where TException : Exception
        {
            try { action(); } catch (TException exception) { return exception; }
            throw new InvalidOperationException("FSM construction verification failed: expected " + typeof(TException).Name);
        }
    }
}
#endif
