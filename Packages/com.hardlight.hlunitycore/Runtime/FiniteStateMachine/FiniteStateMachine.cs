using System;
using System.Collections.Generic;
using Hardlight.Pooling;
using Unity.Profiling;

namespace Hardlight
{
    // HLUnityCore.Runtime.dll:Hardlight.FiniteStateMachine. Bodies reconstructed
    // from named arm64 native functions, including original manager registration.
    public class FiniteStateMachine
    {
        public readonly int FSMId;
        private readonly Dictionary<int, IFSMState> m_ownedStates;
        private readonly Dictionary<int, IFSMTransition> m_ownedTransitions;
        private readonly ProfilerMarker m_profilerMarker;
        // 0x06000365..0x0600036c: auto-properties and dictionary views.
        public bool DoMultipleTransitions { get; set; }
        public bool UpdateTransientStates { get; set; }
        public IFSMState DefaultState { get; set; }
        public IReadOnlyDictionary<int, IFSMState> States => m_ownedStates;
        public IReadOnlyDictionary<int, IFSMTransition> Transitions => m_ownedTransitions;

        // 0x0600036d; arm64 0x1abbadc. Retail Update does not begin/end this marker.
        public FiniteStateMachine(FSMIdentifier fsmId, bool isOverwriteOk = false, bool skipAddToManager = false)
        {
            FSMId = fsmId.Id;
            DoMultipleTransitions = true;
            UpdateTransientStates = false;
            m_ownedStates = new Dictionary<int, IFSMState>();
            m_ownedTransitions = new Dictionary<int, IFSMTransition>();
            m_profilerMarker = new ProfilerMarker("FSM: " + fsmId.Name);
            if (!skipAddToManager)
                FSMManager.GetManager().AddFSM(this, isOverwriteOk);
        }

        // 0x0600036e; arm64 0x1abc168: dictionary indexer overwrites an existing ID.
        public void AddState(IFSMState state) => m_ownedStates[state.StateId] = state;
        // 0x0600036f; arm64 0x1abc23c.
        public bool StateExists(FSMIdentifier stateIdentifier, out IFSMState state) => m_ownedStates.TryGetValue(stateIdentifier.Id, out state);
        // 0x06000370; arm64 0x1abc31c.
        public void AddTransition(IFSMTransition transition) => m_ownedTransitions[transition.TransitionId] = transition;
        // 0x06000371; arm64 0x1abc3f0.
        public bool TransitionExists(FSMIdentifier transitionIdentifier, out IFSMTransition transition) => m_ownedTransitions.TryGetValue(transitionIdentifier.Id, out transition);

        // 0x06000372; arm64 0x1abc4d0..0x1abc840. Subsequent transition
        // checks use zero delta; the final state always receives the original context.
        public void Update(IGraphUser user, FSMUpdateContext context)
        {
            IFSMState initialState = this.GetActiveState(user.Storage);
            if (initialState == null) return;
            IFSMState currentState = initialState;
            float transitionDeltaTime = context.DeltaTime;
            while (true)
            {
                FSMStateChangeAction action = currentState.ShouldTransition(user, new FSMUpdateContext(transitionDeltaTime, context.UpdateType));
                if (action == null || action.ToState == null) break;
                IFSMState nextState = action.ToState;
                SetActiveState(user, action);
                ObjectPool<FSMStateChangeAction>.Despawn(action);
                currentState = nextState;
                if (!DoMultipleTransitions) break;
                if (UpdateTransientStates && nextState != initialState)
                    nextState.Update(user, new FSMUpdateContext(0f, context.UpdateType));
                transitionDeltaTime = 0f;
                if (nextState == initialState) break;
            }
            currentState.Update(user, context);
        }

        // 0x06000373; arm64 0x1abc990: leave, storage/history, then enter.
        // Explicit FromState takes precedence over the stored active state.
        public void SetActiveState(IGraphUser user, FSMStateChangeAction action)
        {
            IFSMState oldState = action.FromState;
            if (oldState == null) oldState = this.GetActiveState(user.Storage);
            if (oldState != null) oldState.OnLeave(user, action);
            this.SetActiveState(user.Storage, action);
            if (action.ToState != null) action.ToState.OnEnter(user, action);
        }

        // 0x06000374; arm64 0x1abce38.
        public IFSMState GetActiveState(IGraphUser user) => this.GetActiveState(user.Storage);

        // 0x06000375; arm64 0x1abcf2c.
        public void InitialiseUser(IGraphUser user, FSMStateChangeAction action = null, IFSMState toState = null)
        {
            IFSMState state = toState ?? DefaultState;
            var initialisation = FSMStateChangeAction.Create(FSMActionReason.InitialiseUser, toState: state, parentAction: action);
            SetActiveState(user, initialisation);
            initialisation.Destroy();
            if (UpdateTransientStates && state != null)
                state.Update(user, new FSMUpdateContext(0f, FSMUpdateType.InitialiseUser));
        }

        // 0x06000376; arm64 0x1abd2ac. OnLeave receives the caller's action,
        // including null, before a separate ClearUser action is allocated.
        public void ClearUser(IGraphUser user, FSMStateChangeAction action = null)
        {
            IFSMState state = GetActiveState(user);
            if (state != null) state.OnLeave(user, action);
            var removal = FSMStateChangeAction.Create(FSMActionReason.ClearUser, parentAction: action);
            this.RemoveActiveState(user.Storage, removal);
            removal.Destroy();
        }

        // 0x06000377; arm64 0x1abd6ec: states first, transitions second.
        // Append nonempty lists as-is; the retail code does not deduplicate.
        public List<FiniteStateMachine> GetDependencies()
        {
            var result = new List<FiniteStateMachine>();
            foreach (KeyValuePair<int, IFSMState> entry in m_ownedStates)
            {
                List<FiniteStateMachine> dependencies = entry.Value.GetDependencies();
                if (dependencies != null && dependencies.Count > 0) result.AddRange(dependencies);
            }
            foreach (KeyValuePair<int, IFSMTransition> entry in m_ownedTransitions)
            {
                List<FiniteStateMachine> dependencies = entry.Value.GetDependencies();
                if (dependencies != null && dependencies.Count > 0) result.AddRange(dependencies);
            }
            return result;
        }

        // 0x06000378..0x0600037a; native delegates to GraphNameLookup.
        public static int ConvertNameToId(string name) => GraphNameLookup.ConvertNameToId(name);
        public static string LookupNameUsingId(int id) => GraphNameLookup.LookupNameUsingId(id);
        public override string ToString() => GraphNameLookup.LookupNameUsingId(FSMId);
    }
}
