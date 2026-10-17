using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0x0200016f (TypeDefIndex 9118), methods
	// 0x06000a28..0x06000a43; bodies translated from the 1.10.1 x86-64 binary.
	// Shipped asset Assets/MonoBehaviour/SDT-LogConsoleSettings.asset uses
	// this script (export GUID 5dc1c56a0f948a1206a05c9b6d8c9429). The script
	// binding is still pending, so that asset does not bind to this type yet.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "LogConsoleSettings", menuName = "Hardlight/LogConsole")]
	public class HLLogConsoleScriptableObject : ScriptableObject
	{
		[SerializeField]
		[Header("| Console Skin")]
		private GUISkin m_consoleSkin;

		[BitMask(typeof(SupportedLogTypes))]
		[SerializeField]
		[Header("| Log Settings")]
		private SupportedLogTypes m_importantLogTypesToDisplay;

		[SerializeField]
		private bool m_showDetailedViewOnImportantLogType;

		[SerializeField]
		[BitMask(typeof(SupportedLogTypes))]
		private SupportedLogTypes m_defaultLogTypesToDisplay;

		// .ctor 0x1b00a00 stores 10, then 100, 10, 100, 10 as one 16-byte block
		// at 0x34 and 30 at 0x44; the colours are opaque white.
		[Min(1)]
		[SerializeField]
		private int m_maxPageToDisplay = 10;

		[SerializeField]
		private bool m_alwaysOpenOnMostRecentPage;

		[Min(1)]
		[SerializeField]
		private int m_logPageElementCountDefaultLimit = 100;

		[SerializeField]
		[Min(1)]
		private int m_logPageElementMinCount = 10;

		[SerializeField]
		[Min(1)]
		private int m_logPageElementMaxCount = 100;

		[SerializeField]
		[Min(8)]
		private int m_fontSizeMin = 10;

		[SerializeField]
		[Min(8)]
		private int m_fontSizeMax = 30;

		[SerializeField]
		[Header("| Text Colours")]
		private Color m_errorTextColour = Color.white;

		[SerializeField]
		private Color m_warningTextColour = Color.white;

		[SerializeField]
		private Color m_infoTextColour = Color.white;

		[SerializeField]
		private Color m_assertTextColour = Color.white;

		[SerializeField]
		private Color m_exceptionTextColour = Color.white;

		[Header("| Icons")]
		[SerializeField]
		private Texture2D m_logConsoleIcon;

		[SerializeField]
		private Texture2D m_infoIcon;

		[SerializeField]
		private Texture2D m_warningIcon;

		[SerializeField]
		private Texture2D m_errorIcon;

		[SerializeField]
		private Texture2D m_assertIcon;

		[SerializeField]
		private Texture2D m_exceptionIcon;

		[SerializeField]
		private Texture2D m_clearIcon;

		[SerializeField]
		private Texture2D m_settingIcon;

		[SerializeField]
		private Texture2D m_exportIcon;

		[SerializeField]
		private Texture2D m_showConsoleIcon;

		[SerializeField]
		private Texture2D m_hideConsoleIcon;

		public GUISkin ConsoleSkin => m_consoleSkin;

		public SupportedLogTypes ImportantLogTypesToDisplay => m_importantLogTypesToDisplay;

		public bool ShowDetailedViewOnImportantLogType => m_showDetailedViewOnImportantLogType;

		public SupportedLogTypes DefaultLogTypesToDisplay => m_defaultLogTypesToDisplay;

		public int LogPaginationMaxCount => m_maxPageToDisplay;

		public bool AlwaysOpenOnMostRecentPage => m_alwaysOpenOnMostRecentPage;

		public int LogPageElementCountDefaultLimit => m_logPageElementCountDefaultLimit;

		public int LogPageElementMinCount => m_logPageElementMinCount;

		public int LogPageElementMaxCount => m_logPageElementMaxCount;

		public int FontSizeMin => m_fontSizeMin;

		public int FontSizeMax => m_fontSizeMax;

		public Color ErrorTextColor => m_errorTextColour;

		public Color WarningTextColor => m_warningTextColour;

		public Color InfoTextColor => m_infoTextColour;

		public Color AssertTextColor => m_assertTextColour;

		public Color ExceptionTextColor => m_exceptionTextColour;

		public Texture2D LogConsoleIcon => m_logConsoleIcon;

		public Texture2D InfoIcon => m_infoIcon;

		public Texture2D WarningIcon => m_warningIcon;

		public Texture2D ErrorIcon => m_errorIcon;

		public Texture2D AssertIcon => m_assertIcon;

		public Texture2D ExceptionIcon => m_exceptionIcon;

		public Texture2D ClearIcon => m_clearIcon;

		public Texture2D SettingIcon => m_settingIcon;

		public Texture2D ExportIcon => m_exportIcon;

		public Texture2D ShowConsoleIcon => m_showConsoleIcon;

		public Texture2D HideConsoleIcon => m_hideConsoleIcon;
	}
}
