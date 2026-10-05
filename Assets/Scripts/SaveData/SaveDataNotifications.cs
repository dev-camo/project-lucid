using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataNotifications : SaveDataResolvableItem<SaveDataNotifications>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private bool m_notificationsPermitted;
        [SerializeField]
        private List<SaveDataNotificationItem> m_savedNotificationData = new List<SaveDataNotificationItem>();
        // Game.Runtime.dll 0x06002c9f.
        public SaveDataNotifications()
        {
        // 0x06002c9f has no additional native work.
        }

        // Game.Runtime.dll 0x06002c97.
        public bool NotificationsPermitted
        {
            get
            {
                return m_notificationsPermitted;
            }

            // 0x06002c98
            set
            {
                if (m_notificationsPermitted == value)
                    return;
                m_notificationsPermitted = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c99.
        public List<SaveDataNotificationItem> NotificationSaveData
        {
            get
            {
                return m_savedNotificationData;
            }
        }

        // Game.Runtime.dll 0x06002c9a.
        public SaveDataNotificationItem TryGetOrNew(NotificationType notificationType)
        {
            foreach (SaveDataNotificationItem data in m_savedNotificationData)
            {
                if (data.Notification == notificationType)
                    return data;
            }

            SaveDataNotificationItem created = new SaveDataNotificationItem
            {
                Notification = notificationType
            };
            m_savedNotificationData.Add(created);
            created.MarkDirty();
            MarkDirty();
            return created;
        }

        // Game.Runtime.dll 0x06002c9b.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (SaveDataNotificationItem data in m_savedNotificationData)
                action(data);
        }

        // Game.Runtime.dll 0x06002c9c.
        public void OnBeforeSerialize()
        {
        // 0x06002c9c has no additional native work.
        }

        // Game.Runtime.dll 0x06002c9d.
        public void OnAfterDeserialize()
        {
            Initialise();
        }

        // Game.Runtime.dll 0x06002c9e.
        public override void ResolveNewData(SaveDataNotifications newSaveDataNotifications)
        {
            foreach (SaveDataNotificationItem incoming in newSaveDataNotifications.m_savedNotificationData)
            {
                bool found = false;
                foreach (SaveDataNotificationItem current in m_savedNotificationData)
                {
                    if (current.Notification == incoming.Notification)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    m_savedNotificationData.Add(incoming);
            }

            foreach (SaveDataNotificationItem current in m_savedNotificationData)
            {
                foreach (SaveDataNotificationItem incoming in newSaveDataNotifications.m_savedNotificationData)
                {
                    if (current.Notification == incoming.Notification)
                        current.ResolveNewData(incoming);
                }
            }

            m_notificationsPermitted = newSaveDataNotifications.m_notificationsPermitted;
        }
    }
}
