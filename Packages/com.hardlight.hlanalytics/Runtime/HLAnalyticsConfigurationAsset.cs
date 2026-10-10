using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x0200000a; complete three-method asset.
    [CreateAssetMenu(fileName = "AnalyticsConfiguration", menuName = "Hardlight/Analytics/Configuration")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAnalyticsConfigurationAsset : SystemConfigurationAsset
    {
        [Tooltip("If set to true, the editor options to generate data will be visible.")]
        [SerializeField] private bool m_enableEditorDataGeneration = true;
        // 0x06000065
        public bool EnableEditorDataGeneration => m_enableEditorDataGeneration;
        // 0x06000066: both original native bodies return immediately.
        public override void Validate() { }
        // 0x06000067: the authored default is set before the original base constructor.
        public HLAnalyticsConfigurationAsset() { }
    }
}
