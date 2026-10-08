using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000216, 06000d94/06000d95.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ResolutionScaleConfiguration : VisualQualityConfiguration
    {
        [Range(0.1f, 1f), SerializeField] private float m_resolutionScale = 1f;

        public override void Apply() =>
            ScalableBufferManager.ResizeBuffers(m_resolutionScale, m_resolutionScale);

        public ResolutionScaleConfiguration() { }
    }
}
