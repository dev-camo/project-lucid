using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0200021a, 06000da5/06000da6.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UnityQualityConfiguration : VisualQualityConfiguration
    {
        [SerializeField] private string m_unityQualitySetting = "Medium";

        public override void Apply()
        {
            string[] names = QualitySettings.names;
            int index = -1;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(m_unityQualitySetting, names[i]))
                {
                    index = i;
                    break;
                }
            // A missing authored name logs, then still passes -1 to Unity.
            // The original rereads the authored field for the diagnostic.
            if (index == -1)
                HLOutput.LogError("Quality setting \"" + m_unityQualitySetting +
                    "\" not found, please check available names in engine QualitySettings", null);
            QualitySettings.SetQualityLevel(index);
        }

        public UnityQualityConfiguration() { }
    }
}
