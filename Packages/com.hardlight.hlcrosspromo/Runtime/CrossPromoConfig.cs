using System;
using System.IO;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000005; bodies translated from the 1.10.1 x86-64 binary.
	public struct CrossPromoConfig
	{
		// 06000022 (1.10.1 RVA 0x1A10070)
		public string RouterPropertyName { get; }

		// 06000023 (1.10.1 RVA 0x1A10080)
		public Version AppVersion { get; }

		// 06000024 (1.10.1 RVA 0x1A10090)
		public string LanguageId { get; }

		// 06000025 (1.10.1 RVA 0x1A100A0)
		public string MetaDataURL { get; }

		// 06000026 (1.10.1 RVA 0x1A100B0)
		public string PlatformName { get; }

		// 06000027 (1.10.1 RVA 0x1A100C0)
		public string IncomingPath { get; }

		// 06000028 (1.10.1 RVA 0x1A100D0)
		public string DestinationPath { get; }

		// 06000029 (1.10.1 RVA 0x1A100E0)
		public string MetaFilePath { get; }

		// 0600002a (1.10.1 RVA 0x1A100F0)
		public int IconImagePoolSize { get; }

		// 0600002b (1.10.1 RVA 0x1A10100)
		public int MainImagePoolSize { get; }

		// 0600002c (1.10.1 RVA 0x1A10110)
		public long InvalidContentLengthThreshold { get; }

		public bool Debug;

		// 0600002d (1.10.1 RVA 0x1A10120)
		public bool CanUseLocalStorage { get; }

		// 0600002e (1.10.1 RVA 0x1A10130). Both folders sit directly under persistentDataPath; the
		// meta file lives in the destination folder.
		public CrossPromoConfig(CrossPromoConfigurationAsset configurationAsset, string appVersion, string country, string language, string persistentDataPath)
			: this(configurationAsset.Server, configurationAsset.GameTitle, configurationAsset.RouterPropertyName, configurationAsset.PlatformName,
				appVersion, country, language,
				Path.Combine(persistentDataPath, configurationAsset.IncomingFolder),
				Path.Combine(persistentDataPath, configurationAsset.DestinationFolder),
				Path.Combine(persistentDataPath, configurationAsset.DestinationFolder, configurationAsset.MetaFilename),
				configurationAsset.Debug, configurationAsset.CanUseLocalStorage, configurationAsset.IconImagePoolSize,
				configurationAsset.MainImagePoolSize, configurationAsset.InvalidContentLengthThreshold)
		{
		}

		// 0600002f (1.10.1 RVA 0x1A10300). The language is kept on the config but is not part of the
		// metadata request URL.
		private CrossPromoConfig(string server, string gameTitle, string routerPropertyName, string platformName, string appVersion,
			string country, string language, string incomingPath, string destinationPath, string metaFilePath, bool debug = false,
			bool canUseLocalStorage = true, int iconImagePoolSize = 8, int mainImagePoolSize = 3, long invalidContentLengthThreshold = 1024)
		{
			RouterPropertyName = routerPropertyName;
			AppVersion = new Version(appVersion);
			LanguageId = language;
			PlatformName = platformName;
			IncomingPath = incomingPath;
			DestinationPath = destinationPath;
			MetaFilePath = metaFilePath;
			MetaDataURL = server + "?title=" + gameTitle + "&version=" + appVersion + "&country=" + country;
			IconImagePoolSize = iconImagePoolSize;
			MainImagePoolSize = mainImagePoolSize;
			InvalidContentLengthThreshold = invalidContentLengthThreshold;
			Debug = debug;
			CanUseLocalStorage = canUseLocalStorage;
		}
	}
}
