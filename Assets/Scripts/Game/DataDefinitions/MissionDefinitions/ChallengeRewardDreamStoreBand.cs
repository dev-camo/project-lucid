using System;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200057a, complete thirteen-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardDreamStoreBand", menuName = "HardlightProject/ChallengeReward/ChallengeRewardDreamStoreBand")]
    public class ChallengeRewardDreamStoreBand : ChallengeReward
    {
        [SerializeField] private DreamPowerStoreBandDefinition m_rewardDefinition;

        public override ChallengeRewardType RewardType => ChallengeRewardType.DreamPowerStoreBand; // 0x06001da5
        public DreamPowerStoreBandDefinition DreamPowerStoreBand => m_rewardDefinition; // 0x06001da6
        public override void Validate(UnityEngine.Object context) { } // 0x06001da7

        // 0x06001da8: virtual reward type first, then the band type's identifier string.
        public override string GetAnalyticsName()
        {
            return string.Format("{0}_{1}", RewardType, m_rewardDefinition.Type.GetString());
        }

        public override string GetRewardGUID() => m_rewardDefinition.GetGUID(); // 0x06001da9
        public override void GiveReward() { } // 0x06001daa

        // 0x06001dab: original hashed Strings value 0xcfd0dbdb.
        public override Strings GetRewardName() => Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_DREAM_POWER_STORE_TIER;

        // 0x06001dac/1dad use only the band's sprite wrapper and the sprite callback.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            m_rewardDefinition.ImageAsset.LoadAsync(spriteCallback);
        }

        public override void UnloadImage() => m_rewardDefinition.ImageAsset.Unload();

        // 0x06001dae: the reward record is found by the virtual reward GUID.
        public override void Save()
        {
            SaveManager saveManager = Hardlight.ProcessManager.GetSystem<SaveManager>(null, true);
            SaveDataGame saveData = saveManager.CurrentSave;
            saveData.GetRewardDataByRewardGUIDUnsafe(GetRewardGUID()).Collected = true;
            saveManager.RequestSave();
        }

        // 0x06001daf: definition, inherited XP threshold, requested track.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType)
        {
            return new MetaGameUnlockDreamPowerStoreBand(m_rewardDefinition, m_xpThreshold, trackType);
        }

        // 0x06001db0: original Large hash 0x51c014f1.
        protected override UIWidgetButtonChallengeRewardSize GetDefaultRewardSize()
        {
            return UIWidgetButtonChallengeRewardSize.Large;
        }

        // 0x06001db1: base-only constructor.
        public ChallengeRewardDreamStoreBand() { }
    }
}
