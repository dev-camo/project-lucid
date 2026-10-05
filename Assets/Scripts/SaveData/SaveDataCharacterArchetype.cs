using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataCharacterArchetype : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private CharacterArchetype m_characterArchetype;

        [SerializeField]
        private bool m_unlocked;

        [SerializeField]
        private bool m_unlockSeen;

        // Game.Runtime.dll 0x06002b9f.
        public CharacterArchetype CharacterArchetype
        {
            get { return m_characterArchetype; }
        }

        // Game.Runtime.dll 0x06002ba0.
        public bool Unlocked
        {
            get { return m_unlocked; }
            // Game.Runtime.dll 0x06002ba1.
            set
            {
                m_unlocked = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ba2.
        public bool UnlockSeen
        {
            get { return m_unlockSeen; }
            // Game.Runtime.dll 0x06002ba3.
            set
            {
                m_unlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ba4.
        public SaveDataCharacterArchetype(CharacterArchetype characterArchetype)
        {
            m_characterArchetype = characterArchetype;
        }

        // Game.Runtime.dll 0x06002ba6.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002ba7.
        public void OnBeforeSerialize()
        {
            // Original ARM64 body is a single RET.
        }

        // Game.Runtime.dll 0x06002ba8.
        public void OnAfterDeserialize()
        {
            // Original ARM64 body is a single RET.
        }

    }
}
