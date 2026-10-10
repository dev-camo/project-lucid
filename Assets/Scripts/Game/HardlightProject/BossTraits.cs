using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000499, all four fields and constructor 06001a65.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class BossTraits : ActorTraits
    {
        [Header("Boss.")]
        [Tooltip("How many times does the player need to attack the boss to win?")]
        public int HealthPool = 1;

        [Tooltip("Layers the boss interacts with and treats as the usable track.")]
        public LayerMask TrackMask = -1;

        [Tooltip("Does this boss use the TransformLimitedRotator component?")]
        public bool UsesSmoothedTransformRotator;

        // The repeated tooltip is the original authored field attribute.
        [Tooltip("Does this boss use the TransformLimitedRotator component?")]
        public bool DetachVisualProxy;

        // The implicit constructor initializes HealthPool then converts -1 to
        // LayerMask before calling the genuine ActorTraits constructor.
    }
}
