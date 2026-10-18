using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000953; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class DeactivateWhileContainersAreOpen : MonoBehaviour
    {
        [SerializeField] private List<GameObject> m_objectsToDeactivate;
        [SerializeField] private List<UIContainerIdentifier> m_containers;
        private SystemRef<UIManager> m_uiManagerRef;

        // 0x0600359c
        private void Start()
        {
            m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>();
            m_uiManagerRef.InvokeOnValid(ListenForContainerOpen);
        }

        // 0x0600359d: the checked Get() reports a missing manager before the removal.
        private void OnDisable()
        {
            m_uiManagerRef.Get().OnContainerCreated -= OnContainerOpen;
        }

        // 0x0600359e
        private void ListenForContainerOpen(UIManager uiManager)
        {
            uiManager.OnContainerCreated += OnContainerOpen;
        }

        // 0x0600359f: every created container decides the objects' state, so opening an
        // unlisted container reactivates them.
        private void OnContainerOpen(UIContainer container)
        {
            foreach (GameObject objectToDeactivate in m_objectsToDeactivate)
                objectToDeactivate.SetActive(!m_containers.Contains(container.Identifier));
        }
    }
}
