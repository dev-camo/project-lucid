using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000580, complete twelve-method owner.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardSkin", menuName = "HardlightProject/ChallengeReward/ChallengeRewardSkin")]
    public class ChallengeRewardSkin : ChallengeReward
    {
        [SerializeField] private CharacterSkinDefinition m_reward;

        public override ChallengeRewardType RewardType => ChallengeRewardType.Skin; // 0x06001df2
        public CharacterSkinDefinition Reward => m_reward; // 0x06001df3
        public override void Validate(UnityEngine.Object context) { } // 0x06001df4

        // 0x06001df5: virtual reward type first, then the skin's name string.
        public override string GetAnalyticsName()
        {
            return string.Format("{0}_{1}", RewardType, m_reward.Name.GetString());
        }

        public override string GetRewardGUID() => m_reward.GetGUID(); // 0x06001df6
        public override void GiveReward() { } // 0x06001df7

        // 0x06001df8: original hashed Strings value 0x8c035df3.
        public override Strings GetRewardName() => Strings.MENU_CHARACTER_SKIN_SELECT;

        // 0x06001df9/1dfa use the skin's thumbnail wrapper and the sprite callback only.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            m_reward.Thumbnail.LoadAsync(spriteCallback);
        }

        public override void UnloadImage() => m_reward.Thumbnail.Unload();

        // 0x06001dfb: the skin is unlocked by its own GUID, the reward record is found by this
        // reward's GUID (each reads CurrentSave afresh), then the save is requested and the
        // challenge XP rewards are refreshed.
        public override void Save()
        {
            SaveManager saveManager = s_saveManagerRef.Get();
            saveManager.CurrentSave.GetOrCreateSkinData(m_reward.GetGUID()).Unlocked = true;
            saveManager.CurrentSave.GetRewardDataByTrackGUIDUnsafe(m_guid).Collected = true;
            saveManager.RequestSave();
            ProcessManager.GetSystem<ChallengeManager>(null, true).RefreshXPRewards();
        }

        // 0x06001dfc: skin, inherited XP threshold, requested track.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType)
        {
            return new MetaGameUnlockCharacterSkin(m_reward, m_xpThreshold, trackType);
        }

        // 0x06001dfd: base-only constructor.
        public ChallengeRewardSkin() { }
    }
}
