using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ScriptableRenderFeaturesConfiguration : VisualQualityConfiguration
    {
        [SerializeField] private ScriptableRendererFeature[] m_enabledRenderFeatures;
        [SerializeField] private ScriptableRendererFeature[] m_disabledRenderFeatures;

        // Game.Runtime 0x06002db6: each original array is captured for its own
        // pass. A null array faults; Unity-null entries are skipped. Enabled work
        // precedes disabled work, so a feature in both arrays ends disabled.
        public override void Apply()
        {
            foreach (ScriptableRendererFeature feature in m_enabledRenderFeatures)
            {
                if (feature == null)
                    continue;
                feature.SetActive(true);
            }
            foreach (ScriptableRendererFeature feature in m_disabledRenderFeatures)
            {
                if (feature == null)
                    continue;
                feature.SetActive(false);
            }
        }

        // 0x06002db7: original null arrays and genuine configuration base ctor.
        public ScriptableRenderFeaturesConfiguration() { }
    }
}
