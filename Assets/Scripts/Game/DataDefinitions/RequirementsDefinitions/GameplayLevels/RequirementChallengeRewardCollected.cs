using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005a5.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RequirementChallengeRewardCollected", menuName = "HardlightProject/Requirements/RequirementChallengeRewardCollected")]
    public class RequirementChallengeRewardCollected : RequirementGameplayLevelBase
    {
        [SerializeField] private ChallengeReward m_challengeReward;
        private static readonly SystemRef<SaveManager> s_saveManagerRef =
            ProcessManager.GetSystemRef<SaveManager>(null, true);

        // 0x06001ef6 (x86-64 0x556770): a missing SaveManager, or a reward with
        // no save record yet, is simply not collected.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (!s_saveManagerRef.TryGet(out SaveManager saveManager)) return false;
            SaveDataGame saveDataGame = saveManager.CurrentSave;
            SaveDataChallengeReward rewardData =
                saveDataGame.GetRewardDataByRewardGUIDUnsafe(m_challengeReward.GetRewardGUID());
            return rewardData != null && rewardData.Collected;
        }

        // 0x06001ef7: only the ScriptableObject constructor runs.
        public RequirementChallengeRewardCollected() { }
    }
}
