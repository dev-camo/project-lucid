using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 020008f0/020008f1; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerNotificationRequest : UIContainer
    {
        [SerializeField]
        private NotificationPermissionConfig[] m_notificationTypes;
        [SerializeField]
        private Image m_displayImage;
        [SerializeField]
        private LocalisedUIString m_displayString;
        private NotificationManager m_notificationManager;
        private NotificationType m_notification;

        // 060033ae (1.10.1 RVA 0x66BD10): the base Setup is not called. Every configuration for the
        // requested notification is shown, the last match winning, and the request is recorded
        // once per match.
        public override void Setup(IUIContainerParameters uiContainerParameters)
        {
            m_notificationManager = ProcessManager.GetSystem<NotificationManager>(null, true);
            m_notification = (uiContainerParameters as UIContainerNotificationRequestParameters).Notification;
            for (int i = 0; i < m_notificationTypes.Length; i++)
            {
                NotificationPermissionConfig config = m_notificationTypes[i];
                if (config.Notification == m_notification)
                {
                    m_displayImage.sprite = config.Sprite;
                    m_displayString.Identifier = config.String;
                    m_notificationManager.IsRequestingNotification(config.Notification);
                }
            }
        }

        // 060033af (1.10.1 RVA 0x66BE20)
        public void Action_OnAccepted()
        {
            m_notificationManager.GrantPermission();
        }

        // 060033b0 (1.10.1 RVA 0x66BE30)
        public UIContainerNotificationRequest()
        {
        }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class NotificationPermissionConfig
        {
            public NotificationType Notification;
            public Sprite Sprite;
            [HashEnum(null)]
            public Strings String;

            // 060033b1 (1.10.1 RVA 0x66BE40)
            public NotificationPermissionConfig()
            {
            }
        }
    }
}
