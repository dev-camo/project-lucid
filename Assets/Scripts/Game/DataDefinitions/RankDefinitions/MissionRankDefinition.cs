using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "MissionRankDefinition", menuName = "HardlightProject/DefinitionData/Definitions/MissionRankDefinition")]
    public sealed class MissionRankDefinition : ScriptableObject
    {
        [SerializeField] private SerializableDictionary<RankType, MissionRank> m_ranks =
            new SerializableDictionary<RankType, MissionRank>(HardlightEnumComparers.RankTypeComparer);
        private List<MissionRank> m_ranksList;

        // Original Game.Runtime 0x06001ee1, ARM64 0x52d244..0x52d24c.
        public SerializableDictionary<RankType, MissionRank> Ranks => m_ranks;

        // Original 0x06001ee2, ARM64 0x52d24c..0x52d4c4. Native KeyValuePair
        // Deconstruct is explicit; validation visits values in dictionary order.
        private void OnValidate()
        {
            foreach ((RankType rankType, MissionRank rank) in m_ranks)
                rank.Validate();
        }

        // Original 0x06001ee3, ARM64 0x52d4c4..0x52d594: a fresh list then Sort.
        private void OnEnable()
        {
            m_ranksList = new List<MissionRank>(m_ranks.Values);
            m_ranksList.Sort();
        }

        // Original 0x06001ee4, ARM64 0x52a3e4..0x52a69c; <>c predicate
        // 0x06001eea, ARM64 0x52d6c8..0x52d6f4 selects RankType.D.
        public bool TryGetRankForTimeSeconds(float timeSeconds, float minimumTime, out MissionRank outRank)
        {
            if (m_ranksList.Count > 0)
            {
                foreach (MissionRank rank in m_ranksList)
                {
                    if (timeSeconds > minimumTime && timeSeconds < rank.TimeInSeconds)
                    {
                        outRank = rank;
                        return true;
                    }
                }
                outRank = m_ranksList.Find(rank => rank.RankType == RankType.D);
                return outRank != null;
            }
            outRank = null;
            return false;
        }

        // Original 0x06001ee5, ARM64 0x52a7dc..0x52abf0; <>c predicate
        // 0x06001eeb, ARM64 0x52d6f4..0x52d720 has the same exact D test.
        public bool TryGetRankForTimeSecondsIgnoreMinimumTime(float timeSeconds, out MissionRank outRank)
        {
            if (m_ranksList.Count > 0)
            {
                foreach (MissionRank rank in m_ranksList)
                {
                    if (timeSeconds < rank.TimeInSeconds)
                    {
                        outRank = rank;
                        return true;
                    }
                }
                outRank = m_ranksList.Find(rank => rank.RankType == RankType.D);
                return outRank != null;
            }
            outRank = null;
            return false;
        }

        // Original 0x06001ee6, ARM64 0x52abf0..0x52ac68.
        public MissionRank GetBestRank()
        {
            return m_ranksList.Count > 0 ? m_ranksList[0] : null;
        }

        // Implicit original 0x06001ee7, ARM64 0x52d594..0x52d654. The original
        // comparer-backed dictionary is allocated before ScriptableObject..ctor.
        // Generated <>c 0x06001ee8/1ee9 and cache field ordinals6/7 remain
        // unverified until the real complete dependency graph compiles.
    }
}
