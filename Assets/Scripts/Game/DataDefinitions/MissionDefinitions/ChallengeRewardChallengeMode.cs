using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000577, both methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardChallengeMode", menuName = "HardlightProject/ChallengeReward/ChallengeRewardChallengeMode")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ChallengeRewardChallengeMode : ChallengeRewardGameFeature
    {
        [SerializeField] private ChallengeCycleDefinition m_cycleDefinition;

        // 0x06001d95: the track type is ignored.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType)
        {
            return new MetaGameUnlockChallengesFeature(m_cycleDefinition);
        }

        // 0x06001d96: base-only constructor.
        public ChallengeRewardChallengeMode() { }
    }
}
