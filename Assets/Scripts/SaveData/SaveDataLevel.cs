using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataLevel : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_guid;

        [SerializeField]
        private List<SaveDataLevelMission> m_missions = new List<SaveDataLevelMission>();

        [SerializeField]
        private bool m_missionIntrosSeen;

        [SerializeField]
        private List<SaveDataLevelPersistentObject> m_persistentObjects = new List<SaveDataLevelPersistentObject>();

        [SerializeField]
        private List<string> m_cutscenesSeen = new List<string>();

        [SerializeField]
        private bool m_unlockSeen;

        [SerializeField]
        private bool m_levelSelectUnlockSeen;

        [SerializeField]
        private List<string> m_missionGroupsUnlockSeen = new List<string>();

        private Dictionary<string, SaveDataLevelMission> m_missionsByGuid = new Dictionary<string, SaveDataLevelMission>();

        private Dictionary<PersistentObjectIdentifierType, SaveDataLevelPersistentObject> m_persistentObjectsById = new Dictionary<PersistentObjectIdentifierType, SaveDataLevelPersistentObject>(HardlightEnumComparers.PersistentObjectIdentifierTypeComparer);

        // Game.Runtime.dll 0x06002c43.
        public string GUID
        {
            get { return m_guid; }
        }

        // Game.Runtime.dll 0x06002c44.
        public bool UnlockSeen
        {
            get { return m_unlockSeen; }
            // Game.Runtime.dll 0x06002c45.
            set
            {
                m_unlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c46.
        public bool LevelSelectUnlockSeen
        {
            get { return m_levelSelectUnlockSeen; }
            // Game.Runtime.dll 0x06002c47.
            set
            {
                m_levelSelectUnlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c48.
        public List<string> SeenCutsceneOnLevelGuids
        {
            get { return m_cutscenesSeen; }
        }

        // Game.Runtime.dll 0x06002c49.
        public List<string> MissionGroupsUnlockSeen
        {
            get { return m_missionGroupsUnlockSeen; }
        }

        // Game.Runtime.dll 0x06002c4a.
        public IReadOnlyList<SaveDataLevelMission> Missions
        {
            get { return m_missions; }
        }

        // Game.Runtime.dll 0x06002c4b.
        public SaveDataLevel(string levelGuid)
        {
            m_guid = levelGuid;
        }

        // Game.Runtime.dll 0x06002c4d.
        public override void Initialise()
        {
            if (m_missionsByGuid == null) m_missionsByGuid = new Dictionary<string,SaveDataLevelMission>();
            if (m_missions == null) m_missions = new List<SaveDataLevelMission>();
            if (m_persistentObjectsById == null) m_persistentObjectsById = new Dictionary<PersistentObjectIdentifierType,SaveDataLevelPersistentObject>(HardlightEnumComparers.PersistentObjectIdentifierTypeComparer);
            if (m_persistentObjects == null) m_persistentObjects = new List<SaveDataLevelPersistentObject>();
            if (m_cutscenesSeen == null) m_cutscenesSeen = new List<string>();
            if (m_missionGroupsUnlockSeen == null) m_missionGroupsUnlockSeen = new List<string>();
            base.Initialise();
        }

        // Game.Runtime.dll 0x06002c4e.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (var pair in m_missionsByGuid) action(pair.Value);
            foreach (var pair in m_persistentObjectsById) action(pair.Value);
        }

        // Game.Runtime.dll 0x06002c55.
        public void OnBeforeSerialize()
        {
            DictionaryToList(m_missionsByGuid, ref m_missions);
            DictionaryToList(m_persistentObjectsById, ref m_persistentObjects);
        }

        // Game.Runtime.dll 0x06002c56.
        public void OnAfterDeserialize()
        {
            Initialise();
            ListToDictionary(m_missions, m_missionsByGuid, entry => entry.GUID);
            ListToDictionary(m_persistentObjects, m_persistentObjectsById, entry => entry.ID);
        }

    }
}
