using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameInputDisabler : MonoBehaviour
    {
        [Tooltip("GameInputs to disable when triggered.")]
        [SerializeField] private GameInput[] m_gameInputs;
        [Tooltip("If true, disable the GameInputs when this component is enabled. GameInputs are always re-enabled when component is disabled.")]
        [SerializeField] private bool m_activateOnEnable;
        private readonly SystemRef<InputSystem> m_inputSystemRef = ProcessManager.GetSystemRef<InputSystem>(null, true);
        private ControlMapping m_controlMapping;
        private readonly List<StackableDataHandle> m_stackableDataHandles = new List<StackableDataHandle>();

        // Original0600238e: store or clear the mapping before freshly checking the authored enable flag.
        private void OnEnable()
        {
            if (m_inputSystemRef.TryGet(out InputSystem inputSystem))
                m_controlMapping = inputSystem.ControlMapping;
            else
                m_controlMapping = null;
            if (m_activateOnEnable) Action_DisableGameInputs();
        }

        // Original0600238f and06002390 both invoke the same release action.
        private void OnDestroy() { Action_ReenableGameInputs(); }
        private void OnDisable() { Action_ReenableGameInputs(); }

        // Original06002391: Behaviour.enabled is the first guard; the authored array is captured once.
        public void Action_DisableGameInputs()
        {
            if (!enabled) return;
            if (m_stackableDataHandles.Count > 0) return;
            if (m_controlMapping == null) return;
            foreach (GameInput gameInput in m_gameInputs)
            {
                ControlMapping controlMapping = m_controlMapping;
                List<StackableDataHandle> handles = m_stackableDataHandles;
                handles.Add(controlMapping.AddGameInputDisabled(gameInput));
            }
        }

        // Original06002392: dispose the live list enumerator on fault; clear only after successful removal.
        public void Action_ReenableGameInputs()
        {
            if (m_stackableDataHandles.Count == 0) return;
            if (m_controlMapping == null)
            {
                m_stackableDataHandles.Clear();
                return;
            }
            foreach (StackableDataHandle handle in m_stackableDataHandles)
                m_controlMapping.RemoveGameInputDisabled(handle);
            m_stackableDataHandles.Clear();
        }

        // Original06002393: the two field initializers run in declaration order, then the real MonoBehaviour constructor.
    }
}
