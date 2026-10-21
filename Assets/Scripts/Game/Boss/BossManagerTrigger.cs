using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001f9; 0x7036B0..0x7037F0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossManagerTrigger : MonoBehaviour
    {
        private SystemRef<BossManager> m_bossManagerRef;
        private BossManager m_bossManager;

        // 06000b94, with the cached <Start>b__2_0 instance callback.
        private void Start()
        {
            m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
            m_bossManagerRef.InvokeOnValid(manager => m_bossManager = manager);
        }

        // 06000b95: SetPhaseContinue is inlined natively (it only raises the continue flag).
        public void Action_SetPhaseProgression() => m_bossManager.SetPhaseContinue();
    }
}
