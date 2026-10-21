using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200093b; bodies translated from the 1.10.1 x86-64 binary.
    // The notifications page: one toggle that grants or retracts the permission to notify.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSettingsNotifications : UIContainer
    {
        [SerializeField] private Toggle m_notificationsPermission;

        // 0x0600350a (0x67DC80): shows the current permission without reporting a change.
        private void Start()
        {
            m_notificationsPermission.SetIsOnWithoutNotify(ProcessManager.GetSystem<NotificationManager>().NotificationPermissionFullyGranted);
        }

        // 0x0600350b (0x67DD00)
        public void Action_SetNotificationPermission(bool active)
        {
            if (active)
                ProcessManager.GetSystem<NotificationManager>().GrantPermission();
            else
                ProcessManager.GetSystem<NotificationManager>().RetractPermission();
        }

        // 0x0600350c (0x67DDB0)
        public UIContainerSettingsNotifications() { }
    }
}
