using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "ChallengeRankDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ChallengeRankDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeRankDefinition : ScriptableObject
    {
        [SerializeField] private SerializableDictionary<RankType, ChallengeRank> m_ranks =
            new SerializableDictionary<RankType, ChallengeRank>(HardlightEnumComparers.RankTypeComparer);
        [SerializeField] private int m_sRankBonus = 25;
        private List<ChallengeRank> m_ranksList;

        public SerializableDictionary<RankType, ChallengeRank> Ranks => m_ranks;
        public int SRankBonus => m_sRankBonus;

        //06001ed5: publish a new dictionary-values list before Sort. The cached
        //list is not refreshed when callers mutate the returned dictionary.
        private void OnEnable()
        {
            m_ranksList = new List<ChallengeRank>(m_ranks.Values);
            m_ranksList.Sort();
        }

        //06001ed6: ordered first inclusive threshold wins. Below every threshold,
        //the real original fallback is RankType.D (0x5c4c95fb), not a guessed
        //minimum rank. Empty caches yield null; uninitialized/null entries retain
        //their original failures. The fallback predicate represents natural3.
        public bool TryGetRankForScore(int score, out ChallengeRank outRank)
        {
            if (m_ranksList.Count == 0)
            {
                outRank = null;
                return false;
            }
            for (int i = 0; i < m_ranksList.Count; i++)
            {
                if (score >= m_ranksList[i].Score)
                {
                    outRank = m_ranksList[i];
                    return true;
                }
            }
            outRank = m_ranksList.Find(rank => rank.RankType == RankType.D);
            return outRank != null;
        }

        //06001ed7: sorted cache head, or null only when the cache is empty.
        public ChallengeRank GetBestRank()
            => m_ranksList.Count > 0 ? m_ranksList[0] : null;

        // Implicit06001ed8: real enum comparer/dictionary initialization, then25,
        //then genuine ScriptableObject constructor. No initial list is allocated.
    }
}
