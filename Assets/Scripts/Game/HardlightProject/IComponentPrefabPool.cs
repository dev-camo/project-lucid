using Hardlight;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200077a. These are abstract contracts; the shipping
    // metadata supplies no method bodies. The inherited pool belongs to Hardlight.
    public interface IComponentPrefabPool : IPrefabPool
    {
        bool Valid { get; }
        Bounds CullBounds { get; }
        float CullDistanceSqr { get; }
    }
}
