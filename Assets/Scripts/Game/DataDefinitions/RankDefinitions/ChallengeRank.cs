using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class ChallengeRank : BaseRank, IComparable<ChallengeRank>
    {
        [SerializeField] private int m_score;
        public int Score => m_score;

        // Original06001ed1: reversed Int32.CompareTo, not subtraction; descending
        //sort avoids overflow and retains the original null-other failure.
        public int CompareTo(ChallengeRank other) => other.m_score.CompareTo(m_score);

        // Original06001ed2 is the implicit constructor. Its complete native body
        //inlines the real BaseRank constructor, including its SystemRef field init.
    }
}
