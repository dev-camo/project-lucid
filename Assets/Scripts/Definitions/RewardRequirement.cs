using System;
using UnityEngine;

namespace HardlightProject
{
    // Game.Runtime.dll 0x0200041d: complete method-free original value type.
    [Serializable]
    public struct RewardRequirement
    {
        [SerializeField] public CollectableType RewardType;
        [SerializeField] public int Count;
    }
}
