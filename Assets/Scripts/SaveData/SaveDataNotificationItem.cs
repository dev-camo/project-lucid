using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataNotificationItem : SaveDataResolvableItem<SaveDataNotificationItem>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private NotificationType m_notification;
        [SerializeField]
        private bool m_requestedPermission;
        // Game.Runtime.dll 0x06002c96.
        public SaveDataNotificationItem()
        {
        // 0x06002c96 has no additional native work.
        }

        // Game.Runtime.dll 0x06002c8e.
        public NotificationType Notification
        {
            get
            {
                return m_notification;
            }

            // 0x06002c8f
            set
            {
                if (m_notification == value)
                    return;
                m_notification = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c90.
        public bool RequestedPermission
        {
            get
            {
                return m_requestedPermission;
            }

            // 0x06002c91
            set
            {
                if (m_requestedPermission == value)
                    return;
                m_requestedPermission = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c92.
        public override void ResolveNewData(SaveDataNotificationItem saveDataResolvableItem)
        {
            m_requestedPermission = saveDataResolvableItem.m_requestedPermission;
        }

        // Game.Runtime.dll 0x06002c93.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
        // 0x06002c93 has no additional native work.
        }

        // Game.Runtime.dll 0x06002c94.
        public void OnBeforeSerialize()
        {
        // 0x06002c94 has no additional native work.
        }

        // Game.Runtime.dll 0x06002c95.
        public void OnAfterDeserialize()
        {
        // 0x06002c95 has no additional native work.
        }
    }
}
