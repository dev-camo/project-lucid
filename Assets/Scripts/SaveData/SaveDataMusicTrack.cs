using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataMusicTrack : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_guid;

        [SerializeField]
        private bool m_seen;

        // Game.Runtime.dll 0x06002c86.
        public string GUID
        {
            get { return m_guid; }
        }

        // Game.Runtime.dll 0x06002c87.
        public bool Seen
        {
            get { return m_seen; }
            // Game.Runtime.dll 0x06002c88.
            set
            {
                m_seen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c89.
        public SaveDataMusicTrack(string musicTrackGUID)
        {
            m_guid = musicTrackGUID;
        }

        // Game.Runtime.dll 0x06002c8b.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002c8c.
        public void OnBeforeSerialize()
        {
            // Original ARM64 body is a single RET.
        }

        // Game.Runtime.dll 0x06002c8d.
        public void OnAfterDeserialize()
        {
            // Original ARM64 body is a single RET.
        }

    }
}
