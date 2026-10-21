using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005ac.
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementRewardLevelUnlocked", menuName = "HardlightProject/Requirements/RequirementRewardLevelUnlocked")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RequirementRewardLevelUnlocked : RequirementGameplayLevelBase
    {
        private readonly SystemRef<SaveManager> m_saveManagerRef =
            ProcessManager.GetSystemRef<SaveManager>(null, true);

        // 0x06001f04 (x86-64 0x557510): the level GUID doubles as the reward
        // GUID, and the level counts as unlocked once that reward is collected.
        // A missing SaveManager or open save faults; an unknown reward is false.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            SaveDataChallengeReward rewardData =
                m_saveManagerRef.Get().CurrentSave.GetRewardDataByRewardGUIDUnsafe(levelDefinition.GetGUID());
            return rewardData != null && rewardData.Collected;
        }

        // 0x06001f05.
        public RequirementRewardLevelUnlocked() { }
    }
}
