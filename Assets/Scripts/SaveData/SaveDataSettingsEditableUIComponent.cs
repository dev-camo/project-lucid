using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataSettingsEditableUIComponent : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_id;
        [SerializeField]
        private Vector2 m_position;
        [SerializeField]
        private float m_scale;
        [SerializeField]
        private bool m_userSet;
        // Game.Runtime.dll 0x06002cfd.
        public string ID
        {
            get
            {
                return m_id;
            }
        }

        // Game.Runtime.dll 0x06002cfe.
        public Vector2 Position
        {
            get
            {
                return m_position;
            }

            // 0x06002cff; native compares components exactly.
            set
            {
                if (m_position.x == value.x && m_position.y == value.y)
                    return;
                m_position = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002d00.
        public float Scale
        {
            get
            {
                return m_scale;
            }

            // 0x06002d01
            set
            {
                if (m_scale == value)
                    return;
                m_scale = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002d02.
        public bool UserSet
        {
            get
            {
                return m_userSet;
            }

            // 0x06002d03
            set
            {
                if (m_userSet == value)
                    return;
                m_userSet = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002d04.
        public SaveDataSettingsEditableUIComponent(string id)
        {
            m_id = id;
        }

        // Game.Runtime.dll 0x06002d05.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
        // 0x06002d05 has no additional native work.
        }

        // Game.Runtime.dll 0x06002d06.
        public void OnBeforeSerialize()
        {
        // 0x06002d06 has no additional native work.
        }

        // Game.Runtime.dll 0x06002d07.
        public void OnAfterDeserialize()
        {
        // 0x06002d07 has no additional native work.
        }

        // Game.Runtime.dll 0x06002d08.
        public SaveDataSettingsEditableUIComponent Clone()
        {
            SaveDataSettingsEditableUIComponent clone = new SaveDataSettingsEditableUIComponent(m_id);
            clone.CopyFrom(this);
            return clone;
        }

        // Game.Runtime.dll 0x06002d09.
        public void CopyFrom(SaveDataSettingsEditableUIComponent other)
        {
            Position = other.Position;
            Scale = other.Scale;
            UserSet = other.UserSet;
        }
    }
}
