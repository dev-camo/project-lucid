using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005ad.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RequirementSpecificMissionComplete", menuName = "HardlightProject/Requirements/RequirementSpecificMissionComplete")]
    public class RequirementSpecificMissionComplete : RequirementGameplayLevelBase
    {
        [SerializeField] private MissionDefinition m_missionDefinition;
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);

        // 0x06001f06 (x86-64 0x557600): both managers must be present. The
        // mission's own level comes from the DataManager lookup (throwing for an
        // unregistered mission), and the save records are created on demand.
        // The levelDefinition argument is not read.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (!m_dataManagerRef.TryGet(out DataManager dataManager)) return false;
            if (!m_saveManagerRef.TryGet(out SaveManager saveManager)) return false;
            GameplayLevelDefinition missionLevel = dataManager.MissionDefinitionLevelLookup[m_missionDefinition];
            return saveManager.CurrentSave.GetOrCreateLevelData(missionLevel.GetGUID())
                .GetOrCreateMissionData(m_missionDefinition.GetGUID()).Complete;
        }

        // 0x06001f07.
        public RequirementSpecificMissionComplete() { }
    }
}
