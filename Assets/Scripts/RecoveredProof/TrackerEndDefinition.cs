using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class TrackerEndDefinition : ScriptableObject
    {
        // Original 0x040014d8; offset 0x18.
        [Tooltip("Distance from end to force directional movement.")]
        [SerializeField]
        private float m_forceDirectionDistance;

        public float ForceDirectionDistance => m_forceDirectionDistance;
        protected TrackerEndDefinition() { }
    }
}
