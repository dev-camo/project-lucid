using Hardlight;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005a9.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementPlayerHasEnoughRewards", menuName = "HardlightProject/Requirements/RequirementPlayerHasEnoughRewards")]
    public sealed class RequirementPlayerHasEnoughRewards : RequirementGameplayLevelBase
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);

        // 0x06001efe (x86-64 0x556970): a level outside the game's level list is never
        // met. After the DebugUnlockLevels class initialisation (its inlined always-
        // false query is folded away, so the true return below is an unreachable
        // source stand-in, as in GameplayLevelDefinition.MeetsAllRequirements) every
        // reward requirement is compared with the collected total of its type; all of
        // them are always evaluated.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (!m_levelManagerRef.Get().GameLevels.GetLevels().Contains(levelDefinition)) return false;
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;

            var totalProgress = ProcessManager.GetSystem<ProgressionManager>(null, true).GetTotalProgress();
            bool allRewardsReached = true;
            foreach (RewardRequirement rewardRequirement in levelDefinition.RewardRequirements)
                allRewardsReached &= totalProgress.GetValueOrDefault(rewardRequirement.RewardType).Collected >= rewardRequirement.Count;
            return allRewardsReached;
        }

        // 0x06001eff.
        public RequirementPlayerHasEnoughRewards() { }
    }
}
