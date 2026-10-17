using System.IO;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000030, methods 0x06000140..0x06000146.
    // Build-time Firebase integration settings; the player never reads them.
    [CreateAssetMenu(fileName = "FirebaseConfigurationAsset", menuName = "Hardlight/HLUnityCore/FirebaseConfigurationAsset", order = 0)]
    public class FirebaseConfigurationAsset : SystemConfigurationAsset
    {
        [Tooltip("Allow module to automatically integrate Firebase during the post-build process?")]
        [SerializeField]
        private bool m_automaticIntegration = true;
        [SerializeField]
        [Header("Android")]
        [Tooltip("Path to google-services.json that is required by Firebase. Relative to Assets folder. If set, will be copied to the launcher Android project root.")]
        private string m_googleServicesJsonPath = string.Empty;
        [SerializeField]
        [Header("iOS")]
        [Tooltip("Path to GoogleService-Info.plist that is required by Firebase. Relative to Assets folder. If set, will be copied and added to the XCode project.")]
        private string m_googleServiceInfoPlist = string.Empty;
        [SerializeField]
        [Tooltip("Whether to inject a native call to [FIRApp configure]. It will be injected in didFinishLaunchingWithOptions callback as recommended by the docs. This will happen before calling ApplicationModuleBase::didFinishLaunchingWithOptions callbacks. Just used for convenience and projects can implement their own way of calling [FIRApp configure].")]
        private bool m_injectFirebaseConfigure;

        public bool AutomaticIntegration => m_automaticIntegration;
        internal string GoogleServicesJsonPath => m_googleServicesJsonPath;
        internal string GoogleServiceInfoPlist => m_googleServiceInfoPlist;
        internal bool InjectFirebaseConfigureCall => m_injectFirebaseConfigure;

        // 0x06000144: each path is checked under its field name.
        public override void Validate()
        {
            ValidateAssetPath(nameof(m_googleServicesJsonPath), m_googleServicesJsonPath);
            ValidateAssetPath(nameof(m_googleServiceInfoPlist), m_googleServiceInfoPlist);
        }

        // 0x06000145 (x86-64 0x1aa0130): blank paths are skipped; otherwise the
        // Assets-relative file is probed and the result discarded. varName and
        // the probe result are unused in the retail body, which is consistent
        // with a [Conditional] report (for instance inside an if on File.Exists)
        // compiled out of the retail build; the exact call is unknown, so only
        // the surviving probe is restored.
        private static void ValidateAssetPath(string varName, string varValue)
        {
            if (string.IsNullOrWhiteSpace(varValue)) return;
            File.Exists(Application.dataPath + "/" + varValue);
        }
    }
}
