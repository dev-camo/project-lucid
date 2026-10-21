using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008c1; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerGameplayControl : UIContainer
    {
        [SerializeField]
        private bool m_enableGameplayControl;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private bool m_previousGameplayControl;

        // 06003293 (1.10.1 RVA 0x6358E0): remembers the control state it found so
        // OnDestroy can put it back.
        protected virtual void Start()
        {
            m_previousGameplayControl = m_appRef.Get().Storage.GetValue<bool>(AppFSMKeys.GameplayControlActive, false, true);
            m_appRef.Get().SetHasGameplayControl(m_enableGameplayControl, false);
        }

        // 06003294 (1.10.1 RVA 0x635A30)
        protected virtual void OnDestroy()
        {
            m_appRef.Get().SetHasGameplayControl(m_previousGameplayControl, false);
        }

        // 06003295 (1.10.1 RVA 0x635A80)
        public UIContainerGameplayControl()
        {
        }
    }
}
