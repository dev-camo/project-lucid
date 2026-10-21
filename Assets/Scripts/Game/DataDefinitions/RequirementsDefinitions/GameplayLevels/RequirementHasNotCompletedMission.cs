using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b5.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementHasNotCompletedMission", menuName = "HardlightProject/Requirements/RequirementHasNotCompletedMission")]
    public class RequirementHasNotCompletedMission : RequirementGameplayLevelBase
    {
        [SerializeField] private MissionDefinition m_missionDefinition;
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

        // 0x06001f15 (x86-64 0x557cb0): without a MissionManager the requirement
        // is not met. The mission's own level comes from the DataManager lookup,
        // which throws for an unregistered mission; the levelDefinition argument
        // is not read.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (m_missionManagerRef.IsNull()) return false;
            GameplayLevelDefinition missionLevel = m_dataManagerRef.Get().MissionDefinitionLevelLookup[m_missionDefinition];
            SaveDataLevelMission missionData = m_missionManagerRef.Get()
                .GetSaveDataForMission(m_missionDefinition.GetGUID(), missionLevel.GetGUID());
            return !missionData.Complete;
        }

        // 0x06001f16: both field initializers run before the ScriptableObject base.
        public RequirementHasNotCompletedMission() { }
    }
}
