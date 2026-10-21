using Hardlight;

namespace HardlightProject
{
    // Original Game.Runtime 020005fb; bodies translated from the 1.10.1 x86-64 binary.
    public class ProjectConsole : HLLogConsole
    {
        // 0600201c (1.10.1 RVA 0x5604C0): the console runs only when the core configuration enables the
        // device console. A missing configuration faults.
        private void Start()
        {
            enabled = SystemConfiguration.GetConfig<CoreGameConfiguration>().DeviceConsoleEnabled;
        }

        // 0600201d (1.10.1 RVA 0x560520): the debug menu button that calls this is compiled out of retail.
        private void OpenConsoleFromDebugMenu()
        {
            Action_ConsoleShow();
        }

        // 0600201e (1.10.1 RVA 0x560530)
        public ProjectConsole() { }
    }
}
