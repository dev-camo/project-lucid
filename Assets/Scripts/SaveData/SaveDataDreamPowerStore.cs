using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataDreamPowerStore : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<SaveDataDreamPowerStoreItem> m_dreamPowers = new List<SaveDataDreamPowerStoreItem>();

        [SerializeField]
        private DreamPowerStoreIsNewState m_isNewState;

        private Dictionary<string, SaveDataDreamPowerStoreItem> m_dreamPowersByGuid = new Dictionary<string, SaveDataDreamPowerStoreItem>();

        // Game.Runtime.dll 0x06002bda.
        public List<SaveDataDreamPowerStoreItem> DreamPowers
        {
            get { return m_dreamPowers; }
        }

        // Game.Runtime.dll 0x06002bdb.
        public DreamPowerStoreIsNewState IsNewState
        {
            get { return m_isNewState; }
            // Game.Runtime.dll 0x06002bdc.
            set
            {
                m_isNewState = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002be3.
        public SaveDataDreamPowerStore()
        {
        }

        // Game.Runtime.dll 0x06002bde.
        public override void Initialise()
        {
            if (m_dreamPowersByGuid == null) m_dreamPowersByGuid = new Dictionary<string,SaveDataDreamPowerStoreItem>();
            if (m_dreamPowers == null) m_dreamPowers = new List<SaveDataDreamPowerStoreItem>();
            base.Initialise();
        }

        // Game.Runtime.dll 0x06002bdf.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (var pair in m_dreamPowersByGuid) action(pair.Value);
        }

        // Game.Runtime.dll 0x06002be1.
        public void OnBeforeSerialize()
        {
            DictionaryToList(m_dreamPowersByGuid, ref m_dreamPowers);
        }

        // Game.Runtime.dll 0x06002be2.
        public void OnAfterDeserialize()
        {
            Initialise();
            ListToDictionary(m_dreamPowers, m_dreamPowersByGuid, entry => entry.GUID);
        }

    }
}
