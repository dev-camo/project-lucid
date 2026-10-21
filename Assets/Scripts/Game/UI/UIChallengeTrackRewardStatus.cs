using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200085a; bodies translated from the 1.10.1 x86-64 binary.
    // One reward of a challenge reward track as the UI sees it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIChallengeTrackRewardStatus
    {
        // 0x06003068..0x0600306b
        public ChallengeReward Reward { get; }
        public bool Unlocked { get; }
        public bool Collected { get; private set; }

        private readonly int m_rewardNumber;
        private readonly RewardTrackType m_rewardTrackType;

        // 0x0600306c (0x60F860): unlocked and not yet collected.
        public bool IsReadyToCollect()
        {
            return Unlocked && !Collected;
        }

        // 0x0600306d (0x60F880)
        public UIChallengeTrackRewardStatus(ChallengeReward reward, bool collected, bool unlocked, int rewardNumber, RewardTrackType rewardTrackType)
        {
            Reward = reward;
            Collected = collected;
            Unlocked = unlocked;
            m_rewardNumber = rewardNumber;
            m_rewardTrackType = rewardTrackType;
        }

        // 0x0600306e (0x60F8E0): the reward saves itself, then the entry is marked collected.
        public void Save()
        {
            Reward.Save();
            Collected = true;
        }

        // 0x0600306f (0x60F910): a tail call into the reward's virtual analytics hook.
        public void FireAnalyticsEvents()
        {
            Reward.FireAnalyticsEvents(m_rewardTrackType, m_rewardNumber);
        }

        // 0x06003070 (0x60F940)
        public IMetaGameUnlock GetMetaGameUnlock()
        {
            return Reward.GetMetaGameUnlock(m_rewardTrackType);
        }
    }
}
