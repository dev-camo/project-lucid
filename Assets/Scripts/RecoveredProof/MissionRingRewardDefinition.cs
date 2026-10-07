using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000584, including its original public nested data class.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "MissionRingRewardDefinition", menuName = "HardlightProject/DefinitionData/Definitions/MissionRingRewardDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionRingRewardDefinition : ScriptableObjectWithGuid
    {
        // Original 0x040013f4; instance offset 0x20. No constructor allocation.
        [SerializeField] private RewardData[] m_rewardData;

        // Original 0x06001e4d, ARM64 0x52b158..0x52b190.
        // Scan authored entries backwards and take the first inclusive threshold.
        // Ordering, repeated thresholds and negative rewards remain authored values.
        public int GetRingRewardXP(int ringCount)
        {
            for (int index = m_rewardData.Length - 1; index >= 0; index--)
            {
                RewardData reward = m_rewardData[index];
                if (reward.RingCount <= ringCount)
                    return reward.RewardXP;
            }
            return 0;
        }

        // Original 0x06001e4e: genuine ScriptableObjectWithGuid constructor only.
        public MissionRingRewardDefinition() { }

        // Original 0x02000585: Serializable, NestedPublic, two private serialized fields.
        [Serializable]
        public class RewardData
        {
            // Original 0x040013f5 and 0x040013f6; offsets 0x10 and 0x14.
            [SerializeField] private int m_ringCount;
            [SerializeField] private int m_rewardXP;

            // Original 0x06001e4f and 0x06001e50: direct original field reads.
            public int RingCount => m_ringCount;
            public int RewardXP => m_rewardXP;

            // Original 0x06001e51: Object construction, zero fields unchanged.
            public RewardData() { }
        }
    }
}
