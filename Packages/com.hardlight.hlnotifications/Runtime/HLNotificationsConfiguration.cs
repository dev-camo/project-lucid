using UnityEngine;
using UnityEngine.Serialization;

namespace Hardlight
{
    // HLNotifications.Runtime 0x02000003. Preserve the actual Gradle asset contract.
    [CreateAssetMenu(fileName = "NotificationsConfiguration", menuName = "Hardlight/HLNotifications/Configuration", order = 0)]
    public class HLNotificationsConfiguration : SystemConfigurationAsset, IGradleFileConfig
    {
        [FormerlySerializedAs("m_pathToGradleTemplateFile")]
        [SerializeField]
        [Tooltip("Path to template Gradle configuration file. Relative to Unity's Assets/ directory. Will use the default gradle template if not set.")]
        private string m_gradleTemplateFileOverride = string.Empty;
        [Tooltip("Path to where the Gradle configuration file will be generated from the template (see m_gradleTemplateFileOverride). Relative to Unity's Assets/ directory. Will use the default path if not set.")]
        [SerializeField]
        [FormerlySerializedAs("m_pathToGradleFile")]
        private string m_gradleFileOverride = string.Empty;

        // 0x06000011 / 0x06000012 / 0x06000013. DefaultGradlePath is inherited
        // from the genuine default-interface implementation, not an extra own method.
        public string PathToGradleTemplateFile => m_gradleTemplateFileOverride;
        public string PathToGradleFile => m_gradleFileOverride;
        public string DefaultGradleTemplatePath => "/NativeAndroid~/build.2019gradle";

        // 0x06000014. Each field is evaluated before its own fresh dataPath read;
        // the original diagnostic then rereads that field after the file test.
        public override void Validate()
        {
            if (!FileExists(m_gradleTemplateFileOverride, Application.dataPath))
                HLOutput.LogError("Path to HLUnityCore Gradle template file not valid: " + m_gradleTemplateFileOverride, null);
            if (!FileExists(m_gradleFileOverride, Application.dataPath))
                HLOutput.LogError("Path to HLUnityCore Gradle file not valid: " + m_gradleFileOverride, null);
        }

        // 0x06000015; both string initialisers precede the original base constructor.
        public HLNotificationsConfiguration() { }
    }
}
