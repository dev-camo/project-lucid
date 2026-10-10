using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200057d, complete twelve-method owner.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardMusicTrack", menuName = "HardlightProject/ChallengeReward/ChallengeRewardMusicTrack")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeRewardMusicTrack : ChallengeReward
    {
        [SerializeField] private MusicTrackDefinition m_reward;
        private readonly SystemRef<MusicTrackManager> m_musicTrackManagerRef =
            ProcessManager.GetSystemRef<MusicTrackManager>(null, true);

        // 0x06001dcc/1dcd, original enum value 2 and direct serialized reference.
        public override ChallengeRewardType RewardType => ChallengeRewardType.MusicTrack;
        public MusicTrackDefinition MusicTrack => m_reward;
        // 0x06001dce is genuinely empty.
        public override void Validate(UnityEngine.Object context) { }

        // 0x06001dcf: failed TryGet returns String.Empty; the successful branch
        // evaluates the virtual reward type before the live definition/index.
        public override string GetAnalyticsName()
        {
            if (m_musicTrackManagerRef.TryGet(out MusicTrackManager manager))
                return string.Format("{0}_{1}", RewardType, manager.GetTrackIndex(m_reward));
            return string.Empty;
        }

        // 0x06001dd0 uses the genuine GUID base; 1dd1 has no grant body.
        public override string GetRewardGUID() => m_reward.GetGUID();
        public override void GiveReward() { }
        // 0x06001dd2: original hashed Strings value 0xcf29f87d.
        public override Strings GetRewardName() => Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_MUSIC_TRACK;

        // 0x06001dd3/1dd4: original texture wrapper only. The sprite callback
        // is ignored; do not add callback/null/Unity-object checks.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            m_reward.ImageAssetTexture.LoadAsync(textureCallback);
        }
        public override void UnloadImage() => m_reward.ImageAssetTexture.Unload();

        // 0x06001dd5 actually collects during Save with refresh=true.
        public override void Save()
        {
            ProcessManager.GetSystem<MusicTrackManager>(null, true).CollectTrack(m_reward, true);
        }
        // 0x06001dd6 forwards the inherited threshold and requested track.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType rewardTrackType)
        {
            return new MetaGameUnlockMusicTrack(m_reward, m_xpThreshold, rewardTrackType);
        }
        // 0x06001dd7: the readonly reference initializer above runs before base.
        public ChallengeRewardMusicTrack() { }
    }
}
