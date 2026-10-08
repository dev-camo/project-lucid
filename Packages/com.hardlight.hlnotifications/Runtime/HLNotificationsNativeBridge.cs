using System;
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLNotifications.Runtime 0x02000004. These empty/true/null implementations
    // are the actual shipping macOS bridge bodies, not replacement platform services.
    // Original metadata marks the ten methods final virtual in this sealed class
    // without an interface. Ordinary C# cannot reproduce that flag combination;
    // preserve the whole API/body and retain that explicit emission limitation.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class HLNotificationsNativeBridge
    {
        // 0x06000016; actual original defaults true/false/true, immediate return.
        public void Initialise(HLNotifications componentOwner, bool enableAnalytics = true,
            bool showNotificationBadge = false, bool enableCloudMessagingAutoInit = true) { }
        // 0x06000017 / 0x06000018 / 0x06000019; immediate-return originals.
        public void RegisterForNotifications() { }
        public void ClearAllReceived() { }
        public void ScheduleLocalNotification(string message, DateTime fireDate,
            string notificationId, string category, Dictionary<string, string> actionAndParams) { }
        // 0x0600001a / 0x0600001b / 0x0600001c; original constant true bodies.
        public bool CancelAllNotifications() => true;
        public bool CancelNotification(string type) => true;
        public bool CancelNotification(string type, string id) => true;
        // 0x0600001d; immediate return. 0x0600001e; original constant true.
        public void ClearNotificationBadge() { }
        public bool IsPermissionGranted() => true;
        // 0x0600001f; original null, with no replacement collection allocation.
        public IDictionary GetReceivedNotificationData() => null;
        // 0x06000020; original object base constructor only.
        public HLNotificationsNativeBridge() { }
    }
}
