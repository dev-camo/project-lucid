using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000847; bodies translated from the 1.10.1 x86-64 binary.
    // Menu callbacks that publish the authored continue and back UI events.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIMenuNavigationActions : MonoBehaviour
    {
        [SerializeField] private UIModernEvent m_continueUIEvent;
        [SerializeField] private UIModernEvent m_backUIEvent;

        // 0x06002ffa
        public void Action_Continue()
        {
            TriggerEvent(m_continueUIEvent);
        }

        // 0x06002ffb
        public void Action_GoBack()
        {
            TriggerEvent(m_backUIEvent);
        }

        // 0x06002ffc: the event is published even when unset.
        private void TriggerEvent(UIModernEvent uiEvent)
        {
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(uiEvent);
        }

        // 0x06002ffd
        public UIMenuNavigationActions() { }
    }
}
