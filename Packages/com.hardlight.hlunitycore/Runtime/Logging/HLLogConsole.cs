using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0x0200016d (TypeDefIndex 9117), methods
	// 0x060009ec..0x06000a22; bodies translated from the 1.10.1 x86-64 binary.
	// Shipped assets use this type through Game.Runtime ProjectConsole
	// (export script GUID d2adfffc4b4189615271a15e09067d08) on
	// Assets/GameObject/LogHandler.prefab, whose m_logConsoleAsset points at
	// SDT-LogConsoleSettings.asset. The script binding is still pending, so the
	// .meta GUID is not the export's (f247f29087f2ed9762c5897ddbde89ed).
	[RequireComponent(typeof(HLLogHandler))]
	public class HLLogConsole : MonoBehaviour
	{
		// TypeDefIndex 9116 (0x0200016e), methods 0x06000a23..0x06000a27.
		private class ConsoleLogMessage
		{
			private static long s_lastIdStamp;

			private HLLogMessage m_logMessage;

			private long m_idStamp;

			private int m_numberOfTimesLogged;

			public HLLogMessage LogMessage => m_logMessage;

			public long IdStamp => m_idStamp;

			public int NumberOfTimesLogged
			{
				get => m_numberOfTimesLogged;
				set => m_numberOfTimesLogged = value;
			}

			// 0x1b00090: the first stamp handed out is 1.
			public ConsoleLogMessage(HLLogMessage message)
			{
				m_logMessage = message;
				m_idStamp = ++s_lastIdStamp;
			}
		}

		private readonly Rect m_titleBarRect = new Rect(0f, 0f, 10000f, 20f);

		private const string WindowTitle = "HL Log Console";
		private const string HideButtonText = "Hide";
		private const string ClearButtonText = "Clear";
		private const string ExportButtonText = "Export log";
		private const string CopyButtonText = "Copy to clipboard";
		private const string BackButtonText = "Back";
		private const string ExitButtonText = "Exit";
		private const string NextButtonText = "Next";
		private const string OpenConsoleButtonText = "Open in console";
		private const string SettingButtonText = "Settings";
		private const string SettingFontSizeLabel = "Font size:";
		private const string SettingGeneralLabel = "| General";
		private const string SettingLogFilterButtonText = "Log to extract from logHandler:";
		private const string SettingApplyFilterButtonText = "Apply filter";
		private const string SettingApplyFilterButtonTooltip = "Only active log types will be processed by the console. Apply will flush all logs for disabled types.";
		private const string SettingLogPanelLabel = "| Log panel";
		private const string SettingLogPanelPreviewLabel = "Preview";
		private const string SettingLogPanelMessagePerPageLabel = "Elements per page:";
		private const string SettingLogPanelWordwrappingLabel = "Word wrapping:";
		private const string SettingLogPanelPreviewSampleText = "Assembly for Assembly Definition File 'Assets/Plugins/HLNetworking/Tests/Editor/HLNetworking.Editor.Tests.asmdef' will not be compiled, because it has no scripts associated with it.";
		private const string HeaderLabelStyle = "headerlabel";
		private const string LogMessageLabelStyle = "logmessage";
		private const string LogMessageDarkerLabelStyle = "logmessagedarker";
		private const string LastLogMessageSelectedLabelStyle = "logmessageselected";
		private const string LeftButtonStyle = "leftbutton";
		private const string RightButtonStyle = "rightbutton";
		private const string StackTraceTextAreaStyle = "stacktracetextarea";
		private const string ImportantMessageWindowStyle = "importantmessagewindow";
		private const string SliderLabelStyle = "sliderlabel";
		private const string ExcludedLogLabelStyle = "excludedloglabel";
		private const int WindowMargin = 20;
		private const int LogFontSize = 12;
		private const int MaxLabelTextLength = 16382;

		// 0x1b007a0 stores 0x3e, every SupportedLogTypes flag. The field is
		// public and mutable, so readers compare against its current value.
		public static SupportedLogTypes AllLogTypes = SupportedLogTypes.Log | SupportedLogTypes.Warning | SupportedLogTypes.Error | SupportedLogTypes.Assert | SupportedLogTypes.Exception;

		// The accessors (0x060009ec, 0x060009ed) precede the event accessors,
		// while the event backing fields sit between AllLogTypes and
		// m_logConsoleAsset, so the properties are declared here.
		private static int s_logTypeCount => Enum.GetValues(typeof(LogType)).Length;

		public bool IsConsoleVisible => m_isConsoleVisible;

		public event Action OnConsoleOpen;

		public event Action OnConsoleClose;

		[Tooltip("Config asset for the log console. If not assigned, it will try to find an existing one.")]
		[SerializeField]
		private HLLogConsoleScriptableObject m_logConsoleAsset;

		private HLLogHandler m_logHolderComponent;
		private Dictionary<LogType, List<ConsoleLogMessage>> m_logMessagesPerType;
		private SortedList<long, ConsoleLogMessage> m_currentSortedFilteredLogMessageList;
		private Queue<ConsoleLogMessage> m_importantMessageQueue = new Queue<ConsoleLogMessage>();
		private List<ConsoleLogMessage> m_dynamicLogExclusionList = new List<ConsoleLogMessage>();
		private Dictionary<string, GUIStyle> m_customStyles;
		private Dictionary<LogType, Texture2D> m_logIconPerType;
		private ConsoleLogMessage m_detailPanelCachedMessage;
		private GUIStyle m_detailPanelCurrentStyle;
		private GUIContent m_consoleWindowTitleContent = new GUIContent();
		private GUIContent m_exportButtonContent = new GUIContent();
		private GUIContent m_hideButtonContent = new GUIContent();
		private GUIContent m_settingButtonContent = new GUIContent();
		private GUIContent m_settingApplyFilterButtonContent = new GUIContent();
		private GUIContent m_settingPreviewContent = new GUIContent();
		private GUIContent m_genericContent = new GUIContent();
		private SupportedLogTypes m_logTypeToDisplay = AllLogTypes;
		private SupportedLogTypes m_logTypeToExtractFromHandler = AllLogTypes;
		private SupportedLogTypes m_temporaryLogTypeToExtractFromHandler = AllLogTypes;
		private Vector2 m_logScrollPosition = Vector2.zero;
		private Vector2 m_settingScrollPosition = Vector2.zero;
		private Vector2 m_detailPanelScrollPosition = Vector2.zero;
		private Vector2 m_settingPreviewScrollPosition = Vector2.zero;
		private Rect m_consoleWindowRect;
		private float m_consoleWidth;
		private float m_consoleHeight;
		private float m_consolePositionX = WindowMargin;
		private float m_consolePositionY = WindowMargin;
		private int m_lastSelectedMessageIndex = -1;
		private int m_logPageElementCountLimit;
		private int m_currentPaginationIndex;
		private int m_currentLogPageIndex;
		private int m_headerLabelFontSizeDifference;
		private bool m_isConsoleVisible;
		private bool m_isSettingPanelOpen;
		private bool m_isLastMessageIsImportant;
		private bool m_hasReceivedImportantMessage;
		private bool m_isDetailsPanelOpen;
		private bool m_initialized;

		// 0x1af8be0. A failed check returns before the event fields are cleared.
		private void Awake()
		{
			m_logHolderComponent = GetComponent<HLLogHandler>();
			if (!ConsoleConfigAndSkinCheck())
				return;
			if (m_logConsoleAsset)
			{
				m_logPageElementCountLimit = m_logConsoleAsset.LogPageElementCountDefaultLimit;
				m_logTypeToDisplay = m_logConsoleAsset.DefaultLogTypesToDisplay;
				GUIStyleInitialize();
			}
			OnConsoleOpen = null;
			OnConsoleClose = null;
		}

		// 0x1af8da0.
		private void GUIStyleInitialize()
		{
			m_logIconPerType = new Dictionary<LogType, Texture2D>
			{
				{ LogType.Log, m_logConsoleAsset.InfoIcon },
				{ LogType.Warning, m_logConsoleAsset.WarningIcon },
				{ LogType.Error, m_logConsoleAsset.ErrorIcon },
				{ LogType.Assert, m_logConsoleAsset.AssertIcon },
				{ LogType.Exception, m_logConsoleAsset.ExceptionIcon },
			};
			m_customStyles = new Dictionary<string, GUIStyle>(m_logConsoleAsset.ConsoleSkin.customStyles.Length);
			int customStyleCount = m_logConsoleAsset.ConsoleSkin.customStyles.Length;
			for (int i = 0; i < customStyleCount; i++)
				m_customStyles.Add(m_logConsoleAsset.ConsoleSkin.customStyles[i].name, m_logConsoleAsset.ConsoleSkin.customStyles[i]);
			m_headerLabelFontSizeDifference = m_customStyles[HeaderLabelStyle].fontSize - m_logConsoleAsset.ConsoleSkin.label.fontSize;
			m_exportButtonContent.text = ExportButtonText;
			m_exportButtonContent.image = m_logConsoleAsset.ExportIcon;
			m_consoleWindowTitleContent.text = WindowTitle;
			m_consoleWindowTitleContent.image = m_logConsoleAsset.LogConsoleIcon;
			m_settingButtonContent.text = SettingButtonText;
			m_settingButtonContent.image = m_logConsoleAsset.SettingIcon;
			m_hideButtonContent.text = HideButtonText;
			m_hideButtonContent.image = m_logConsoleAsset.HideConsoleIcon;
			m_settingApplyFilterButtonContent.text = SettingApplyFilterButtonText;
			m_settingApplyFilterButtonContent.tooltip = SettingApplyFilterButtonTooltip;
			m_settingApplyFilterButtonContent.image = m_logConsoleAsset.WarningIcon;
			m_settingPreviewContent.text = SettingLogPanelPreviewSampleText;
			m_settingPreviewContent.image = m_logConsoleAsset.InfoIcon;
		}

		// 0x1af8cd0. The failure path evaluates m_logConsoleAsset == null a
		// second time (0x1af8d79) and drops the result. It probably fed an empty
		// or stripped logging helper that was inlined away (a [Conditional] call
		// would have removed the comparison too); that call is not reconstructed.
		private bool ConsoleConfigAndSkinCheck()
		{
			if (m_logConsoleAsset == null || m_logConsoleAsset.ConsoleSkin == null)
			{
				bool isConfigMissing = m_logConsoleAsset == null;
				enabled = false;
				return false;
			}
			return true;
		}

		// 0x1af9340. The lists are sized from the handler's estimate once; the
		// handler subscription is added on every enable.
		private void OnEnable()
		{
			if (!ConsoleConfigAndSkinCheck())
				return;
			if (!m_initialized)
			{
				int estimatedLogMessagesCount = m_logHolderComponent.GetEstimatedLogMessagesCount();
				m_logMessagesPerType = new Dictionary<LogType, List<ConsoleLogMessage>>
				{
					{ LogType.Log, new List<ConsoleLogMessage>(estimatedLogMessagesCount) },
					{ LogType.Warning, new List<ConsoleLogMessage>(estimatedLogMessagesCount) },
					{ LogType.Error, new List<ConsoleLogMessage>(estimatedLogMessagesCount) },
					{ LogType.Assert, new List<ConsoleLogMessage>(estimatedLogMessagesCount) },
					{ LogType.Exception, new List<ConsoleLogMessage>(estimatedLogMessagesCount) },
				};
				m_currentSortedFilteredLogMessageList = new SortedList<long, ConsoleLogMessage>(s_logTypeCount * estimatedLogMessagesCount);
				m_initialized = true;
			}
			m_logHolderComponent.OnLogMessageReceived += LogMessageReceived;
		}

		// 0x1af9790. The Rect returned by GUILayout.Window is discarded
		// (0x1af9936), so the stored window rect only changes through
		// ConsoleWindowRectCompute.
		private void OnGUI()
		{
			GUI.skin = m_logConsoleAsset.ConsoleSkin;
			ImportantMessageCheck();
			if (gameObject.activeInHierarchy && m_isConsoleVisible)
			{
				ConsoleWindowRectCompute();
				GUILayout.Window(0, m_consoleWindowRect, ConsoleWindowDraw, m_consoleWindowTitleContent, m_logConsoleAsset.ConsoleSkin.window);
			}
			else
			{
				LogMessageDetailOverlayDraw();
			}
			GUI.enabled = true;
		}

		private void ConsoleWindowDraw(int windowId)
		{
			GUILayout.BeginVertical();
			ConsoleHeaderDraw();
			if (m_isSettingPanelOpen)
				SettingPanelDraw();
			else if (m_isDetailsPanelOpen)
				LogMessageDetailWindowDraw();
			else
				LogPanelDraw();
			GUILayout.EndVertical();
			GUI.DragWindow(m_titleBarRect);
		}

		private void ConsoleHeaderDraw()
		{
			GUILayout.BeginVertical(m_logConsoleAsset.ConsoleSkin.box);
			GUILayout.BeginHorizontal();
			m_isSettingPanelOpen = GUILayout.Toggle(m_isSettingPanelOpen, m_settingButtonContent, GUI.skin.button);
			if (GUILayout.Button(m_hideButtonContent))
				ConsoleHide();
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}

		private void LogMessageDetailOverlayDraw()
		{
			if (!m_isDetailsPanelOpen)
				return;
			ConsoleWindowRectCompute();
			GUILayout.BeginArea(m_consoleWindowRect);
			LogMessageDetailWindowDraw(true);
			GUILayout.EndArea();
		}

		// 0x1afa6b0. Only the stack trace is truncated; the message label is not.
		private void LogMessageDetailWindowDraw(bool isOverlayMode = false)
		{
			GUILayout.BeginVertical(m_detailPanelCurrentStyle);
			m_detailPanelScrollPosition = GUILayout.BeginScrollView(m_detailPanelScrollPosition);
			GUILayout.Box(new GUIContent(m_detailPanelCachedMessage.LogMessage.Type.ToString(), m_logIconPerType[m_detailPanelCachedMessage.LogMessage.Type]));
			GUILayout.Label(m_detailPanelCachedMessage.LogMessage.Message);
			GUILayout.Label(LabelTextTruncate(m_detailPanelCachedMessage.LogMessage.StackTrace), m_customStyles[StackTraceTextAreaStyle]);
			GUILayout.EndScrollView();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(m_exportButtonContent))
				m_logHolderComponent.LogExport();
			if (GUILayout.Button(CopyButtonText))
			{
				TextEditor textEditor = new TextEditor();
				textEditor.text = OpString.large + m_detailPanelCachedMessage.LogMessage.Message + "\n" + m_detailPanelCachedMessage.LogMessage.StackTrace;
				textEditor.SelectAll();
				textEditor.Copy();
			}
			GUILayout.EndHorizontal();

			m_genericContent.text = DetailModeActionButtonGetText(isOverlayMode);
			m_genericContent.image = null;
			if (GUILayout.Button(m_genericContent))
			{
				m_isLastMessageIsImportant = false;
				m_detailPanelCachedMessage = null;
				HideDetailsPanel();
			}
			if (isOverlayMode)
			{
				m_genericContent.text = OpenConsoleButtonText;
				m_genericContent.image = m_logConsoleAsset.ShowConsoleIcon;
				if (GUILayout.Button(m_genericContent))
					ConsoleShow();
			}
			// Only a message not yet collapsed can start a dynamic exclusion.
			if (m_detailPanelCachedMessage != null && m_detailPanelCachedMessage.NumberOfTimesLogged == 0)
			{
				m_genericContent.text = "Ignore All Logs Of This Type";
				m_genericContent.image = null;
				if (GUILayout.Button(m_genericContent))
				{
					m_dynamicLogExclusionList.Add(m_detailPanelCachedMessage);
					CollapseExcludedLogMessages(m_detailPanelCachedMessage);
					HideDetailsPanel();
					SortedLogListUpdate();
					m_currentLogPageIndex = 0;
				}
			}
			GUILayout.EndVertical();
		}

		// 0x1afbc40. Any queued important message wins over the overlay flag.
		private string DetailModeActionButtonGetText(bool isOverlayMode)
		{
			if (m_importantMessageQueue.Count > 0)
				return string.Format("<b>{0} ({1} important log remaining)</b>", NextButtonText, m_importantMessageQueue.Count);
			return isOverlayMode ? ExitButtonText : BackButtonText;
		}

		// 0x1afb320. The scroll view is ended after the vertical group it was
		// opened inside, and the pagination is drawn outside both.
		private void LogPanelDraw()
		{
			GUILayout.BeginVertical(GUI.skin.box);
			GUILayout.BeginHorizontal();
			LogTypeFiltersAndClearDraw();
			m_genericContent.text = ClearButtonText;
			m_genericContent.image = m_logConsoleAsset.ClearIcon;
			if (GUILayout.Button(m_genericContent))
				ClearAllLogTypes();
			GUILayout.EndHorizontal();
			if (HasFlagValue(m_logTypeToDisplay, AllLogTypes))
			{
				m_logScrollPosition = GUILayout.BeginScrollView(m_logScrollPosition);
				LogPanelPageDraw(m_currentSortedFilteredLogMessageList.Values);
				GUILayout.EndVertical();
				GUILayout.EndScrollView();
				LogPanelPaginationDraw();
			}
			else
			{
				GUILayout.EndVertical();
			}
		}

		// 0x1afc1e0. Only types the handler extracts get a filter toggle; a type
		// without one drops out of the display mask when anything changes.
		private void LogTypeFiltersAndClearDraw()
		{
			bool displayLog = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Log) && LogTypeFilterElementDraw(LogType.Log, SupportedLogTypes.Log, m_logConsoleAsset.InfoIcon);
			bool displayWarning = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Warning) && LogTypeFilterElementDraw(LogType.Warning, SupportedLogTypes.Warning, m_logConsoleAsset.WarningIcon);
			bool displayError = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Error) && LogTypeFilterElementDraw(LogType.Error, SupportedLogTypes.Error, m_logConsoleAsset.ErrorIcon);
			bool displayAssert = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Assert) && LogTypeFilterElementDraw(LogType.Assert, SupportedLogTypes.Assert, m_logConsoleAsset.AssertIcon);
			bool displayException = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Exception) && LogTypeFilterElementDraw(LogType.Exception, SupportedLogTypes.Exception, m_logConsoleAsset.ExceptionIcon);
			if (GUI.changed)
			{
				m_logTypeToDisplay = (displayLog ? SupportedLogTypes.Log : 0)
					| (displayWarning ? SupportedLogTypes.Warning : 0)
					| (displayError ? SupportedLogTypes.Error : 0)
					| (displayAssert ? SupportedLogTypes.Assert : 0)
					| (displayException ? SupportedLogTypes.Exception : 0);
				SortedLogListUpdate();
			}
		}

		private bool LogTypeFilterElementDraw(LogType logType, SupportedLogTypes supportedLogTypes, Texture2D icon)
		{
			m_genericContent.image = icon;
			m_genericContent.text = m_logMessagesPerType[logType].Count.ToString();
			GUILayout.BeginHorizontal();
			bool isDisplayed = GUILayout.Toggle(HasFlagValue(m_logTypeToDisplay, supportedLogTypes), m_genericContent, m_customStyles[LeftButtonStyle]);
			if (GUILayout.Button(m_logConsoleAsset.ClearIcon, m_customStyles[RightButtonStyle]))
				ClearLogTypes(supportedLogTypes);
			GUILayout.EndHorizontal();
			return isDisplayed;
		}

		// 0x1afc690. A list that fits on one page resets the page index.
		private void LogPanelPageDraw(IList<ConsoleLogMessage> listToDraw)
		{
			int count = listToDraw.Count;
			if (count <= m_logPageElementCountLimit)
				m_currentLogPageIndex = 0;
			int firstIndex = m_currentLogPageIndex * m_logPageElementCountLimit;
			int lastIndex = Mathf.Min(firstIndex + m_logPageElementCountLimit, count);
			for (int i = firstIndex; i < lastIndex; i++)
				LogPanelPageMessageDraw(i, listToDraw[i]);
		}

		// 0x1afd6f0. Rows alternate by absolute index; the excluded-count style
		// is fetched before the label text is formatted.
		private void LogPanelPageMessageDraw(int index, ConsoleLogMessage consoleMessage)
		{
			m_genericContent.text = LabelTextTruncate(consoleMessage.LogMessage.Message);
			m_genericContent.image = m_logIconPerType[consoleMessage.LogMessage.Type];
			GUIStyle messageStyle = m_customStyles[m_lastSelectedMessageIndex == index ? LastLogMessageSelectedLabelStyle : index % 2 == 0 ? LogMessageLabelStyle : LogMessageDarkerLabelStyle];
			switch (consoleMessage.LogMessage.Type)
			{
				case LogType.Error:
					messageStyle.normal.textColor = m_logConsoleAsset.ErrorTextColor;
					break;
				case LogType.Assert:
					messageStyle.normal.textColor = m_logConsoleAsset.AssertTextColor;
					break;
				case LogType.Warning:
					messageStyle.normal.textColor = m_logConsoleAsset.WarningTextColor;
					break;
				case LogType.Log:
					messageStyle.normal.textColor = m_logConsoleAsset.InfoTextColor;
					break;
				case LogType.Exception:
					messageStyle.normal.textColor = m_logConsoleAsset.ExceptionTextColor;
					break;
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(m_genericContent, messageStyle))
			{
				m_lastSelectedMessageIndex = index;
				m_detailPanelCachedMessage = consoleMessage;
				ShowDetailsPanel();
			}
			if (consoleMessage.NumberOfTimesLogged > 0)
			{
				GUIStyle excludedLogStyle = m_customStyles[ExcludedLogLabelStyle];
				GUILayout.Label(string.Format("Ignored\n{0}", consoleMessage.NumberOfTimesLogged), excludedLogStyle);
			}
			GUILayout.EndHorizontal();
		}

		// 0x1afc820. The bounds and the next-page test use the pagination index
		// read before the "|<" and "<" buttons change it; the page buttons and
		// the handlers read it again.
		private void LogPanelPaginationDraw()
		{
			int totalPageCount = GetTotalPageCount();
			if (totalPageCount < 2)
				return;
			int lastPageIndex = totalPageCount - 1;
			int lastPaginationIndex = lastPageIndex / m_logConsoleAsset.LogPaginationMaxCount;
			if (m_currentPaginationIndex > lastPaginationIndex)
			{
				m_currentPaginationIndex = lastPaginationIndex;
				m_currentLogPageIndex = m_currentPaginationIndex * m_logConsoleAsset.LogPaginationMaxCount;
			}
			m_currentLogPageIndex = Mathf.Min(m_currentLogPageIndex, lastPageIndex);

			GUILayout.BeginHorizontal(GUI.skin.box);
			int maxPageToDisplay = m_logConsoleAsset.LogPaginationMaxCount;
			int pageButtonCount = Mathf.Min(totalPageCount - maxPageToDisplay * m_currentPaginationIndex, maxPageToDisplay);
			bool hasPreviousPages = m_currentPaginationIndex != 0;
			bool hasNextPages = (m_currentPaginationIndex + 1) * maxPageToDisplay < totalPageCount;
			if (hasPreviousPages)
			{
				if (GUILayout.Button("|<"))
				{
					m_currentPaginationIndex = 0;
					m_currentLogPageIndex = 0;
				}
				else if (GUILayout.Button("<"))
				{
					m_currentPaginationIndex--;
					m_currentLogPageIndex = (m_currentPaginationIndex + 1) * m_logConsoleAsset.LogPaginationMaxCount - 1;
				}
			}
			string[] pageLabels = new string[pageButtonCount];
			for (int i = 0; i < pageButtonCount; i++)
				pageLabels[i] = (m_logConsoleAsset.LogPaginationMaxCount * m_currentPaginationIndex + i + 1).ToString();
			int selectedPageButton = m_currentLogPageIndex % m_logConsoleAsset.LogPaginationMaxCount;
			int newSelectedPageButton = GUILayout.Toolbar(selectedPageButton, pageLabels);
			if (GUI.changed && newSelectedPageButton != selectedPageButton)
				m_currentLogPageIndex = m_logConsoleAsset.LogPaginationMaxCount * m_currentPaginationIndex + newSelectedPageButton;
			if (hasNextPages)
			{
				if (GUILayout.Button(">"))
				{
					m_currentPaginationIndex++;
					m_currentLogPageIndex = m_currentPaginationIndex * m_logConsoleAsset.LogPaginationMaxCount;
				}
				else if (GUILayout.Button(">|"))
				{
					m_currentPaginationIndex = lastPaginationIndex;
					m_currentLogPageIndex = lastPageIndex;
				}
			}
			GUILayout.EndHorizontal();
		}

		// 0x1afdce0: a partial page counts as a page.
		private int GetTotalPageCount()
		{
			return m_currentSortedFilteredLogMessageList.Count / m_logPageElementCountLimit
				+ (m_currentSortedFilteredLogMessageList.Count % m_logPageElementCountLimit != 0 ? 1 : 0);
		}

		// 0x1afa440. The general and log sections share one width option.
		private void SettingPanelDraw()
		{
			GUILayoutOption formatOption = GUILayout.Width(m_consoleWindowRect.width / 2 - WindowMargin - GUI.skin.verticalScrollbar.fixedWidth);
			GUILayout.BeginVertical(m_detailPanelCurrentStyle);
			m_settingScrollPosition = GUILayout.BeginScrollView(m_settingScrollPosition);
			SettingPanelGeneralDraw(formatOption);
			SettingPanelLogDraw(formatOption);
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}

		// 0x1afdd30. The font size and extraction choices only apply when the
		// GUI changed; the extraction mask is staged until "Apply filter".
		private void SettingPanelGeneralDraw(GUILayoutOption formatOption)
		{
			GUILayout.Label(SettingGeneralLabel, m_customStyles[HeaderLabelStyle]);
			int fontSize = SettingCustomSliderDraw(SettingFontSizeLabel, m_logConsoleAsset.ConsoleSkin.label.fontSize, m_logConsoleAsset.FontSizeMin, m_logConsoleAsset.FontSizeMax, formatOption);
			GUILayoutOption extractionOption = GUILayout.Width(m_consoleWindowRect.width - WindowMargin * 2 - m_logConsoleAsset.ConsoleSkin.verticalScrollbar.fixedWidth);
			GUILayout.BeginVertical();
			GUILayout.Label(SettingLogFilterButtonText);
			GUILayout.BeginHorizontal(extractionOption);
			bool extractLog = LogTypeElementToExtractDraw(LogType.Log, SupportedLogTypes.Log, m_logConsoleAsset.InfoIcon);
			bool extractWarning = LogTypeElementToExtractDraw(LogType.Warning, SupportedLogTypes.Warning, m_logConsoleAsset.WarningIcon);
			bool extractError = LogTypeElementToExtractDraw(LogType.Error, SupportedLogTypes.Error, m_logConsoleAsset.ErrorIcon);
			bool extractAssert = LogTypeElementToExtractDraw(LogType.Assert, SupportedLogTypes.Assert, m_logConsoleAsset.AssertIcon);
			bool extractException = LogTypeElementToExtractDraw(LogType.Exception, SupportedLogTypes.Exception, m_logConsoleAsset.ExceptionIcon);
			if (m_temporaryLogTypeToExtractFromHandler != m_logTypeToExtractFromHandler && GUILayout.Button(m_settingApplyFilterButtonContent))
			{
				m_logTypeToExtractFromHandler = m_temporaryLogTypeToExtractFromHandler;
				LogMessagePerTypeTrim();
			}
			m_genericContent.tooltip = null;
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			if (GUILayout.Button(m_exportButtonContent, formatOption))
				m_logHolderComponent.LogExport();
			if (GUI.changed)
			{
				GeneralFontSizeUpdate(fontSize);
				m_temporaryLogTypeToExtractFromHandler = (extractLog ? SupportedLogTypes.Log : 0)
					| (extractWarning ? SupportedLogTypes.Warning : 0)
					| (extractError ? SupportedLogTypes.Error : 0)
					| (extractAssert ? SupportedLogTypes.Assert : 0)
					| (extractException ? SupportedLogTypes.Exception : 0);
			}
		}

		// 0x1afea80.
		private int SettingCustomSliderDraw(string label, int sliderValue, int minSliderValue, int maxSliderValue, params GUILayoutOption[] layoutOption)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label, layoutOption);
			GUILayout.BeginHorizontal(layoutOption);
			GUILayout.Label(sliderValue.ToString(), m_customStyles[SliderLabelStyle]);
			sliderValue = (int)GUILayout.HorizontalSlider(sliderValue, minSliderValue, maxSliderValue);
			GUILayout.EndHorizontal();
			GUILayout.EndHorizontal();
			return sliderValue;
		}

		// 0x1aff180. Every custom style except the three log message styles
		// takes the general size, then the header keeps its initial offset.
		// The key count read first (0x1aff290) is unused in the shipped body.
		private void GeneralFontSizeUpdate(int fontSize)
		{
			int customStyleCount = m_customStyles.Keys.Count;
			foreach (KeyValuePair<string, GUIStyle> customStyle in m_customStyles)
			{
				if (customStyle.Key == LogMessageLabelStyle || customStyle.Key == LastLogMessageSelectedLabelStyle || customStyle.Key == LogMessageDarkerLabelStyle)
					continue;
				customStyle.Value.fontSize = fontSize;
			}
			m_logConsoleAsset.ConsoleSkin.button.fontSize = m_logConsoleAsset.ConsoleSkin.label.fontSize = fontSize;
			m_customStyles[HeaderLabelStyle].fontSize += m_headerLabelFontSizeDifference;
		}

		private bool LogTypeElementToExtractDraw(LogType logType, SupportedLogTypes supportedLogTypes, Texture2D icon)
		{
			m_genericContent.text = logType.ToString();
			m_genericContent.image = icon;
			return GUILayout.Toggle(HasFlagValue(m_temporaryLogTypeToExtractFromHandler, supportedLogTypes), m_genericContent, m_customStyles[LeftButtonStyle]);
		}

		// 0x1afe440. The page size, font size and word wrap only apply when the
		// GUI changed.
		private void SettingPanelLogDraw(GUILayoutOption formatOption)
		{
			GUILayout.Label(SettingLogPanelLabel, m_customStyles[HeaderLabelStyle]);
			int logPageElementCount = SettingCustomSliderDraw(SettingLogPanelMessagePerPageLabel, m_logPageElementCountLimit, m_logConsoleAsset.LogPageElementMinCount, m_logConsoleAsset.LogPageElementMaxCount, formatOption);
			int fontSize = SettingCustomSliderDraw(SettingFontSizeLabel, m_customStyles[LogMessageLabelStyle].fontSize, m_logConsoleAsset.FontSizeMin, m_logConsoleAsset.FontSizeMax, formatOption);
			GUILayout.BeginHorizontal();
			GUILayout.Label(SettingLogPanelWordwrappingLabel, formatOption);
			bool wordWrap = GUILayout.Toggle(m_customStyles[LogMessageLabelStyle].wordWrap, "", formatOption);
			GUILayout.EndHorizontal();
			SettingPanelLogPreviewDraw();
			if (GUI.changed)
			{
				m_customStyles[LastLogMessageSelectedLabelStyle].fontSize = fontSize;
				m_customStyles[LogMessageLabelStyle].fontSize = fontSize;
				m_customStyles[LogMessageDarkerLabelStyle].fontSize = fontSize;
				m_customStyles[LastLogMessageSelectedLabelStyle].wordWrap = wordWrap;
				m_customStyles[LogMessageLabelStyle].wordWrap = wordWrap;
				m_customStyles[LogMessageDarkerLabelStyle].wordWrap = wordWrap;
				m_logPageElementCountLimit = logPageElementCount;
			}
		}

		private void SettingPanelLogPreviewDraw()
		{
			GUILayout.BeginVertical(GUI.skin.box);
			GUILayout.Label(SettingLogPanelPreviewLabel);
			GUILayout.Label(m_settingPreviewContent, m_customStyles[LogMessageLabelStyle]);
			GUILayout.EndVertical();
		}

		private void OnDisable()
		{
			m_logHolderComponent.OnLogMessageReceived -= LogMessageReceived;
		}

		private void OnDestroy()
		{
			m_currentSortedFilteredLogMessageList.Clear();
			m_logMessagesPerType.Clear();
		}

		// 0x1affa10. A message whose text matches a dynamic exclusion only bumps
		// that entry's count. Otherwise the important, extracted and displayed
		// masks are tested in turn (jump tables 0x1b00068 and 0x1b0007c); a
		// LogType outside the five mapped values passes none of them. An
		// important message that is not extracted is still queued.
		private void LogMessageReceived(HLLogMessage logMessage)
		{
			for (int i = 0; i < m_dynamicLogExclusionList.Count; i++)
			{
				if (logMessage.Message == m_dynamicLogExclusionList[i].LogMessage.Message)
				{
					m_dynamicLogExclusionList[i].NumberOfTimesLogged++;
					return;
				}
			}

			bool isImportant = false;
			switch (logMessage.Type)
			{
				case LogType.Error: isImportant = HasFlagValue(m_logConsoleAsset.ImportantLogTypesToDisplay, SupportedLogTypes.Error); break;
				case LogType.Assert: isImportant = HasFlagValue(m_logConsoleAsset.ImportantLogTypesToDisplay, SupportedLogTypes.Assert); break;
				case LogType.Warning: isImportant = HasFlagValue(m_logConsoleAsset.ImportantLogTypesToDisplay, SupportedLogTypes.Warning); break;
				case LogType.Log: isImportant = HasFlagValue(m_logConsoleAsset.ImportantLogTypesToDisplay, SupportedLogTypes.Log); break;
				case LogType.Exception: isImportant = HasFlagValue(m_logConsoleAsset.ImportantLogTypesToDisplay, SupportedLogTypes.Exception); break;
			}
			ConsoleLogMessage consoleMessage = null;
			if (isImportant)
			{
				consoleMessage = new ConsoleLogMessage(logMessage);
				if (m_logConsoleAsset.ShowDetailedViewOnImportantLogType)
					m_importantMessageQueue.Enqueue(consoleMessage);
				else
					m_hasReceivedImportantMessage = true;
			}

			bool isExtracted = false;
			switch (logMessage.Type)
			{
				case LogType.Error: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Error); break;
				case LogType.Assert: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Assert); break;
				case LogType.Warning: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Warning); break;
				case LogType.Log: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Log); break;
				case LogType.Exception: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Exception); break;
			}
			if (!isExtracted)
				return;
			if (consoleMessage == null)
				consoleMessage = new ConsoleLogMessage(logMessage);
			m_logMessagesPerType[logMessage.Type].Add(consoleMessage);

			bool isDisplayed = false;
			switch (logMessage.Type)
			{
				case LogType.Error: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Error); break;
				case LogType.Assert: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Assert); break;
				case LogType.Warning: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Warning); break;
				case LogType.Log: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Log); break;
				case LogType.Exception: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Exception); break;
			}
			if (isDisplayed)
				m_currentSortedFilteredLogMessageList.Add(consoleMessage.IdStamp, consoleMessage);
		}

		// 0x1af9c00. The window keeps a WindowMargin inset on both axes and
		// never starts above or left of the safe area or WindowMargin.
		private void ConsoleWindowRectCompute()
		{
			m_consoleWidth = Screen.safeArea.width;
			m_consoleHeight = Screen.safeArea.height;
			float consoleWindowWidth = m_consoleWidth - WindowMargin * 2;
			float consoleWindowHeight = m_consoleHeight - WindowMargin * 2;
			GUIStyle windowStyle = m_logConsoleAsset.ConsoleSkin.window;
			windowStyle.fixedHeight = consoleWindowHeight;
			windowStyle.fixedWidth = consoleWindowWidth;
			m_consolePositionX = Mathf.Max(m_consolePositionX, Screen.safeArea.x, WindowMargin);
			m_consolePositionY = Mathf.Max(m_consolePositionY, Screen.safeArea.y, WindowMargin);
			m_consoleWindowRect = new Rect(m_consolePositionX, m_consolePositionY, consoleWindowWidth, consoleWindowHeight);
		}

		// 0x1afbef0. Rebuilds the id-ordered list from the displayed types
		// (jump table 0x1afc1c8) and clears the selection.
		private void SortedLogListUpdate()
		{
			m_lastSelectedMessageIndex = -1;
			m_currentSortedFilteredLogMessageList.Clear();
			for (int i = 0; i < s_logTypeCount; i++)
			{
				bool isDisplayed = false;
				switch ((LogType)i)
				{
					case LogType.Error: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Error); break;
					case LogType.Assert: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Assert); break;
					case LogType.Warning: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Warning); break;
					case LogType.Log: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Log); break;
					case LogType.Exception: isDisplayed = HasFlagValue(m_logTypeToDisplay, SupportedLogTypes.Exception); break;
				}
				if (!isDisplayed)
					continue;
				List<ConsoleLogMessage> logMessages = m_logMessagesPerType[(LogType)i];
				int count = logMessages.Count;
				for (int j = 0; j < count; j++)
					m_currentSortedFilteredLogMessageList.Add(logMessages[j].IdStamp, logMessages[j]);
			}
		}

		// 0x1afbde0. The message counts itself once, then once per other copy
		// of its text removed from its type's list.
		private void CollapseExcludedLogMessages(ConsoleLogMessage message)
		{
			message.NumberOfTimesLogged++;
			List<ConsoleLogMessage> logMessages = m_logMessagesPerType[message.LogMessage.Type];
			for (int i = 0; i < logMessages.Count; i++)
			{
				ConsoleLogMessage loggedMessage = logMessages[i];
				if (loggedMessage.LogMessage.Message == message.LogMessage.Message && loggedMessage.IdStamp != message.IdStamp)
				{
					logMessages.RemoveAt(i);
					message.NumberOfTimesLogged++;
					i--;
				}
			}
		}

		// 0x1b00110. Both exits share one block (0x1b001ad) that returns
		// i < Count from the Count read on that iteration; the early-return form
		// is the equivalent source (a trailing `return i < list.Count` would
		// re-read the list after the opaque op_Equality call).
		private bool IsExcludedLogMessage(HLLogMessage message)
		{
			for (int i = 0; i < m_dynamicLogExclusionList.Count; i++)
			{
				if (message.Message == m_dynamicLogExclusionList[i].LogMessage.Message)
					return true;
			}
			return false;
		}

		// 0x1b001d0: excluded messages are dequeued and dropped.
		private ConsoleLogMessage GetNextImportantLogMessage()
		{
			while (m_importantMessageQueue.Count > 0)
			{
				ConsoleLogMessage message = m_importantMessageQueue.Dequeue();
				if (!IsExcludedLogMessage(message.LogMessage))
					return message;
			}
			return null;
		}

		// 0x1afef50. Clears the types the handler no longer extracts (jump
		// table 0x1aff160); a LogType outside the five mapped values is kept.
		private void LogMessagePerTypeTrim()
		{
			bool hasTrimmed = false;
			for (int i = 0; i < s_logTypeCount; i++)
			{
				bool isExtracted = true;
				switch ((LogType)i)
				{
					case LogType.Error: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Error); break;
					case LogType.Assert: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Assert); break;
					case LogType.Warning: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Warning); break;
					case LogType.Log: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Log); break;
					case LogType.Exception: isExtracted = HasFlagValue(m_logTypeToExtractFromHandler, SupportedLogTypes.Exception); break;
				}
				if (isExtracted)
					continue;
				m_logMessagesPerType[(LogType)i].Clear();
				hasTrimmed = true;
			}
			if (hasTrimmed)
				SortedLogListUpdate();
		}

		public void ClearAllLogTypes()
		{
			foreach (LogType logType in m_logMessagesPerType.Keys)
				m_logMessagesPerType[logType].Clear();
			SortedLogListUpdate();
		}

		// 0x1afd400 (jump table 0x1afd6d4).
		public void ClearLogTypes(SupportedLogTypes typesToClear)
		{
			foreach (LogType logType in m_logMessagesPerType.Keys)
			{
				bool shouldClear = false;
				switch (logType)
				{
					case LogType.Error: shouldClear = HasFlagValue(typesToClear, SupportedLogTypes.Error); break;
					case LogType.Assert: shouldClear = HasFlagValue(typesToClear, SupportedLogTypes.Assert); break;
					case LogType.Warning: shouldClear = HasFlagValue(typesToClear, SupportedLogTypes.Warning); break;
					case LogType.Log: shouldClear = HasFlagValue(typesToClear, SupportedLogTypes.Log); break;
					case LogType.Exception: shouldClear = HasFlagValue(typesToClear, SupportedLogTypes.Exception); break;
				}
				if (shouldClear)
					m_logMessagesPerType[logType].Clear();
			}
			SortedLogListUpdate();
		}

		// 0x1af9a00. Pending important messages (or, without the detailed view,
		// any received one) open the console unless a detail panel is already
		// showing. Otherwise the panel style falls back to the skin box while
		// no important message is on screen.
		private void ImportantMessageCheck()
		{
			if (m_importantMessageQueue.Count != 0 || (!m_logConsoleAsset.ShowDetailedViewOnImportantLogType && m_hasReceivedImportantMessage))
			{
				if (m_isDetailsPanelOpen)
					return;
				if (m_logConsoleAsset.ShowDetailedViewOnImportantLogType)
				{
					m_detailPanelCachedMessage = GetNextImportantLogMessage();
					if (m_detailPanelCachedMessage != null)
					{
						m_isLastMessageIsImportant = true;
						m_detailPanelCurrentStyle = m_customStyles[ImportantMessageWindowStyle];
						ShowDetailsPanel();
					}
					else
					{
						HideDetailsPanel();
					}
				}
				else
				{
					m_isLastMessageIsImportant = false;
					m_hasReceivedImportantMessage = false;
				}
				ConsoleShow();
			}
			else if (!m_isLastMessageIsImportant)
			{
				m_detailPanelCurrentStyle = GUI.skin.box;
			}
		}

		// 0x1afb800.
		private static string LabelTextTruncate(string message)
		{
			if (!string.IsNullOrEmpty(message) && message.Length > MaxLabelTextLength)
				return message.Substring(0, MaxLabelTextLength);
			return message;
		}

		private static bool HasFlagValue(SupportedLogTypes value, SupportedLogTypes flagValue)
		{
			return (value & flagValue) != 0;
		}

		private void ShowDetailsPanel()
		{
			m_isDetailsPanelOpen = true;
		}

		private void HideDetailsPanel()
		{
			m_isDetailsPanelOpen = false;
		}

		private void ConsoleToggle()
		{
			if (m_isConsoleVisible)
				ConsoleHide();
			else
				ConsoleShow();
		}

		// 0x1afbd30. An important message keeps its detail panel open.
		private void ConsoleShow()
		{
			m_isConsoleVisible = true;
			m_isSettingPanelOpen = false;
			if (!m_isLastMessageIsImportant)
				HideDetailsPanel();
			if (m_logConsoleAsset.AlwaysOpenOnMostRecentPage)
			{
				m_currentLogPageIndex = GetTotalPageCount() - 1;
				m_currentPaginationIndex = m_currentLogPageIndex / m_logConsoleAsset.LogPaginationMaxCount;
			}
			OnConsoleOpen?.Invoke();
		}

		private void ConsoleHide()
		{
			m_isConsoleVisible = false;
			m_isSettingPanelOpen = false;
			if (!m_isLastMessageIsImportant)
				HideDetailsPanel();
			OnConsoleClose?.Invoke();
		}

		public void EnableExclusionList(string name, bool enabled)
		{
			m_logHolderComponent.EnableExclusionList(name, enabled);
		}

		[ContextMenu("Toggle console")]
		public void Action_ConsoleToggle()
		{
			ConsoleToggle();
		}

		[ContextMenu("Show console")]
		public void Action_ConsoleShow()
		{
			ConsoleShow();
		}

		[ContextMenu("Hide console")]
		public void Action_ConsoleHide()
		{
			ConsoleHide();
		}
	}
}
