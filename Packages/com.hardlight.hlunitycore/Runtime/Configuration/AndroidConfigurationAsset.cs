using UnityEngine;
using UnityEngine.Serialization;

namespace Hardlight
{
	[CreateAssetMenu(fileName = "AndroidConfigurationAsset", menuName = "Hardlight/HLUnityCore/AndroidConfigurationAsset", order = 0)]
	public class AndroidConfigurationAsset : SystemConfigurationAsset, IGradleFileConfig
	{
		[FormerlySerializedAs("m_pathToGradleTemplateFile")]
		[Tooltip("Path to template Gradle configuration file. Relative to Unity's Assets/ directory. Will use the default gradle template if not set.")]
		[SerializeField]
		private string m_gradleTemplateFileOverride;

		[Tooltip("Path to where the Gradle configuration file will be generated from the template (see m_gradleTemplateFileOverride). Relative to Unity's Assets/ directory. Will use the default path if not set.")]
		[SerializeField]
		[FormerlySerializedAs("m_pathToGradleFile")]
		private string m_gradleFileOverride;

		[SerializeField]
		[Tooltip("Path to the UnityPlayerActivity.java source file. Relative to the BuildOutput directory.")]
		private string m_pathToUnityPlayerActivitySourceFile = "/unityLibrary/src/main/java/com/unity3d/player/UnityPlayerActivity.java";

		[Tooltip("Destination path where to copy the UnityPlayerActivity.java so HLUnityCore can inject itself (see m_pathToUnityPlayerActivitySourceFile). Relative to the Unity's Assets/ directory. Will use the default path if not set.")]
		[SerializeField]
		[FormerlySerializedAs("m_pathToUnityPlayerActivityDestination")]
		private string m_unityPlayerActivityFileOverride;

		[Tooltip("The Gradle plugin dependency. Usually called 'Plugin' or 'GameLib'")]
		[SerializeField]
		private string m_gradlePluginDependencyImplementation = "implementation project(path: ':Plugin')";

		public string PathToGradleTemplateFile => m_gradleTemplateFileOverride;

		public string PathToGradleFile => m_gradleFileOverride;

		public string PathToUnityPlayerActivitySourceFile => m_pathToUnityPlayerActivitySourceFile;

		public string UnityPlayerActivityFileOverride => m_unityPlayerActivityFileOverride;

		public string GradlePluginDependencyImplementation => m_gradlePluginDependencyImplementation;

		public override void Validate()
		{
            // 0x0600013e; original ARM64 0x1aa30ec. The paths are reread for
            // each validation/error, and template validation precedes file validation.
            if (!string.IsNullOrEmpty(m_gradleTemplateFileOverride) && !FileExists(m_gradleTemplateFileOverride, Application.dataPath))
                HLOutput.LogError("Path to HLUnityCore Gradle template file not valid: " + m_gradleTemplateFileOverride);
            if (!string.IsNullOrEmpty(m_gradleFileOverride) && !FileExists(m_gradleFileOverride, Application.dataPath))
                HLOutput.LogError("Path to HLUnityCore Gradle file not valid: " + m_gradleFileOverride);
		}
	}
}
