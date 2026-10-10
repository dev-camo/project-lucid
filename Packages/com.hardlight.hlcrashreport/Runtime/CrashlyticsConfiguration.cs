using System;
using System.IO;
using UnityEngine;
using Version = Hardlight.Utils.Version;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "CrashlyticsConfiguration",
        menuName = "Hardlight/HLCrashReport/Crashlytics Configuration", order = 0)]
    public class CrashlyticsConfiguration : ScriptableObject
    {
        public enum VersionSelectOption { Default = 0, Manual = 1, HLFirebase = 2 }

        [SerializeField] private VersionSelectOption m_versionSelectOption;
        [Tooltip("Path to the Dependencies.xml file that has the Firebase version set. Relative to Assets/ folder. If provided, HLCrashReport will use the Pod version defined there.")]
        [ShowIf("m_versionSelectOption", VersionSelectOption.HLFirebase)]
        [SerializeField] private string m_firebaseDependenciesXmlPath = string.Empty;
        [SerializeField]
        [ShowIf("m_versionSelectOption", VersionSelectOption.Manual)]
        [Tooltip("Override the pod version if required. NOTE: that it may break the module due to version incompatibility.")]
        private Version m_podVersion;
        [SerializeField]
        [Tooltip("Override the Firebase BOM version if required. NOTE: that it may break the module due to version incompatibility.")]
        [ShowIf("m_versionSelectOption", VersionSelectOption.Manual)]
        private Version m_gradleBOMVersion;
        [Tooltip("Toggle whether to upload debug symbols to Crashlytics on build.")]
        [SerializeField]
        [Header("iOS")] private bool m_uploadSymbolsOnBuild = true;
        [Tooltip("Override the debug information format to \"DWARF with dSYM\".")]
        [SerializeField] private bool m_overrideDebugFormat;

        // HLCrashReport.Runtime 06000003..08: all six getters are internal.
        internal bool UploadSymbolsOnBuild => m_uploadSymbolsOnBuild;
        internal bool OverrideDebugFormat => m_overrideDebugFormat;
        internal Version PodVersion => m_podVersion;
        internal Version GradleBOMVersion => m_gradleBOMVersion;
        internal VersionSelectOption VersionSelect => m_versionSelectOption;
        internal string FirebaseDependenciesXmlPath => m_firebaseDependenciesXmlPath;

        // 06000009: preserve validation order and ignored results. This player
        // computes the Firebase path but contains no subsequent XML or file read.
        private void OnValidate()
        {
            string error = null;
            switch (m_versionSelectOption)
            {
                case VersionSelectOption.Default:
                    return;
                case VersionSelectOption.Manual:
                    string podVersion = m_podVersion.ToString();
                    if (podVersion != Version.DefaultString)
                        Version.IsVersionStringValid(podVersion, out error);
                    string gradleVersion = m_gradleBOMVersion.ToString();
                    if (gradleVersion != Version.DefaultString)
                        Version.IsVersionStringValid(gradleVersion, out error);
                    return;
                case VersionSelectOption.HLFirebase:
                    Path.Join(Application.dataPath, m_firebaseDependenciesXmlPath.Trim());
                    return;
                default:
                    throw new ArgumentException("Unconfigured case.", "m_versionSelectOption");
            }
        }

        // 0600000a: string.Empty and upload=true are initialized before the base.
        public CrashlyticsConfiguration() { }
    }
}
