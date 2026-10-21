using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008da; bodies translated from the 1.10.1 x86-64 binary.
    // The mission failure screen: shows the failure message and leaves through either exit or restart, once.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerMissionFailure : UIContainer
    {
        [SerializeField] private UIModernEvent m_exitEvent;
        [SerializeField]
        [HashEnum]
        private Strings m_defaultFailureMessage;
        public readonly Bindable<Strings> FailureMessage = new Bindable<Strings>();
        private bool m_isExiting;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);

        // 06003308 (0x63BCF0): the stored override applies only when it is present and not zero.
        private void OnEnable()
        {
            Strings failureMessage = m_defaultFailureMessage;
            if (m_appRef.Get().Storage.TryGetValue(AppFSMKeys.FailureScreenStringOverride, out Strings overrideMessage) && overrideMessage != 0)
                failureMessage = overrideMessage;
            FailureMessage.Value = failureMessage;
        }

        // 06003309 (0x63BDD0)
        public void Action_Exit() => Exit();

        // 0600330a (0x63BF90)
        public void Action_Restart() => Restart();

        // 0600330b (0x63BDE0)
        private void Exit()
        {
            if (m_isExiting)
                return;
            m_isExiting = true;
            CleanUp();
            ProcessManager.GetSystem<MissionManager>(null, true).SetActiveMissionContext(null);
            m_appRef.Get().AddUniqueUIModernEvent(m_exitEvent);
            m_appRef.Get().Storage.SetValue(AppFSMKeys.ShowFailureScreen, false);
        }

        // 0600330c (0x63BFA0)
        private void Restart()
        {
            if (m_isExiting)
                return;
            m_isExiting = true;
            CleanUp();
            ProcessManager.GetSystem<LevelManager>(null, true).Restart();
        }

        // 0600330d (0x63C030)
        private void CleanUp()
        {
            m_appRef.Get().Storage.RemoveValue<Strings>(AppFSMKeys.FailureScreenStringOverride);
        }

        // 0600330e
        public UIContainerMissionFailure() { }
    }
}
