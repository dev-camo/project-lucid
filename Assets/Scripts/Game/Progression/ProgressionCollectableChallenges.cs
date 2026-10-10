using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ProgressionCollectableChallenges
    {
        private readonly ChallengeManager m_challengeManager;
        private readonly DreamPowerStoreManager m_dreamPowerStoreManager;
        private readonly Dictionary<CollectableType, ProgressionCollectable> m_progress =
            new Dictionary<CollectableType, ProgressionCollectable>(HardlightEnumComparers.CollectableTypeComparer);

        // Game.Runtime 06002ae5: return the live progress dictionary.
        public IReadOnlyDictionary<CollectableType, ProgressionCollectable> Progress => m_progress;

        // 06002ae6: the dictionary initializer runs before the base constructor; both system
        // lookups occur afterward, challenge manager first. Retain normal auto-registration.
        public ProgressionCollectableChallenges()
        {
            m_challengeManager = ProcessManager.GetSystem<ChallengeManager>(null, true);
            m_dreamPowerStoreManager = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true);
        }

        // 06002ae7: only the original blue-coin and music-track reward categories enter this
        // collectable view. Reward GUID lookup happens before updating that category's count.
        public void CacheChallengesProgress(SaveDataGame saveData)
        {
            m_progress.Clear();
            foreach (var (rewardType, rewards) in m_challengeManager.ChallengeRewardsByType)
            {
                CollectableType type;
                if (rewardType == ChallengeReward.ChallengeRewardType.BlueCoins) type = CollectableType.BlueCoin;
                else if (rewardType == ChallengeReward.ChallengeRewardType.MusicTrack) type = CollectableType.MusicTrack;
                else continue;
                foreach (ChallengeReward reward in rewards)
                {
                    SaveDataChallengeReward rewardSaveData = saveData.GetRewardDataByTrackGUIDUnsafe(reward.GetGUID());
                    if (!m_progress.TryGetValue(type, out ProgressionCollectable value))
                        value = new ProgressionCollectable(type, 0, 0);
                    m_progress[type] = AddCollectedCount(value, reward, rewardSaveData);
                }
            }
            ProgressionCollectable blueCoins = m_progress.GetValueOrDefault(CollectableType.BlueCoin);
            int startingCoins = m_dreamPowerStoreManager.GetBlueCoinCount();
            m_progress[CollectableType.BlueCoin] = new ProgressionCollectable(CollectableType.BlueCoin,
                unchecked(blueCoins.Collected + startingCoins), unchecked(blueCoins.Total + startingCoins));
        }

        // 06002ae8: blue-coin rewards contribute their authored amount; every other reward
        // contributes one, including null. Only an existing collected save record adds to Collected.
        private static ProgressionCollectable AddCollectedCount(ProgressionCollectable collectable, ChallengeReward reward, SaveDataChallengeReward rewardSaveData)
        {
            int amount = reward is ChallengeRewardBlueCoins blueCoins ? blueCoins.BlueCoinAmount : 1;
            return new ProgressionCollectable(collectable.Type,
                unchecked(collectable.Collected + (rewardSaveData != null && rewardSaveData.Collected ? amount : 0)),
                unchecked(collectable.Total + amount));
        }
    }
}
