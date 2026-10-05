using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataDreamPowerLoadout : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private CharacterArchetype m_archetype;

        [SerializeField]
        private List<string> m_slots = new List<string>();

        private Dictionary<int, string> m_slotsByIndex = new Dictionary<int, string>();

        // Game.Runtime.dll 0x06002bce.
        public CharacterArchetype Archetype
        {
            get { return m_archetype; }
        }

        // Game.Runtime.dll 0x06002bcf.
        public IReadOnlyDictionary<int, string> SlotsByIndex
        {
            get { return m_slotsByIndex; }
        }

        // Game.Runtime.dll 0x06002bd0.
        public SaveDataDreamPowerLoadout(CharacterArchetype characterArchetype)
        {
            m_archetype = characterArchetype;
        }

        // Game.Runtime.dll 0x06002bd2.
        public override void Initialise()
        {
            if (m_slotsByIndex == null) m_slotsByIndex = new Dictionary<int,string>();
            if (m_slots == null) m_slots = new List<string>();
            base.Initialise();
        }

        // Game.Runtime.dll 0x06002bd5.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002bd6.
        public void OnBeforeSerialize()
        {
            DictionaryToList(m_slotsByIndex, ref m_slots);
        }

        // Game.Runtime.dll 0x06002bd7.
        public void OnAfterDeserialize()
        {
            Initialise();
            ListToDictionary(m_slots, m_slotsByIndex, entry => m_slots.IndexOf(entry));
        }

    }
}
