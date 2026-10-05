using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;

namespace HardlightProject
{
    public sealed class FSMStateLoader
    {
        // Game.Runtime.dll:HardlightProject.FSMStateLoader:0x0600398b..0x0600398e;
        // arm64 0x68bb4c/54/5c/64. Original readonly automatic properties.
        public List<FiniteStateMachineScriptableObject> LoadedStateMachines { get; }
        private List<FiniteStateMachineScriptableObject> m_loadingStateMachines { get; }
        private IEnumerable<FiniteStateMachineScriptableObject> m_stateMachines { get; }
        private Action m_onStateMachinesLoaded { get; }

        // Original token 0x0600398f; arm64 0x68bb6c. Keep supplied enumerable
        // live; lists start empty and no loading occurs inside the constructor.
        public FSMStateLoader(IEnumerable<FiniteStateMachineScriptableObject> stateMachines, Action onStateMachinesLoaded)
        {
            LoadedStateMachines = new List<FiniteStateMachineScriptableObject>();
            m_loadingStateMachines = new List<FiniteStateMachineScriptableObject>();
            m_stateMachines = stateMachines;
            m_onStateMachinesLoaded = onStateMachinesLoaded;
        }
        // Original token 0x06003990; arm64 0x68bc4c. No host construction,
        // duplicate-load guard, handle capture or error catch is present.
        public void LoadStates() => CoroutineUtils.RunCoroutine(LoadStatesCoroutine());

        // Original token 0x06003991; wrapper 0x68bcc8; iterator MoveNext
        // 0x06003995 at 0x68bf08. Gather unique unresolved machines first,
        // dispose the source enumerator, then load pending list in reverse order.
        private IEnumerator LoadStatesCoroutine()
        {
            foreach (FiniteStateMachineScriptableObject stateMachine in m_stateMachines)
                if (stateMachine.FSM == null && !m_loadingStateMachines.Contains(stateMachine)) m_loadingStateMachines.Add(stateMachine);
            if (m_loadingStateMachines.Count == 0)
            {
                m_onStateMachinesLoaded?.Invoke();
                yield break;
            }
            for (int index = m_loadingStateMachines.Count - 1; index >= 0; --index)
            {
                FiniteStateMachineScriptableObject stateMachine = m_loadingStateMachines[index];
                if (stateMachine.FSM != null) OnStateMachineLoaded(stateMachine);
                else
                {
                    stateMachine.OnInitialisationComplete += OnStateMachineLoaded;
                    yield return stateMachine.AcquireFSM();
                }
            }
        }

        // Original token 0x06003992; arm64 0x68bd6c. Remove pending, append
        // unique loaded, unsubscribe, then notify when the live pending list is
        // empty. A failed FSM selection still follows this callback route.
        private void OnStateMachineLoaded(FiniteStateMachineScriptableObject stateMachineScriptableObject)
        {
            m_loadingStateMachines.Remove(stateMachineScriptableObject);
            LoadedStateMachines.AddUnique(stateMachineScriptableObject);
            stateMachineScriptableObject.OnInitialisationComplete -= OnStateMachineLoaded;
            if (m_loadingStateMachines.Count == 0) m_onStateMachinesLoaded?.Invoke();
        }
    }
}
