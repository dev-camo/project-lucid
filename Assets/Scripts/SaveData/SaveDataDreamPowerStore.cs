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

        // Game.Runtime.dll 0x06002bdd.
        public void ResolveNewData(SaveDataDreamPowerStore newSaveData)
        {
            foreach (var (dreamPowerGuid, newDreamPowerData) in newSaveData.m_dreamPowersByGuid)
            {
                if (m_dreamPowersByGuid.TryGetValue(dreamPowerGuid, out var current))
                    current.ResolveNewData(newDreamPowerData);
                else
                    m_dreamPowersByGuid[dreamPowerGuid] = newDreamPowerData;
            }
            if (newSaveData.m_isNewState == DreamPowerStoreIsNewState.IsNew)
            {
                if (m_isNewState == DreamPowerStoreIsNewState.None)
                    m_isNewState = DreamPowerStoreIsNewState.IsNew;
            }
            else if (newSaveData.m_isNewState == DreamPowerStoreIsNewState.Seen)
                m_isNewState = DreamPowerStoreIsNewState.Seen;
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

        // Game.Runtime.dll 0x06002be0.
        public SaveDataDreamPowerStoreItem GetOrCreateDreamPowerData(string dreamPowerGuid)
        {
            if (!m_dreamPowersByGuid.TryGetValue(dreamPowerGuid, out var dreamPowerData))
            {
                dreamPowerData = new SaveDataDreamPowerStoreItem(dreamPowerGuid);
                m_dreamPowersByGuid[dreamPowerGuid] = dreamPowerData;
                MarkDirty();
            }
            return dreamPowerData;
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
            ListToDictionary(m_dreamPowers, m_dreamPowersByGuid, dreamPower => dreamPower.GUID);
        }

        // Game.Runtime.dll 0x06002be3.
        public SaveDataDreamPowerStore()
        {
        }

    }
}
