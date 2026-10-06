using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    public struct CollectableChangeData
    {
        public HardlightProject.CollectableType Type;
        public int FixedChange;
        [UnityEngine.Range(-1f, 1f)]
        [UnityEngine.Tooltip("Multiplied by total number collected.")]
        public float FractionChange;
        public HardlightProject.CollectableChangeMetadata Metadata;
        // Original06003b0c short-circuits nonzero fixed change; NaN fraction
        // also compares unequal to zero, while both signed zeroes are false.
        public bool HasChange() => FixedChange != 0 || FractionChange != 0f;
    }
    [Serializable]
    public struct CollectableChangeMetadata
    {
        public HardlightProject.CollectableSource Source;
    }
}
