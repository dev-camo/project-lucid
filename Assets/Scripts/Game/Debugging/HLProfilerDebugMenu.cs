using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020005f9; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLProfilerDebugMenu : MonoBehaviour
    {
        [SerializeField]
        private HLProfilerConsole m_profilerConsole;

        [SerializeField]
        private RuntimeIssueTracker m_runtimeIssueTracker;

        // 06002017 (1.10.1 RVA 0x560390): the shipped body is empty. The DebugMenu.AddButton calls that
        // registered ToggleProfiler and ToggleIssueTracker are compiled out of retail.
        private void Awake() { }

        // 06002018 (1.10.1 RVA 0x5603A0)
        private void ToggleProfiler()
        {
            if (m_profilerConsole.enabled) m_profilerConsole.CloseProfiler();
            else m_profilerConsole.OpenProfiler();
        }

        // 06002019 (1.10.1 RVA 0x5603D0): the native body inlines CloseTracker(true) and OpenTracker(true).
        private void ToggleIssueTracker()
        {
            if (m_runtimeIssueTracker.IsVisible) m_runtimeIssueTracker.CloseTracker(true);
            else m_runtimeIssueTracker.OpenTracker(true);
        }

        // 0600201a (1.10.1 RVA 0x5604A0)
        public HLProfilerDebugMenu() { }
    }
}
