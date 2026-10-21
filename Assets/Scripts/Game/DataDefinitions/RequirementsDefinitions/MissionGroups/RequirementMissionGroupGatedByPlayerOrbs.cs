using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005bc.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RequirementMissionGroupGatedByPlayerOrbs", menuName = "HardlightProject/DefinitionData/Definitions/RequirementMissionGroupGatedByPlayerOrbs")]
    public sealed class RequirementMissionGroupGatedByPlayerOrbs : RequirementMissionGroupBase
    {
        [SerializeField] private int m_orbs;

        // 0x06001f23 (x86-64 0x558d50): the retail body begins with the class
        // initialisation of DebugUnlockLevels (its inlined always-false query is
        // folded away, so the true return below is an unreachable source stand-in, as
        // in GameplayLevelDefinition.MeetsAllRequirements). The player needs at least
        // m_orbs collected orbs; neither argument is read.
        public override bool RequirementsMet(MissionGroup missionGroup, GameplayLevelDefinition gameplayLevelDefinition)
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            ProgressionManager progressionManager = ProcessManager.GetSystem<ProgressionManager>(null, true);
            return m_orbs <= progressionManager.GetTotalProgress(CollectableType.Orb).Collected;
        }

        // 0x06001f24.
        public RequirementMissionGroupGatedByPlayerOrbs() { }
    }
}
