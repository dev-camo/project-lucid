using System;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200057e, complete thirteen-method owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardOrnament", menuName = "HardlightProject/ChallengeReward/ChallengeRewardOrnament")]
    public class ChallengeRewardOrnament : ChallengeReward
    {
        [SerializeField] private OrnamentDefinition m_ornament;
        private readonly SystemRef<OrnamentManager> m_ornamentManagerRef =
            ProcessManager.GetSystemRef<OrnamentManager>(null, true);

        // 0x06001dd8/1dd9: enum value zero and direct original definition.
        public override ChallengeRewardType RewardType => ChallengeRewardType.Ornament;
        public OrnamentDefinition Ornament => m_ornament;
        public override void Validate(UnityEngine.Object context) { } // 0x06001dda
        // 0x06001ddb: virtual type first, then the original identifier GetString.
        public override string GetAnalyticsName()
        {
            return string.Format("{0}_{1}", RewardType, m_ornament.OrnamentIdentifier.GetString());
        }
        public override string GetRewardGUID() => m_ornament.GetGUID(); // 0x06001ddc
        // 0x06001ddd gets the manager before evaluating identifier and threshold.
        public override void GiveReward()
        {
            m_ornamentManagerRef.Get().AwardOrnament(m_ornament.OrnamentIdentifier, m_xpThreshold);
        }
        // 0x06001dde: original hashed Strings value 0xf3694723.
        public override Strings GetRewardName() => Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_STATUE;
        // 0x06001ddf/1de0 read offset 0x60, the UNCROPPED wrapper rather than
        // public ThumbnailAsset at 0x68. The sprite callback is not used.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            m_ornament.ThumbnailUncroppedAsset.LoadAsync(textureCallback);
        }
        public override void UnloadImage() => m_ornament.ThumbnailUncroppedAsset.Unload();
        public override void Save() // 0x06001de1
        {
            ProcessManager.GetSystem<OrnamentManager>(null, true).CollectOrnament(m_ornament);
        }
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType rewardTrackType) // 0x06001de2
        {
            return new MetaGameUnlockOrnament(m_ornament, m_xpThreshold, rewardTrackType);
        }
        // 0x06001de3 invokes the genuine base event first; only after it succeeds
        // does the original statue event receive definition/null/null/threshold.
        public override void FireAnalyticsEvents(RewardTrackType rewardTrackType, int rewardNumber)
        {
            base.FireAnalyticsEvents(rewardTrackType, rewardNumber);
            AnalyticsEventCollector.StatueCollectedEvent(m_ornament, null, null, m_xpThreshold);
        }
        public ChallengeRewardOrnament() { } // 0x06001de4, pre-base reference initializer
    }
}
