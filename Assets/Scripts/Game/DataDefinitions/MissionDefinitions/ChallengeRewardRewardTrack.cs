using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200057f, complete thirteen-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardRewardTrack", menuName = "HardlightProject/ChallengeReward/ChallengeRewardRewardTrack")]
    public class ChallengeRewardRewardTrack : ChallengeReward
    {
        [SerializeField] private RewardTrackDefinition m_rewardDefinition;
        public override ChallengeRewardType RewardType => ChallengeRewardType.RewardTrack; // 0x06001de5
        // 0x06001de6 really names this definition-valued property RewardTrackType.
        public RewardTrackDefinition RewardTrackType => m_rewardDefinition;
        public override void Validate(UnityEngine.Object context) { } // 0x06001de7
        public override string GetAnalyticsName() // 0x06001de8
        {
            return string.Format("{0}_{1}", RewardType, m_rewardDefinition.Type.GetString());
        }
        public override string GetRewardGUID() => m_rewardDefinition.GetGUID(); // 0x06001de9
        public override void GiveReward() // 0x06001dea, original state value 10
        {
            ProcessManager.GetSystem<ChallengeManager>(null, true).SetRewardTrackState(
                m_rewardDefinition.Type, SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked);
        }
        public override Strings GetRewardName() => m_rewardDefinition.TrackName; // 0x06001deb
        // 0x06001dec/1ded use only IconAsset and the sprite callback.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            m_rewardDefinition.IconAsset.LoadAsync(spriteCallback);
        }
        public override void UnloadImage() => m_rewardDefinition.IconAsset.Unload();

        // 0x06001dee captures manager then CurrentSave. The two record mutations
        // precede RequestSave and RefreshXPRewards, with no added rollback/guards.
        public override void Save()
        {
            SaveManager saveManager = s_saveManagerRef.Get();
            SaveDataGame saveData = saveManager.CurrentSave;
            saveData.GetOrCreateRewardTrackData(m_rewardDefinition.Type).UnlockState =
                SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked;
            saveData.GetRewardDataByRewardGUIDUnsafe(GetRewardGUID()).Collected = true;
            saveManager.RequestSave();
            ProcessManager.GetSystem<ChallengeManager>(null, true).RefreshXPRewards();
        }
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType) // 0x06001def
        {
            return new MetaGameUnlockRewardTrack(m_rewardDefinition, m_xpThreshold, trackType);
        }
        // 0x06001df0: actual Large hash 0x51c014f1, independent of Size getter.
        protected override UIWidgetButtonChallengeRewardSize GetDefaultRewardSize()
        {
            return UIWidgetButtonChallengeRewardSize.Large;
        }
        public ChallengeRewardRewardTrack() { } // 0x06001df1, genuine base-only constructor
    }
}
