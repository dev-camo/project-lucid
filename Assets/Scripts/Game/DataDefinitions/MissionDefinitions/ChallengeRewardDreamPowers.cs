using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000579, both methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardDreamPowers", menuName = "HardlightProject/ChallengeReward/ChallengeRewardDreamPowers")]
    public class ChallengeRewardDreamPowers : ChallengeRewardGameFeature
    {
        [SerializeField] private DreamPowerStoreDefinition m_dreamPowerStoreDefinition;

        // 0x06001da3: the track type is ignored.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType)
        {
            return new MetaGameUnlockDreamPowerStore(m_dreamPowerStoreDefinition);
        }

        // 0x06001da4: base-only constructor.
        public ChallengeRewardDreamPowers() { }
    }
}
