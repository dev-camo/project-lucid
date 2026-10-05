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

        // Game.Runtime.dll 0x06002c4c; ARM640x5be0b4. New keys retain incoming
        // object identities; direct field/dictionary merges do not mark dirty.
        public void ResolveNewData(SaveDataLevel newSaveDataLevel)
        {
            foreach (var pair in newSaveDataLevel.m_missionsByGuid)
            {
                if (m_missionsByGuid.TryGetValue(pair.Key, out var current)) current.ResolveNewData(pair.Value);
                else m_missionsByGuid[pair.Key] = pair.Value;
            }
            foreach (var pair in newSaveDataLevel.m_persistentObjectsById)
            {
                if (m_persistentObjectsById.TryGetValue(pair.Key, out var current)) current.ResolveNewData(pair.Value);
                else m_persistentObjectsById[pair.Key] = pair.Value;
            }
            m_missionIntrosSeen |= newSaveDataLevel.m_missionIntrosSeen;
            Hardlight.ListExtensions.AddUniqueFromRange(m_cutscenesSeen, newSaveDataLevel.m_cutscenesSeen);
            Hardlight.ListExtensions.AddUniqueFromRange(m_missionGroupsUnlockSeen, newSaveDataLevel.m_missionGroupsUnlockSeen);
            m_unlockSeen |= newSaveDataLevel.m_unlockSeen;
            // Original m_levelSelectUnlockSeen is not merged.
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

        // Game.Runtime.dll 0x06002c4f; ARM640x5c17b4.
        public SaveDataLevelMission GetOrCreateMissionData(string missionGuid)
        {
            if (!m_missionsByGuid.TryGetValue(missionGuid, out var mission))
            {
                mission = new SaveDataLevelMission(missionGuid);
                m_missionsByGuid[missionGuid] = mission;
                MarkDirty();
            }
            return mission;
        }

        // Game.Runtime.dll 0x06002c50; Boolean shared ARM640x9a0b24. Value-type
        // computation precedes dictionary lookup; existing type is not asserted.
        private bool InternalGetOrCreatePersistentObjectData<T>(PersistentObjectIdentifierType id,
            out SaveDataLevelPersistentObject persistentObject)
        {
            ValueType valueType = SaveDataLevelPersistentObject.GetValueType<T>();
            bool existed = m_persistentObjectsById.TryGetValue(id, out persistentObject);
            if (!existed)
            {
                persistentObject = new SaveDataLevelPersistentObject(id, valueType);
                m_persistentObjectsById[id] = persistentObject;
                MarkDirty();
            }
            return existed;
        }

        // Game.Runtime.dll 0x06002c51; ARM640x5c18ec: applies default only new.
        public SaveDataLevelPersistentObject GetOrCreatePersistentObjectData(
            PersistentObjectIdentifierType id, bool defaultValue)
        {
            bool existed = InternalGetOrCreatePersistentObjectData<bool>(id, out var persistentObject);
            if (!existed) persistentObject.Set(defaultValue);
            return persistentObject;
        }

        // Game.Runtime.dll 0x06002c52; ARM640x5c19a8: even duplicate marks dirty.
        public void AddSeenCutsceneGuid(string seenCutsceneGUID)
        {
            Hardlight.ListExtensions.AddUnique(m_cutscenesSeen, seenCutsceneGUID);
            MarkDirty();
        }

        // Game.Runtime.dll 0x06002c53; ARM640x5c1a10: even empty marks dirty.
        public void ClearAllSeenCutsceneGuids()
        {
            m_cutscenesSeen.Clear();
            MarkDirty();
        }

        // Game.Runtime.dll 0x06002c54; ARM640x5c1a7c: even duplicate marks dirty.
        public void AddMissionGroupUnlockSeen(string missionGroupGUID)
        {
            Hardlight.ListExtensions.AddUnique(m_missionGroupsUnlockSeen, missionGroupGUID);
            MarkDirty();
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

        // Game.Runtime.dll 0x06002c57; ARM640x5c1d78: dictionary, not list.
        public int GetCompleteMissionCount()
        {
            int complete = 0;
            foreach (var pair in m_missionsByGuid) if (pair.Value.Complete) ++complete;
            return complete;
        }

        // Game.Runtime.dll 0x06002c58; ARM640x5c1eec. Contains executes before
        // checking IsNewState; malformed collections/errors retain that order.
        public bool HasAnyNewContent(IReadOnlyList<string> validMissionGUIDs)
        {
            foreach (var pair in m_missionsByGuid)
                if (Hardlight.ReadOnlyListExtensions.Contains(validMissionGUIDs, pair.Value.GUID) &&
                    pair.Value.IsNewState == MissionIsNewState.IsNew) return true;
            return false;
        }
    }
}
