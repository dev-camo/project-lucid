using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hardlight
{
    // Original synchronous construction path. Multi-FSM/includes/file loading
    // and graph serialization remain unrecovered and are not exposed here.
    public static partial class FSMClassFactory
    {
        [Serializable]
        public class NodeMetadataWithRouting : NodeMetadata
        {
            public NodeMetadata[] StateRoutingMetadata;
            public NodeMetadata[] TransitionRoutingMetadata;
            // HLUnityCore.Runtime.dll:Hardlight.FSMClassFactory+NodeMetadataWithRouting:0x060003b1;
            // arm64 0x1ac2bec.
            public NodeMetadataWithRouting(Vector2 position, Guid guid, NodeMetadata[] stateRoutingMetadata,
                NodeMetadata[] transitionRoutingMetadata, int inDirection, int outDirection)
                : base(position, guid, inDirection, outDirection)
            {
                StateRoutingMetadata = stateRoutingMetadata;
                TransitionRoutingMetadata = transitionRoutingMetadata;
            }
        }

        [Serializable]
        public class JSONStateTransition
        {
            public string Transition;
            public string ToState;
            public NodeMetadataWithRouting NodeMetadata;
            // Original token 0x060003b2; arm64 0x1ac27d8 calls Object's ctor only.
            public JSONStateTransition() { }
        }

        [Serializable]
        public class JSONTransitionClass
        {
            public string Class;
            public string Name;
            public string CtorArgs;
            public NodeMetadata NodeMetadata;
            // Original token 0x060003b3; arm64 0x1ac2868.
            public JSONTransitionClass() { }
        }

        [Serializable]
        public class JSONStateClass
        {
            public string Class;
            public string Name;
            public string CtorArgs;
            public List<JSONStateTransition> StateTransitions;
            public NodeMetadata NodeMetadata;
            // Original token 0x060003b4; arm64 0x1ac27d0.
            public JSONStateClass() { }
        }

        [Serializable]
        public class JSONFiniteStateMachineClass
        {
            public string Name;
            public string DefaultState;
            public bool DoMultipleTransitions;
            public bool UpdateTransientStates;
            public List<JSONTransitionClass> Transitions;
            public List<JSONStateClass> States;
            public NodeMetadata NodeMetadata;
            public ViewMetadata ViewMetadata;
            public BackgroundMetadata[] BackgroundMetadata;
            // Original token 0x060003b5; arm64 0x1ac2740.
            public JSONFiniteStateMachineClass() { }
        }

        [Serializable]
        public class JSONMultipleFSMs
        {
            public List<string> Includes;
            public List<JSONFiniteStateMachineClass> FSMs;
            public ViewMetadata ViewMetadata;
            // Original token 0x060003b6; arm64 0x1ac179c.
            public JSONMultipleFSMs() { }
        }

        private static readonly Dictionary<string, JSONFactoryClass> s_states;
        private static readonly Dictionary<string, JSONFactoryClass> s_transitions;

        // Original tokens 0x0600039f/0x060003a0; arm64 0x1abfc34/0x1abfcb0.
        public static IReadOnlyDictionary<string, JSONFactoryClass> States => s_states;
        public static IReadOnlyDictionary<string, JSONFactoryClass> Transitions => s_transitions;

        // Original token 0x060003a1; arm64 0x1abfd2c. Discover the assemblies
        // present on first access; no later assembly-load subscription is added.
        static FSMClassFactory()
        {
            s_states = new Dictionary<string, JSONFactoryClass>();
            s_transitions = new Dictionary<string, JSONFactoryClass>();
            InitialiseAllAssemblies();
        }

        // Original token 0x060003a2; arm64 0x1abff24 is RET. The type
        // initializer performs discovery before this method's empty retail body.
        public static void Initialise() { }

        // Original token 0x060003a3; arm64 0x1abfe78.
        private static void InitialiseAllAssemblies()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) InitialiseFromAssembly(assembly);
        }

        // Original token 0x060003a4; arm64 0x1abff28. GetTypes occurs
        // outside the per-type catch. Failed state caching also skips transition
        // caching for that type, and discovery continues with the next type.
        private static void InitialiseFromAssembly(Assembly assembly)
        {
            Type[] types = assembly.GetTypes();
            using (ScopedStringBuilder scope = HLOutput.GetLogScopedStringBuilder())
            {
                foreach (Type type in types)
                {
                    try
                    {
                        s_states.TryCacheFactoryType<IFSMState>(type, scope);
                        s_transitions.TryCacheFactoryType<IFSMTransition>(type, scope);
                    }
                    catch (Exception) { }
                }
            }
        }

        // Original token 0x060003a5; arm64 0x1ac01b0. Registration is the
        // constructed state's responsibility, not an extra step in this helper.
        public static IFSMState ConstructState(FiniteStateMachine fsm, string className, FSMIdentifier stateId, string jsonCtorArgs)
        {
            return s_states.ConstructInstance<IFSMState>(className, fsm, stateId, jsonCtorArgs);
        }

        // Original token 0x060003a6; arm64 0x1ac0298.
        public static IFSMTransition ConstructTransition(FiniteStateMachine fsm, string className, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return s_transitions.ConstructInstance<IFSMTransition>(className, fsm, transitionId, jsonCtorArgs);
        }

        // Original token 0x060003a7; arm64 0x1ac0380, LSDA 0x2a1e93c.
        // Only FromJson is inside the catch; DTO construction errors propagate.
        public static FiniteStateMachine ConstructFSM(string json, List<string> errors = null, FiniteStateMachine fsm = null, bool isOverwriteOk = false)
        {
            JSONFiniteStateMachineClass setup;
            try { setup = JsonUtility.FromJson<JSONFiniteStateMachineClass>(json); }
            catch (Exception exception)
            {
                HLOutput.LogError(exception.Message);
                if (errors != null) errors.Add(exception.Message);
                return null;
            }
            return ConstructFSM(setup, errors, fsm, isOverwriteOk);
        }

        // Original token 0x060003a8; arm64 0x1ac0520, LSDA 0x2a1e97c.
        // Ten passes retry only null factories, states before transitions.
        // Factory errors retain partial mutations; there is no rollback.
        public static FiniteStateMachine ConstructFSM(JSONFiniteStateMachineClass json, List<string> errors = null,
            FiniteStateMachine fsm = null, bool isOverwriteOk = false)
        {
            if (string.IsNullOrEmpty(json.Name))
            {
                const string error = "Failed to construct FSM as the name is null or empty.";
                HLOutput.LogError(error);
                if (errors != null) errors.Add(error);
                return null;
            }
            if (fsm == null)
            {
                try { fsm = new FiniteStateMachine(new FSMIdentifier(json.Name), isOverwriteOk); }
                catch (Exception exception)
                {
                    HLOutput.LogError(exception.Message);
                    if (errors != null) errors.Add(exception.Message);
                    return null;
                }
            }

            fsm.DoMultipleTransitions = json.DoMultipleTransitions;
            fsm.UpdateTransientStates = json.UpdateTransientStates;
            bool finishedCreation = false;
            bool errorOccurred = false;
            var statesAwaitingCreation = new List<JSONStateClass>(json.States);
            var transitionsAwaitingCreation = new List<JSONTransitionClass>(json.Transitions);
            for (int pass = 0; !finishedCreation && !errorOccurred && pass < 10; ++pass)
            {
                finishedCreation = true;
                bool finalLoop = pass == 9;
                var states = new List<JSONStateClass>(statesAwaitingCreation);
                statesAwaitingCreation = new List<JSONStateClass>();
                // Original closure token 0x060003ba; arm64 0x1ac2e84,
                // LSDA 0x2a1ebd4 includes null-error logging and queue insertion.
                states.ForEach(stateSetup =>
                {
                    try
                    {
                        if (ConstructState(fsm, stateSetup.Class, new FSMIdentifier(stateSetup.Name), stateSetup.CtorArgs) == null)
                        {
                            finishedCreation = false;
                            if (finalLoop)
                            {
                                string error = "Failed to create state '" + stateSetup.Class + " - " + stateSetup.Name + "'. Check dependencies are setup correctly.";
                                HLOutput.LogError(error);
                                if (errors != null) errors.Add(error);
                                errorOccurred = true;
                            }
                            else statesAwaitingCreation.Add(stateSetup);
                        }
                    }
                    catch (Exception exception)
                    {
                        string error = "Exception when creating state '" + stateSetup.Class + " - " + stateSetup.Name + "'. '" + exception.Message + "'.";
                        HLOutput.LogError(error);
                        if (errors != null) errors.Add(error);
                        errorOccurred = true;
                    }
                });
                var transitions = new List<JSONTransitionClass>(transitionsAwaitingCreation);
                transitionsAwaitingCreation = new List<JSONTransitionClass>();
                // Original closure token 0x060003bb; arm64 0x1ac33f0,
                // LSDA 0x2a1ecb4. Unlike state errors, include an inner message.
                transitions.ForEach(transitionSetup =>
                {
                    try
                    {
                        if (ConstructTransition(fsm, transitionSetup.Class, new FSMIdentifier(transitionSetup.Name), transitionSetup.CtorArgs) == null)
                        {
                            finishedCreation = false;
                            if (finalLoop)
                            {
                                string error = "Failed to create transition '" + transitionSetup.Class + " - " + transitionSetup.Name + "'. Check dependencies are setup correctly.";
                                HLOutput.LogError(error);
                                if (errors != null) errors.Add(error);
                                errorOccurred = true;
                            }
                            else transitionsAwaitingCreation.Add(transitionSetup);
                        }
                    }
                    catch (Exception exception)
                    {
                        string error = "Exception when creating transition '" + transitionSetup.Class + " - " + transitionSetup.Name + "'. '" + exception.Message + "'.";
                        HLOutput.LogError(error);
                        if (errors != null) errors.Add(error);
                        if (exception.InnerException != null)
                        {
                            string innerMessage = exception.InnerException.Message;
                            HLOutput.LogError(innerMessage);
                            if (errors != null) errors.Add(innerMessage);
                        }
                        errorOccurred = true;
                    }
                });
            }

            if (errorOccurred) return null;
            if (!string.IsNullOrEmpty(json.DefaultState))
            {
                int defaultState = GraphNameLookup.ConvertNameToId(json.DefaultState);
                if (!fsm.States.ContainsKey(defaultState))
                {
                    string error = "Failed to find default state '" + json.DefaultState + "'.";
                    HLOutput.LogError(error);
                    if (errors != null) errors.Add(error);
                    errorOccurred = true;
                }
                // The native path still indexes after the missing-key error.
                // This exception is outside the constructor-only catch.
                fsm.DefaultState = fsm.States[defaultState];
            }

            if (errorOccurred) return null;
            // Original closure tokens 0x060003b8/0x060003bd;
            // arm64 0x1ac2c44/0x1ac39c8. No node metadata is consumed here.
            json.States.ForEach(stateSetup =>
            {
                if (stateSetup.StateTransitions == null) return;
                IFSMState state = fsm.States[GraphNameLookup.ConvertNameToId(stateSetup.Name)];
                stateSetup.StateTransitions.ForEach(link =>
                {
                    int destination = GraphNameLookup.ConvertNameToId(link.ToState);
                    if (!fsm.States.ContainsKey(destination))
                    {
                        string error = "Failed to find state '" + link.ToState + "' when creating state transitions for state '" + stateSetup.Name + "'.";
                        HLOutput.LogError(error);
                        if (errors != null) errors.Add(error);
                        errorOccurred = true;
                    }
                    int transition = GraphNameLookup.ConvertNameToId(link.Transition);
                    if (!fsm.Transitions.ContainsKey(transition))
                    {
                        string error = "Failed to find transition '" + link.Transition + "' when creating state transitions for state '" + stateSetup.Name + "'.";
                        HLOutput.LogError(error);
                        if (errors != null) errors.Add(error);
                        errorOccurred = true;
                    }
                    if (errorOccurred) return;
                    IFSMState toState = fsm.States[destination];
                    IFSMTransition fsmTransition = fsm.Transitions[transition];
                    state.AddTransition(fsmTransition, toState);
                });
            });
            return errorOccurred ? null : fsm;
        }
    }
}
