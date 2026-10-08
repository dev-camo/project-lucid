using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000207, 06000d68/06000d69.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FramerateConfiguration : VisualQualityConfiguration
    {
        [SerializeField] private int m_targetFramerate = 60;

        public override void Apply() => Application.targetFrameRate = m_targetFramerate;

        public FramerateConfiguration() { }
    }
}
