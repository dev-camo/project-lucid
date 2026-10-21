using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200057c, complete fourteen-method owner.
    [CreateAssetMenu(fileName = "ChallengeRewardGameplayLevel", menuName = "HardlightProject/ChallengeReward/ChallengeRewardGameplayLevel")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeRewardGameplayLevel : ChallengeReward
    {
        [SerializeField] private GameplayLevelDefinition m_reward;

        public override ChallengeRewardType RewardType => ChallengeRewardType.GameplayLevel; // 0x06001dbe
        public GameplayLevelDefinition LevelDefinition => m_reward; // 0x06001dbf
        public override void Validate(UnityEngine.Object context) { } // 0x06001dc0

        // 0x06001dc1: virtual reward type first, then the level's scene name.
        public override string GetAnalyticsName()
        {
            return string.Format("{0}_{1}", RewardType, m_reward.SceneName);
        }

        public override string GetRewardGUID() => m_reward.GetGUID(); // 0x06001dc2

        // 0x06001dc3: the saved record is found by this reward's own GUID, not the level's.
        public override void GiveReward()
        {
            SaveManager saveManager = s_saveManagerRef.Get();
            saveManager.CurrentSave.GetRewardDataByTrackGUIDUnsafe(m_guid).Collected = true;
            saveManager.RequestSave();
        }

        // 0x06001dc4: the level's act name (offset 0x24), not its display name.
        public override Strings GetRewardName() => m_reward.ActName;

        // 0x06001dc5/1dc6 forward to the level's own image loader; the texture callback is unused.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            m_reward.LoadLevelImage(spriteCallback);
        }

        public override void UnloadImage() => m_reward.UnloadLevelImage();

        // 0x06001dc7: same record lookup as GiveReward, through the process-manager system.
        public override void Save()
        {
            SaveManager saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            saveManager.CurrentSave.GetRewardDataByTrackGUIDUnsafe(m_guid).Collected = true;
            saveManager.RequestSave();
        }

        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType) // 0x06001dc8
        {
            return new MetaGameUnlockLevel(m_reward);
        }

        // 0x06001dc9: the title of the zone containing the level; None when it has none.
        public Strings GetZoneName()
        {
            if (ProcessManager.GetSystem<LevelManager>(null, true).GameLevels.TryGetZoneForLevel(m_reward, out GameplayLevelCategory zone))
                return zone.Title;
            return Strings.NONE;
        }

        // 0x06001dca: original Large hash 0x51c014f1.
        protected override UIWidgetButtonChallengeRewardSize GetDefaultRewardSize()
        {
            return UIWidgetButtonChallengeRewardSize.Large;
        }

        // 0x06001dcb: base-only constructor.
        public ChallengeRewardGameplayLevel() { }
    }
}
