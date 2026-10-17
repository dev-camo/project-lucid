// Complete original type candidate; source/runtime/serialization acceptance pending.
using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CameraRecenterHeadingDefinition : ScriptableObject
    {
        [Tooltip("How long before we stop trying to snap behind the character e.g. they are rotating at the same speed as we are trying to snap behind.")]
        [SerializeField]
        private float m_timeoutSeconds = 3f;
        [SerializeField]
        [Tooltip("When snapping to character, snap logic will be exited when the angle between the camera forward and character forward is less than or equal to this.")]
        private float m_forwardAngleExitSnapThreshold;
        [SerializeField]
        [Tooltip("Once the camera has finished snapping, the sticky controls will be cleared and set back to normal.")]
        private bool m_clearStickyControlsOnSnapEnd;
        public float TimeoutSeconds => m_timeoutSeconds;
        public float ForwardAngleExitSnapThreshold => m_forwardAngleExitSnapThreshold;
        public bool ClearStickyControlsOnSnapEnd => m_clearStickyControlsOnSnapEnd;
        // Original .ctor 0x06001a70 (x86-64 0x53fb10): stores 0x40400000 (3f) to
        // m_timeoutSeconds at +0x18, then tail-calls ScriptableObject .ctor.
        protected CameraRecenterHeadingDefinition() { }
    }
}
