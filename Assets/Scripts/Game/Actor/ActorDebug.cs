// Complete original native-derived candidate. The genuine Actor/ActorState/DataManager
// dependency graph is still open; this private source is not accepted or playable.
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ActorDebug : MonoBehaviour
    {
        public const float RenderVisibleLifetime = 5f;
        protected Actor m_actor;
        private readonly List<StateChangeInfo> m_stateHistory = new List<StateChangeInfo>();
        private const int StateHistoryMaxCount = 100;
        public const string DebugMenuPath = "Actor Info";
        public const int DebugMenuPriority = 70;
        public bool IsEnabled { get; protected set; }

        // 060003aa: only the original owner assignment.
        public virtual void Init(Actor actor) => m_actor = actor;

        // The supplied release's drawing hooks genuinely return immediately. Their
        // Conditional attributes also suppress calls outside BUILD_DEVELOPMENT.
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void OnFixedUpdate(float deltaTime) { } // 060003ab, native RET

        // 060003ac: state/class names are evaluated before the transition action.
        // Only one ParentAction is considered; this is not recursive ancestry.
        [Conditional("BUILD_DEVELOPMENT")]
        public void OnStateEnter(FSMState state, FSMStateChangeAction action)
        {
            string stateName = state.ToString();
            string className = state.GetType().ToString().Replace("HardlightProject.", "");
            IFSMTransition transition = null;
            if (action != null)
            {
                transition = action.TransitionThatFired;
                if (transition == null)
                {
                    FSMStateChangeAction parent = action.ParentAction;
                    if (parent != null)
                        transition = parent.TransitionThatFired;
                }
            }
            string transitionName = transition == null ? "None" : transition.ToString();

            StateChangeInfo info = new StateChangeInfo();
            info.TransitionName = transitionName;
            info.StateName = stateName;
            info.ClassName = className;
            info.TimeActive = 0f;
            m_stateHistory.Add(info);
            while (m_stateHistory.Count > StateHistoryMaxCount)
                m_stateHistory.RemoveAt(0);
        }

        // 060003ad: FindLast updates the most recent matching string, not necessarily
        // the newest entry. The natural captured stateName supplies the original lambda.
        [Conditional("BUILD_DEVELOPMENT")]
        public void OnStateUpdate(FSMState state, float deltaTime)
        {
            string stateName = state.ToString();
            StateChangeInfo info = m_stateHistory.FindLast(item => item.StateName == stateName);
            if (info != null)
                info.TimeActive += deltaTime;
        }

        [Conditional("BUILD_DEVELOPMENT")]
        public void GetStateInfo(FiniteStateMachine fsm, StringBuilder stringInfoBuilder) // 060003ae
        {
            if (fsm == null)
                return;
            ActorState state = GetActorState(fsm.GetActiveState(m_actor));
            if (state != null)
                state.DebugInfo(m_actor, stringInfoBuilder);
            else
                stringInfoBuilder.Append("None");
        }

        [Conditional("BUILD_DEVELOPMENT")]
        public void GetStateInfo(FiniteStateMachine fsm, StringBuilder stringInfoBuilder, IGraphUser user) // 060003af
        {
            if (fsm == null)
                return;
            ActorState state = GetActorState(fsm.GetActiveState(user));
            if (state != null)
                state.DebugInfo(m_actor, stringInfoBuilder);
            else
                stringInfoBuilder.Append("None");
        }

        // 060003b0: the fsm argument is unused. Repeated transition strings collapse
        // reverse-chronologically; a first null transition matches the initial null.
        [Conditional("BUILD_DEVELOPMENT")]
        public void GetFsmInfo(FiniteStateMachine fsm, StringBuilder stringInfoBuilder)
        {
            string lastTransition = null;
            int repeatedCount = 0;
            for (int i = m_stateHistory.Count - 1; i >= 0; --i)
            {
                StateChangeInfo info = m_stateHistory[i];
                if (lastTransition == info.TransitionName)
                {
                    ++repeatedCount;
                    stringInfoBuilder.Append(info.ClassName + " (" + info.StateName + ") ");
                }
                else
                {
                    lastTransition = info.TransitionName;
                    if (repeatedCount >= 1)
                        stringInfoBuilder.AppendLine();
                    stringInfoBuilder.AppendLine();
                    stringInfoBuilder.AppendLine(string.Format("{0} -> {1} ({2}) {3}s",
                        info.TransitionName, info.ClassName, info.StateName, info.TimeActive));
                    repeatedCount = 0;
                }
            }
        }

        [Conditional("BUILD_DEVELOPMENT")]
        public void ClearFsmInfo() => m_stateHistory.Clear(); // 060003b1

        private ActorState GetActiveState(FiniteStateMachine fsm) =>
            GetActorState(fsm.GetActiveState(m_actor)); // 060003b2

        private ActorState GetActiveState(FiniteStateMachine fsm, IGraphUser user) =>
            GetActorState(fsm.GetActiveState(user)); // 060003b3

        // 060003b4: direct ActorState first, then group children. The original walks
        // sub-FSM children recursively but does not recurse into nested group children.
        private ActorState GetActorState(IFSMState state)
        {
            if (state is ActorState actorState)
                return actorState;
            if (state is FSMStateGroup group)
            {
                foreach (IFSMState child in group.States)
                {
                    if (child is ActorState childActorState)
                        return childActorState;
                    if (child is FSMStateSubFSM)
                    {
                        ActorState nestedState = GetActorState(child);
                        if (nestedState != null)
                            return nestedState;
                    }
                }
            }
            if (state is FSMStateSubFSM subFSM)
                return GetActorState(subFSM.SubFSM.GetActiveState(m_actor));
            return null;
        }

        protected abstract string ContainerIdentifierName { get; } // 060003b5, genuine contract

        // 060003b6: manager resolution is UI then definitions; enabled changes before
        // the genuine Close/GetOrCreate callback. A missing dictionary key is not repaired.
        public virtual void ToggleDebugInfo()
        {
            UIManager uiManager = ProcessManager.GetSystem<UIManager>();
            DataManager dataManager = ProcessManager.GetSystem<DataManager>();
            dataManager.Containers.TryGetValue(ContainerIdentifierName, out UIContainerIdentifier identifier);
            bool isOpen = uiManager.IsOpen(identifier);
            IsEnabled = !isOpen;
            if (isOpen)
                uiManager.Close(identifier);
            else
                uiManager.GetOrCreate(identifier);
        }

        // 060003b7: retain the unusual +0.001f display offset and the two original
        // StateHistory reads. The first read occurs before the fsm-null guard.
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void GetActorInfo(FiniteStateMachine fsm, StringBuilder stringInfoBuilder)
        {
            stringInfoBuilder.AppendLine("Name = " + m_actor.ActorName);
            TimeCategory category = m_actor.GetTimeCategory();
            TimeManager timeManager = ProcessManager.GetSystem<TimeManager>();
            stringInfoBuilder.AppendLine(string.Format("Unity TimeScale = {0:F2}, Actor TimeScale = {1:F2}",
                Time.timeScale + 0.001f, timeManager.GetTimescale(category) + 0.001f));

            FSMStorage storage = m_actor.Storage as FSMStorage;
            if (storage != null)
            {
                IReadOnlyDictionary<int, Queue<FSMStateChangeAction>> firstHistory = storage.StateHistory;
                if (fsm != null && firstHistory != null &&
                    storage.StateHistory.TryGetValue(fsm.FSMId, out Queue<FSMStateChangeAction> history) && history.Count > 0)
                {
                    FSMStateChangeAction action = history.Peek();
                    if (action != null && action.TransitionThatFired != null)
                        stringInfoBuilder.AppendLine("Transition = " + action.TransitionThatFired.ToString());
                }
            }
        }

        public virtual Vector3 GetDisplayVelocity() => Vector3.zero; // 060003b8, original constant return

        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void Close() => IsEnabled = false; // 060003b9

        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void AddLocationTimestamp(Rigidbody body, FiniteStateMachine fsm, bool forceUpdate = false) { } // 060003ba
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void BeginLocationInstantiation(bool forceUpdate = false) { } // 060003bb
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void EndLocationInstantiation(bool forceUpdate = false) { } // 060003bc
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void SetThrottleDebugInfo(float minSpeed, float maxSpeed, float acceleration, float deceleration) { } // 060003bd
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void SetMovementDebugInfo(float targetSpeed, float scaledSpeed, float accelerationMultiplier, float effectiveInputMagnitude) { } // 060003be
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void SetTurningDebugInfo(float intendedTurnDelta, float byVelocity, float byAngle, float rateOfTurn) { } // 060003bf
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void DrawRay(Vector3 position, Vector3 direction, Color colour) { } // 060003c0

        public virtual GameObject GetLocationParent() => null; // 060003c1, genuine null return

        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void AddLocationInstantiation(Vector3 position, Quaternion rotation, string namePostfix,
            Vector3? scale = null, GameObject parent = null, float lifetime = RenderVisibleLifetime,
            CharacterDebug_Metadata.Metadata data = null, bool unique = false) { } // 060003c2
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void AddLocationObject(GameObject gameObject, string name, Vector3? scale = null,
            GameObject parent = null, float lifetime = RenderVisibleLifetime,
            CharacterDebug_Metadata.Metadata data = null, bool unique = false) { } // 060003c3
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void AddLocationLine(Vector3 start, Vector3 end, string namePostfix, GameObject parent = null,
            float lifetime = RenderVisibleLifetime, CharacterDebug_Metadata.Metadata data = null, bool unique = false) { } // 060003c4
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void AddLocationCube(Vector3 centre, Vector3 size, string namePostfix,
            CharacterDebug_Metadata.Metadata data = null) { } // 060003c5
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void RemoveLocation(GameObject locationObject) { } // 060003c6
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void RemoveLocation(GameObject gameObject, GameObject parent) { } // 060003c7
        [Conditional("BUILD_DEVELOPMENT")]
        public virtual void RemoveLocationParent(GameObject locationParent) { } // 060003c8

        protected ActorDebug() { } // 060003c9: readonly list initializes before MonoBehaviour base

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class StateChangeInfo // original02000053, no Serializable attribute
        {
            public string TransitionName;
            public string StateName;
            public string ClassName;
            public float TimeActive;
            public StateChangeInfo() { } // 060003ca: genuine base-only constructor
        }
    }
}
