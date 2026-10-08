using System;
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // HLNotifications.Runtime 0x02000002. Original singleton and native bridge boundary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class HLNotifications : MonoSingleton<HLNotifications>
    {
        [SerializeField]
        [Tooltip("This is the name of the notification sound that is in your Plugins/iOS/Native~/notifications folder")]
        private string m_notificationSoundiOS = string.Empty;
        [Tooltip("If true, Firebase Analytics will be switched on when HLNotifications initialises the native code.")]
        [SerializeField]
        private bool m_enableAnalyticsOnInitialise;
        [Tooltip("If true, Firebase Cloud Messaging auto-init will be switched on again when HLNotifications initialises the native code.")]
        [SerializeField]
        private bool m_enableCloudMessagingAutoInitOnInitialise = true;
        [SerializeField]
        [Tooltip("If true, local notifications will show a badge next to the app icon when a notification is fired.")]
        private bool m_showNotificationBadge;
        private static HLNotificationsNativeBridge s_notificationsNativeBridge;
        private bool m_isInitialised;
        private string m_cachedPushToken;
        public FastAction<string> OnReceivedPushToken;

        // 0x06000001. Return the captured bridge; lazy allocation precedes static storage.
        private static HLNotificationsNativeBridge NativeBridge
        {
            get
            {
                HLNotificationsNativeBridge bridge = s_notificationsNativeBridge;
                if (bridge == null)
                {
                    bridge = new HLNotificationsNativeBridge();
                    s_notificationsNativeBridge = bridge;
                }
                return bridge;
            }
        }

        // 0x06000002 / 0x06000003; direct original field reads.
        public string CachedPushToken => m_cachedPushToken;
        public string NotificationSoundiOS => m_notificationSoundiOS;

        // 0x06000004. Owner initialisation precedes a fresh bridge read.
        public static void RegisterForNotifications()
        {
            Instance.EnsureInitialised();
            NativeBridge.RegisterForNotifications();
        }

        // 0x06000005.
        public static void ClearAllReceived()
        {
            Instance.EnsureInitialised();
            NativeBridge.ClearAllReceived();
        }

        // 0x06000006. Registration is an additional original call after initialisation.
        public static void ScheduleLocalNotification(string message, DateTime fireDate,
            string notificationId, string category, Dictionary<string, string> actionAndParams)
        {
            Instance.EnsureInitialised();
            RegisterForNotifications();
            NativeBridge.ScheduleLocalNotification(message, fireDate, notificationId, category, actionAndParams);
        }

        // 0x06000007.
        public static bool CancelAllNotifications()
        {
            Instance.EnsureInitialised();
            return NativeBridge.CancelAllNotifications();
        }

        // 0x06000008 / 0x06000009; preserve the two original overloads.
        public static bool CancelNotification(string type)
        {
            Instance.EnsureInitialised();
            return NativeBridge.CancelNotification(type);
        }
        public static bool CancelNotification(string type, string id)
        {
            Instance.EnsureInitialised();
            return NativeBridge.CancelNotification(type, id);
        }

        // 0x0600000a.
        public static void ClearNotificationBadge()
        {
            Instance.EnsureInitialised();
            NativeBridge.ClearNotificationBadge();
        }

        // 0x0600000b. This original route skips singleton initialisation.
        public static bool IsPermissionGranted() => NativeBridge.IsPermissionGranted();

        // 0x0600000c.
        public static IDictionary GetReceivedNotificationData()
        {
            Instance.EnsureInitialised();
            return NativeBridge.GetReceivedNotificationData();
        }

        // 0x0600000d. A fresh singleton/token read follows the optional registration.
        public static string GetPushToken()
        {
            if (string.IsNullOrEmpty(Instance.CachedPushToken))
                RegisterForNotifications();
            return Instance.CachedPushToken;
        }

        // 0x0600000e. Shipping macOS inlines the empty Initialise body: only lazy
        // bridge acquisition and the later flag write remain visible. Retaining the
        // real call and its configuration arguments is an authored-flow inference.
        private void EnsureInitialised()
        {
            if (m_isInitialised)
                return;
            NativeBridge.Initialise(this, m_enableAnalyticsOnInitialise,
                m_showNotificationBadge, m_enableCloudMessagingAutoInitOnInitialise);
            m_isInitialised = true;
        }

        // 0x0600000f. Store before reading/invoking the current null-safe FastAction.
        public void Native_ReceivedPushToken(string token)
        {
            m_cachedPushToken = token;
            OnReceivedPushToken.Invoke(token);
        }

        // 0x06000010. Sound/auto-init initialisers precede the genuine base constructor.
        public HLNotifications() { }
    }
}
