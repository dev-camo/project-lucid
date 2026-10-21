using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000951; bodies translated from the 1.10.1 x86-64 binary.
    // A container that publishes a caller supplied UI event when it is closed.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerWithCloseEvent : UIContainer
    {
        private UIModernEvent m_closeEvent;

        // 0x06003591
        public void SetCloseEvent(UIModernEvent closeEvent)
        {
            m_closeEvent = closeEvent;
        }

        // 0x06003592: nothing is published while no event is set (the Unity null test, so a
        // destroyed event also counts as unset).
        public virtual void Action_Close()
        {
            if (m_closeEvent == null)
                return;
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_closeEvent);
        }

        // 0x06003593
        public UIContainerWithCloseEvent() { }
    }
}
