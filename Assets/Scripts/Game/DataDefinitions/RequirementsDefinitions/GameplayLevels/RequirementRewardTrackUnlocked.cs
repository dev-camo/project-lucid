using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b8.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementRewardTrackUnlocked", menuName = "HardlightProject/Requirements/RequirementRewardTrackUnlocked")]
    public class RequirementRewardTrackUnlocked : RequirementGameplayLevelBase
    {
        [SerializeField] private RewardTrackType m_rewardTrackType;
        private readonly SystemRef<SaveManager> m_saveManagerRef =
            ProcessManager.GetSystemRef<SaveManager>(null, true);

        // 0x06001f1b (x86-64 0x5587f0): without a SaveManager or an open save the
        // track is locked; otherwise any state past Locked counts as unlocked.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (!m_saveManagerRef.TryGet(out SaveManager saveManager)) return false;
            if (!saveManager.IsAnySaveOpen) return false;
            return saveManager.CurrentSave.GetOrCreateRewardTrackData(m_rewardTrackType).UnlockState !=
                SaveDataChallengeRewardTrack.FeatureUnlockState.Locked;
        }

        // 0x06001f1c.
        public RequirementRewardTrackUnlocked() { }
    }
}
