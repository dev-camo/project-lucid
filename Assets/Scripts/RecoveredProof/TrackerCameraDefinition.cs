using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TrackerCameraDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TrackerCameraDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TrackerCameraDefinition : ScriptableObject
    {
        // Original 0x040014d6; offset 0x18.
        [Tooltip("Free look camera override settings for the duration of tracking a surface forward.")]
        [SerializeField]
        private CameraHeadingOverride.CameraHeadingOverrideSettings m_forwardHeadingOverride;
        // Original 0x040014d7; offset 0x20.
        [Tooltip("Free look camera override settings for the duration of tracking a surface backward.")]
        [SerializeField]
        private CameraHeadingOverride.CameraHeadingOverrideSettings m_backwardHeadingOverride;

        public CameraHeadingOverride.CameraHeadingOverrideSettings ForwardHeadingOverride => m_forwardHeadingOverride;
        public CameraHeadingOverride.CameraHeadingOverrideSettings BackwardHeadingOverride => m_backwardHeadingOverride;

        // Original 06001f9b: forward update completes before the backward read.
        private void OnValidate()
        {
            m_forwardHeadingOverride.UpdateCachedValues();
            m_backwardHeadingOverride.UpdateCachedValues();
        }
        public TrackerCameraDefinition() { }
    }
}
