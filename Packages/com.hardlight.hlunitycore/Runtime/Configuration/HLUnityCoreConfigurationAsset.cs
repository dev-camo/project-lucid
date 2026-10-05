using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	[CreateAssetMenu(fileName = "HLUnityCoreConfiguration", menuName = "Hardlight/HLUnityCore/Module Configuration", order = 0)]
	public class HLUnityCoreConfigurationAsset : SystemConfigurationAsset
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		public class LowMemoryConfiguration
		{
			[Tooltip("Should it listen to a low memory callback from Unity? If this is disabled, none of the options below will have any effect.")]
			[SerializeField]
			private bool m_enabled = true;

			[Tooltip("The breadcrumb message to be set when low memory is triggered. To disable the breadcrumb, set an empty string.")]
			[SerializeField]
			private string m_breadcrumbMessage = "Low Memory Warning";

			[SerializeField]
			[Tooltip("The minimum interval in seconds which needs to have passed for a new log to be triggered regarding low memory.")]
			private float m_minimumLogIntervalSeconds = 5f;

			[SerializeField]
			[Tooltip("Should the breadcrumb message also be logged as a warning?")]
			private bool m_logBreadcrumbMessageAsWarning = true;

			[SerializeField]
			[Tooltip("Should we attempt to regain some memory by unloading any unused assets?")]
			private bool m_triggerResourcesUnloadAssets = true;

			public bool Enabled => m_enabled;

			public string BreadcrumbMessage => m_breadcrumbMessage;

			public bool LogBreadcrumbMessageAsWarning => m_logBreadcrumbMessageAsWarning;

			public bool TriggerResourcesUnloadAssets => m_triggerResourcesUnloadAssets;

			public float MinimumLogIntervalSeconds => m_minimumLogIntervalSeconds;
		}

		[SerializeField]
		[Tooltip("All settings on how to deal with low memory callbacks")]
		private LowMemoryConfiguration m_lowMemoryConfiguration;

		[Tooltip("Will enable Unity's debug logger and allow errors and exception from logs to be caught and sent to HLCrashReport. Only in distribution builds")]
		[SerializeField]
		private bool m_allowErrorLoggingInDistribution = true;

		[SerializeField]
		[Tooltip("Will cause a crash report handled Exception instead of throwing a null reference exception")]
		private bool m_logInPlaceOfException;

		[Header("Android")]
		[Tooltip("Configuration for Android builds")]
		[SerializeField]
		private AndroidConfigurationAsset m_gradleConfigurationAsset;

		[Header("iOS")]
		[SerializeField]
		[Tooltip("Configuration for iOS builds")]
		private iOSConfigurationAsset m_iOSConfigurationAsset;

		[SerializeField]
		[Tooltip("Configuration for macOS builds")]
		[Header("macOS")]
		private MacOSConfigurationAsset m_macOSConfigurationAsset;

		public bool LogInPlaceOfException => m_logInPlaceOfException;

		public bool AllowErrorLoggingInDistribution => m_allowErrorLoggingInDistribution;

		public LowMemoryConfiguration LowMemoryConfig
        {
            // 0x06000149; CLR-null lazy allocation, original ARM64 0x1aa34cc.
            get
            {
                if (m_lowMemoryConfiguration == null) m_lowMemoryConfiguration = new LowMemoryConfiguration();
                return m_lowMemoryConfiguration;
            }
        }

		public AndroidConfigurationAsset AndroidConfig
        {
            // 0x0600014a; Unity object-null includes destroyed original assets.
            get
            {
                if (m_gradleConfigurationAsset == null) m_gradleConfigurationAsset = CreateInstance<AndroidConfigurationAsset>();
                return m_gradleConfigurationAsset;
            }
        }

		public iOSConfigurationAsset iOSConfig
        {
            // 0x0600014b; Unity object-null includes destroyed original assets.
            get
            {
                if (m_iOSConfigurationAsset == null) m_iOSConfigurationAsset = CreateInstance<iOSConfigurationAsset>();
                return m_iOSConfigurationAsset;
            }
        }

		public MacOSConfigurationAsset macOSConfig
        {
            // 0x0600014c; Unity object-null includes destroyed original assets.
            get
            {
                if (m_macOSConfigurationAsset == null) m_macOSConfigurationAsset = CreateInstance<MacOSConfigurationAsset>();
                return m_macOSConfigurationAsset;
            }
        }

		// 0x0600014d; original ARM64 0x1aa38c4 is RET.
        public override void Validate()
		{
		}
	}
}
