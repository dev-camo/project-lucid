// Original HLCrossPromo.Runtime.dll:Hardlight.CrossPromoConfigurationAsset (0x02000006).
// Evidence keys: original-configuration22/<assembly>/<type>/<method-token>; both shipping architectures.
// Original behavior is preserved here; current compilation and runtime qualification are recorded separately.
using UnityEngine;

namespace Hardlight
{
	[CreateAssetMenu(fileName = "HLCrossPromoConfiguration", menuName = "Hardlight/CrossPromo/Module Configuration", order = 0)]
	public class CrossPromoConfigurationAsset : SystemConfigurationAsset
	{
		// Original nested enum 0x02000007 has underlying Int32 and no declared methods.
		// Native boxed value__ offset 16 and unboxed schema offset 0 remain distinct evidence roles.
		public enum ServerType
		{
			Dev = 0,
			Prod = 1
		}

		private const string ServerDev = "https://gridserver-dev.hardlightdev.com/info";

		private const string ServerProd = "https://gridserver.hardlightgames.com/info";

		[Tooltip("Title of the game.")]
		[Header("Game")]
		[SerializeField]
		private string m_gameTitle;

		[Tooltip("Router property name, ensure that we source the correct metadata from the server.")]
		[SerializeField]
		private string m_routerPropertyName;

		[SerializeField]
		[Tooltip("Platform name, ensure that we source the correct platform from the metadata.")]
		private string m_platformName;

		[Header("Server")]
		[Tooltip("The server url to use.")]
		[SerializeField]
		private ServerType m_server;

		[SerializeField]
		[Tooltip("Size of the response that should be considered valid.")]
		private long m_invalidContentLengthThreshold = 1024L;

		[SerializeField]
		[Tooltip("Name of the folder where files are first downloaded.")]
		[Header("Filesystem")]
		private string m_incomingFolder = "grid_incoming";

		[SerializeField]
		[Tooltip("Name of the folder where files are stored.")]
		private string m_destinationFolder = "grid";

		[Tooltip("Name of the meta file.")]
		[SerializeField]
		private string m_metaFilename = "settings.json";

		[Tooltip("Can this platform use local storage?")]
		[SerializeField]
		private bool m_canUseLocalStorage = true;

		[Header("System")]
		[Tooltip("Default size of the icon images")]
		[SerializeField]
		private int m_iconImagePoolSize = 8;

		[Tooltip("Default size of the main images")]
		[SerializeField]
		private int m_mainImagePoolSize = 3;

		[SerializeField]
		[Tooltip("Enable debugging logs")]
		private bool m_debug;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000030
		// Returns the original serialized field directly.
		public string MetaFilename => m_metaFilename;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000031
		// Returns the original serialized field directly.
		public string DestinationFolder => m_destinationFolder;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000032
		// Returns the original serialized field directly.
		public string IncomingFolder => m_incomingFolder;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000033
		// Zero selects ServerDev; every nonzero value, including undefined enum values, selects ServerProd.
		public string Server => m_server == ServerType.Dev ? ServerDev : ServerProd;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000034
		// Returns the original serialized field directly.
		public string GameTitle => m_gameTitle;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000035
		// Returns the original serialized field directly.
		public string RouterPropertyName => m_routerPropertyName;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000036
		// Returns the original serialized field directly.
		public string PlatformName => m_platformName;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000037
		// Returns the original serialized field directly.
		public bool CanUseLocalStorage => m_canUseLocalStorage;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000038
		// Returns the original serialized field directly.
		public int IconImagePoolSize => m_iconImagePoolSize;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x06000039
		// Returns the original serialized field directly.
		public int MainImagePoolSize => m_mainImagePoolSize;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x0600003a
		// Returns the original serialized field directly.
		public long InvalidContentLengthThreshold => m_invalidContentLengthThreshold;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x0600003b
		// Returns the original serialized field directly.
		public bool Debug => m_debug;

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x0600003c
		// Both shipping bodies return without field access or calls; this empty override is intentional.
		public override void Validate()
		{
		}

		// original-configuration22/HLCrossPromo.Runtime.dll/Hardlight.CrossPromoConfigurationAsset/0x0600003d
		// The initializers above reproduce the original writes before the SystemConfigurationAsset base constructor; no additional own body work follows.
		public CrossPromoConfigurationAsset()
		{
		}
	}
}
