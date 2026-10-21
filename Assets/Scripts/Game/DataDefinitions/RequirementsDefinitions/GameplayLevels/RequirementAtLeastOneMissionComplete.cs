using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b0.
    [CreateAssetMenu(fileName = "RequirementAtLeastOneMissionComplete", menuName = "HardlightProject/Requirements/RequirementAtLeastOneMissionComplete")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RequirementAtLeastOneMissionComplete : RequirementGameplayLevelBase
    {
        private readonly SystemRef<SaveManager> m_saveManagerRef =
            ProcessManager.GetSystemRef<SaveManager>(null, true);

        // 0x06001f0b (x86-64 0x5577f0): the level's save record is created on
        // demand; a missing SaveManager or level definition faults.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            SaveDataLevel levelData = m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
            return levelData.GetCompleteMissionCount() > 0;
        }

        // 0x06001f0c.
        public RequirementAtLeastOneMissionComplete() { }
    }
}
