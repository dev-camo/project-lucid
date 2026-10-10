using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "MissionList", menuName = "HardlightProject/DefinitionData/Definitions/MissionList")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionList : ScriptableObject
    {
        [SerializeField]
        private List<MissionGroup> m_groups;

        private readonly List<MissionDefinition> m_missionList = new List<MissionDefinition>();

        private readonly List<string> m_missionGUIDsList = new List<string>();

        // Game.Runtime 0x060026b2: returns the original list, without copying.
        public IReadOnlyList<MissionGroup> Groups => m_groups;

        // Game.Runtime 0x060026b3: two local lists survive stripped validation diagnostics.
        // The native method performs these traversals but does not repair or store their results.
        private void OnValidate()
        {
            var missionGUIDs = new List<string>();
            var groupGUIDs = new List<string>();
            foreach (var group in m_groups)
            {
                groupGUIDs.Add(group.GUID);
                foreach (var missionDefinition in group.Definitions)
                {
                    var missionGUID = missionDefinition.GetGUID();
                    if (!missionGUIDs.Contains(missionGUID))
                        missionGUIDs.Add(missionGUID);
                }
            }
        }

        // Game.Runtime 0x060026b4; generated cached predicate 0x060026c0 reads m_complete.
        public IReadOnlyList<MissionDefinition> GetIncompleteMissions(GameplayLevelDefinition levelDefinition)
        {
            return GetMissions(levelDefinition, missionSaveData => !missionSaveData.Complete);
        }

        // Game.Runtime 0x060026b5; generated cached predicate 0x060026c1 reads m_complete.
        public IReadOnlyList<MissionDefinition> GetCompleteMissions(GameplayLevelDefinition levelDefinition)
        {
            return GetMissions(levelDefinition, missionSaveData => missionSaveData.Complete);
        }

        // Game.Runtime 0x060026b6: clear first, then accumulate visible main missions in authored order.
        public IReadOnlyList<MissionDefinition> GetMissions(bool includeHiddenGroups = false)
        {
            m_missionList.Clear();
            foreach (var group in m_groups)
            {
                if (!includeHiddenGroups && group.HasAlwaysHiddenRequirement())
                    continue;
                foreach (var missionDefinition in group.Definitions)
                    if (missionDefinition.ShowInMissionList)
                        m_missionList.Add(missionDefinition);
            }
            return m_missionList;
        }

        // Game.Runtime 0x060026b7: separate reused cache; hidden groups are included.
        public IReadOnlyList<string> GetMissionGUIDs()
        {
            m_missionGUIDsList.Clear();
            foreach (var group in m_groups)
                foreach (var missionDefinition in group.Definitions)
                    if (missionDefinition.ShowInMissionList)
                        m_missionGUIDsList.Add(missionDefinition.GetGUID());
            return m_missionGUIDsList;
        }

        // Game.Runtime 0x060026b8: preserves the original MissionGroup query and equality rules.
        public bool HasMission(MissionDefinition missionDefinition)
        {
            foreach (var group in m_groups)
                if (group.HasMission(missionDefinition))
                    return true;
            return false;
        }

        // Game.Runtime 0x060026b9: both lookups and initial level-data creation precede the clear.
        // GetSaveDataForMission's native body is inlined here but retains its genuine API boundary.
        private IReadOnlyList<MissionDefinition> GetMissions(GameplayLevelDefinition levelDefinition, Predicate<SaveDataLevelMission> predicate)
        {
            var saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            var missionManager = ProcessManager.GetSystem<MissionManager>(null, true);
            var levelData = saveManager.CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
            m_missionList.Clear();
            foreach (var group in m_groups)
            {
                foreach (var missionDefinition in group.Definitions)
                {
                    if (!missionDefinition.ShowInMissionList)
                        continue;
                    var missionData = missionManager.GetSaveDataForMission(missionDefinition.GetGUID(), levelDefinition.GetGUID(), levelData);
                    if (predicate(missionData))
                        m_missionList.Add(missionDefinition);
                }
            }
            return m_missionList;
        }

        // Game.Runtime 0x060026ba: performs save-data queries without modifying either cache.
        public bool HasAnyCompleteMissions(GameplayLevelDefinition levelDefinition)
        {
            var saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            var missionManager = ProcessManager.GetSystem<MissionManager>(null, true);
            var levelData = saveManager.CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
            foreach (var group in m_groups)
                foreach (var missionDefinition in group.Definitions)
                    if (missionDefinition.ShowInMissionList && missionManager.GetSaveDataForMission(missionDefinition.GetGUID(), levelDefinition.GetGUID(), levelData).Complete)
                        return true;
            return false;
        }

        // Game.Runtime 0x060026bb and generated iterator 0x060026c2..0x060026cc.
        // Immediate sub-missions are visited even when their parent is hidden or another type.
        public IEnumerable<MissionDefinition> GetMissionsOfType(MissionType missionType)
        {
            foreach (var group in m_groups)
            {
                foreach (var missionDefinition in group.Definitions)
                {
                    if (missionDefinition.ShowInMissionList && missionDefinition.Type == missionType)
                        yield return missionDefinition;
                    foreach (var subMission in missionDefinition.SubMissions)
                        if (subMission.ShowInMissionList && subMission.Type == missionType)
                            yield return subMission;
                }
            }
        }

        // Game.Runtime 0x060026bc: matching immediate children add the parent's RewardValue.
        // Preserve that native behavior and 32-bit wraparound instead of substituting child values.
        public int MissionCountWithReward(CollectableType reward)
        {
            var count = 0;
            foreach (var group in m_groups)
            {
                if (group.HasAlwaysHiddenRequirement())
                    continue;
                foreach (var missionDefinition in group.Definitions)
                {
                    if (missionDefinition.ShowInMissionList && missionDefinition.RewardType == reward)
                        count = unchecked(count + missionDefinition.RewardValue);
                    foreach (var subMission in missionDefinition.SubMissions)
                        if (subMission.ShowInMissionList && subMission.RewardType == reward)
                            count = unchecked(count + missionDefinition.RewardValue);
                }
            }
            return count;
        }

        // Game.Runtime 0x060026bd: both readonly-list initializers precede the genuine base ctor.
        public MissionList() { }
    }
}
