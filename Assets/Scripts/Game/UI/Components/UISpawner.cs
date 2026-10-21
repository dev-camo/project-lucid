using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a3b; bodies translated from the 1.10.1 x86-64 binary.
    // Opens or closes one UI container on request, never opening a duplicate.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UISpawner : MonoBehaviour
    {
        [Tooltip("Container we want to open and close so we don't spawn duplicate UI events.")]
        [SerializeField] private UIContainerIdentifier m_container;
        private UIManager m_uiManager;

        // 0x06003a85
        private void Start()
        {
            ProcessManager.GetSystemRef<UIManager>(null, true).InvokeOnValid(Initialise);
        }

        // 0x06003a86
        private void Initialise(UIManager uiManager)
        {
            m_uiManager = uiManager;
        }

        // 0x06003a87
        public void Action_SpawnContainer()
        {
            if (m_uiManager.IsOpen(m_container))
                return;
            m_uiManager.GetOrCreate(m_container);
        }

        // 0x06003a88: a freshly opened container that supports a close event is given the event.
        public void Action_SpawnContainerWithCloseEvent(UIModernEvent closeEvent)
        {
            if (m_uiManager.IsOpen(m_container))
                return;
            UIContainerWithCloseEvent container = m_uiManager.GetOrCreate(m_container) as UIContainerWithCloseEvent;
            if (container == null)
                return;
            container.SetCloseEvent(closeEvent);
        }

        // 0x06003a89
        public void Action_DestroyContainer()
        {
            if (m_uiManager.IsOpen(m_container))
                m_uiManager.Close(m_container);
        }

        // 0x06003a8a
        public UISpawner() { }
    }
}
