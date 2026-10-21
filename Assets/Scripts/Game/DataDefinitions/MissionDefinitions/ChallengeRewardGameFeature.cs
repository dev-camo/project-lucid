using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200057b, complete twelve-method owner. Base of the
    // challenge-mode and dream-power feature rewards, which only supply the unlock.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeRewardGameFeature : ChallengeReward
    {
        [HashEnum(null)] [SerializeField] private Strings m_featureName;
        [HashEnum(null)] [SerializeField] private Strings m_featureDescription;
        [SerializeField] private AssetReferenceAtlasedSprite m_featureSprite;
        private ManagedAddressableAsset<Sprite> m_spriteAsset;

        public override ChallengeRewardType RewardType => ChallengeRewardType.GameFeature; // 0x06001db2

        // 0x06001db3: a fresh wrapper per enable; nothing is loaded or released here.
        private void OnEnable()
        {
            m_spriteAsset = new ManagedAddressableAsset<Sprite>(m_featureSprite);
        }

        public override void Validate(UnityEngine.Object context) { } // 0x06001db4

        // 0x06001db5: virtual reward type first, then the feature name string.
        public override string GetAnalyticsName()
        {
            return string.Format("{0}_{1}", RewardType, m_featureName.GetString());
        }

        public override string GetRewardGUID() => m_guid; // 0x06001db6
        public override void GiveReward() { } // 0x06001db7
        public override Strings GetRewardName() => m_featureName; // 0x06001db8

        // 0x06001db9/1dba: the sprite is loaded synchronously and handed to the
        // sprite callback; the texture callback is unused.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            spriteCallback(m_spriteAsset.Load());
        }

        public override void UnloadImage() => m_spriteAsset.Unload();

        // 0x06001dbb: the saved record is found by this reward's own GUID.
        public override void Save()
        {
            SaveManager saveManager = s_saveManagerRef.Get();
            saveManager.CurrentSave.GetRewardDataByTrackGUIDUnsafe(m_guid).Collected = true;
            saveManager.RequestSave();
        }

        // 0x06001dbc: the feature subclasses supply the actual unlock.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType) => null;

        // 0x06001dbd: base-only constructor.
        public ChallengeRewardGameFeature() { }
    }
}
