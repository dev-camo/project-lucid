using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0x02000170 (TypeDefIndex 9119), methods
	// 0x06000a44..0x06000a59; bodies translated from the 1.10.1 x86-64 binary.
	// Shipped assets use this script: the reference export's
	// Assets/GameObject/LogHandler.prefab (instanced in s_common.unity) holds
	// it under script GUID 44031b67d3b370f585efb8453c56e4a4 with
	// m_settingsAsset -> SDT-LogHandlerSettings. The script binding is still
	// pending (no Unity serialization proof yet), so those assets do not bind
	// to this type and the .meta GUID is not the export's.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLLogHandler : MonoBehaviour, ISystem
	{
		private const int BytesPerMB = 1000000;
		private const int AverageLogMessagesPerMB = 1000;
		private const int DefaultLogSizeInMB = 10;
		private const int StringBuilderCapacity = 10240;
		private const int StackTraceSuperfluousCharactersToRemoveOffset = 3;
		private const int StackTraceSuperfluousCharactersToRemoveCount = 2;

		[SerializeField]
		private HLLogHandlerSettingScriptableObject m_settingsAsset;

		// 0x06000a58 creates only the queue; the message list waits for Awake.
		private readonly ConcurrentQueue<HLLogMessage> m_logQueue = new ConcurrentQueue<HLLogMessage>();

		private static StringBuilder s_tempStringBuilder = new StringBuilder(StringBuilderCapacity);

		// 0x06000a59 adds the rows in this order; Assert shares the Error style.
		private static readonly Dictionary<LogType, string> s_cssStylePerLogType = new Dictionary<LogType, string>
		{
			{ LogType.Log, "font-weight:bold" },
			{ LogType.Warning, "color:orange; font-weight:bold" },
			{ LogType.Error, "color:red; font-weight:bold" },
			{ LogType.Assert, "color:red; font-weight:bold" },
			{ LogType.Exception, "color:darkcyan; font-weight:bold" },
		};

		private List<HLLogMessage> m_logMessages;
		private HLLogHandlerSetting m_currentSetting;
		private ExclusionList[] m_exclusionLists;
		private long m_currentLogByteSize;
		private bool m_isAutoExportEnabled;

		public IReadOnlyList<HLLogMessage> LogMessages => m_logMessages;

		public event Action<HLLogMessage> OnLogMessageReceived;

		// 0x06000a47. A missing asset, or no row for the active platform, falls
		// back to a default setting; a missing exclusion array to Array.Empty.
		// A null asset skips the store entirely (0x1b00b1c -> 0x1b00b86), so the
		// default replaces any previous setting without testing it; keep the
		// assignment inside the condition.
		private void Awake()
		{
			ProcessManager.RegisterSystem(this);
			if (m_settingsAsset == null || (m_currentSetting = m_settingsAsset.GetPlatformSetting()) == null)
				m_currentSetting = new HLLogHandlerSetting();
			if (m_settingsAsset == null || (m_exclusionLists = m_settingsAsset.LogExclusionList) == null)
				m_exclusionLists = Array.Empty<ExclusionList>();
			m_isAutoExportEnabled = m_currentSetting.MaxLogSizeInMB > 0;
			m_logMessages = new List<HLLogMessage>(GetEstimatedLogMessagesCount());
		}

		private void OnDestroy()
		{
			ProcessManager.UnregisterSystem(this);
		}

		private void OnEnable()
		{
			Application.logMessageReceivedThreaded += LogMessageReceiveThreaded;
		}

		private void Update()
		{
			LogQueueUpdate();
			LogSizeCheckTresholdForAutoExport();
		}

		// 0x06000a4b. The byte size adds the stack trace (when present) and then
		// the message length; the event is raised after the message is listed.
		private void LogQueueUpdate()
		{
			HLLogMessage logMessage;
			while (m_logQueue.TryDequeue(out logMessage))
			{
				m_currentLogByteSize = m_currentLogByteSize + (logMessage.HasStackTrace ? logMessage.StackTrace.Length : 0) + logMessage.Message.Length;
				m_logMessages.Add(logMessage);
				OnLogMessageReceived?.Invoke(logMessage);
			}
		}

		// 0x06000a4c. The threshold is an int product widened only afterwards.
		private void LogSizeCheckTresholdForAutoExport()
		{
			if (m_isAutoExportEnabled && m_currentLogByteSize >= m_currentSetting.MaxLogSizeInMB * BytesPerMB)
			{
				LogExport();
				LogFlush();
			}
		}

		private void OnDisable()
		{
			Application.logMessageReceivedThreaded -= LogMessageReceiveThreaded;
		}

		// 0x06000a4e reads only the configured stage, never the pause argument.
		private void OnApplicationPause(bool pause)
		{
			if ((m_currentSetting.LogExportStage & ExportStages.OnApplicationPause) != 0)
			{
				LogExport();
				LogFlush();
			}
		}

		private void OnApplicationQuit()
		{
			if ((m_currentSetting.LogExportStage & ExportStages.OnApplicationQuit) != 0)
			{
				LogExport();
				LogFlush();
			}
		}

		// 0x06000a50. A mask equal to HLLogConsole.AllLogTypes accepts every type,
		// including values outside LogType; otherwise only the five mapped types
		// pass (jump table 0x1b01688). Exclusions see the raw message. The stack
		// trace is kept or dropped by the second mask with the same rule (lookup
		// table 0x29a8370). Both switches are written the same way; the jump
		// table versus lookup table is the optimiser's choice, not a different
		// source construct.
		private void LogMessageReceiveThreaded(string message, string stackTrace, LogType type)
		{
			bool isSupported = m_currentSetting.LogTypeSupport == HLLogConsole.AllLogTypes;
			if (!isSupported)
			{
				switch (type)
				{
					case LogType.Error: isSupported = (m_currentSetting.LogTypeSupport & SupportedLogTypes.Error) != 0; break;
					case LogType.Assert: isSupported = (m_currentSetting.LogTypeSupport & SupportedLogTypes.Assert) != 0; break;
					case LogType.Warning: isSupported = (m_currentSetting.LogTypeSupport & SupportedLogTypes.Warning) != 0; break;
					case LogType.Log: isSupported = (m_currentSetting.LogTypeSupport & SupportedLogTypes.Log) != 0; break;
					case LogType.Exception: isSupported = (m_currentSetting.LogTypeSupport & SupportedLogTypes.Exception) != 0; break;
				}
			}
			if (!isSupported)
				return;

			for (int i = 0; i < m_exclusionLists.Length; i++)
			{
				if (m_exclusionLists[i].IsExcluded(message))
					return;
			}

			bool extractStackTrace = m_currentSetting.StackTraceExtractionPerLogType == HLLogConsole.AllLogTypes;
			if (!extractStackTrace)
			{
				switch (type)
				{
					case LogType.Error: extractStackTrace = (m_currentSetting.StackTraceExtractionPerLogType & SupportedLogTypes.Error) != 0; break;
					case LogType.Assert: extractStackTrace = (m_currentSetting.StackTraceExtractionPerLogType & SupportedLogTypes.Assert) != 0; break;
					case LogType.Warning: extractStackTrace = (m_currentSetting.StackTraceExtractionPerLogType & SupportedLogTypes.Warning) != 0; break;
					case LogType.Log: extractStackTrace = (m_currentSetting.StackTraceExtractionPerLogType & SupportedLogTypes.Log) != 0; break;
					case LogType.Exception: extractStackTrace = (m_currentSetting.StackTraceExtractionPerLogType & SupportedLogTypes.Exception) != 0; break;
				}
			}
			string extractedStackTrace = extractStackTrace ? stackTrace : null;
			m_logQueue.Enqueue(new HLLogMessage { Message = message, Type = type, StackTrace = extractedStackTrace });
		}

		// 0x06000a51. OpString addition appends in place, so each extension is
		// added to the previous file name: <name>.txt, <name>.txt.json and
		// <name>.txt.json.md. Any exception is swallowed.
		[ContextMenu("Export log")]
		public void LogExport()
		{
			try
			{
				if ((m_currentSetting.LogExportFormat & ExportFormats.All) != 0)
				{
					OpString path = OpString.medium + Application.persistentDataPath + "/" + m_currentSetting.ExportPath;
					Directory.CreateDirectory(path);
					DateTime now = DateTime.Now;
					string fileName = OpString.medium + "/" + now.ToShortDateString().Replace("/", "-") + "_" + now.ToLongTimeString().Replace(':', '-').RemoveWhitespace();
					path += fileName;
					if ((m_currentSetting.LogExportFormat & ExportFormats.Txt) != 0)
						File.WriteAllText(path + ".txt", LogGetInTextFormat());
					if ((m_currentSetting.LogExportFormat & ExportFormats.Json) != 0)
						File.WriteAllText(path + ".json", LogGetInJsonFormat());
					if ((m_currentSetting.LogExportFormat & ExportFormats.Markdown) != 0)
						File.WriteAllText(path + ".md", LogGetInMarkdownFormat());
				}
			}
			catch (Exception)
			{
			}
		}

		public void LogFlush()
		{
			m_currentLogByteSize = 0;
			m_logMessages.Clear();
		}

		public int GetEstimatedLogMessagesCount()
		{
			return m_isAutoExportEnabled ? m_currentSetting.MaxLogSizeInMB * AverageLogMessagesPerMB : DefaultLogSizeInMB * AverageLogMessagesPerMB;
		}

		// 0x06000a54. Every field read goes back through the list indexer.
		public string LogGetInTextFormat()
		{
			s_tempStringBuilder.Length = 0;
			int count = m_logMessages.Count;
			for (int i = 0; i < count; i++)
			{
				if (m_logMessages[i].HasStackTrace)
				{
					s_tempStringBuilder.Append(m_logMessages[i].Type.ToString());
					s_tempStringBuilder.Append(": ");
					s_tempStringBuilder.Append(m_logMessages[i].Message);
					s_tempStringBuilder.Append("\nStack trace:\n");
					s_tempStringBuilder.Append(m_logMessages[i].StackTrace);
					s_tempStringBuilder.Append("\n");
				}
				else
				{
					s_tempStringBuilder.Append(m_logMessages[i].Type.ToString());
					s_tempStringBuilder.Append(": ");
					s_tempStringBuilder.Append(m_logMessages[i].Message);
					s_tempStringBuilder.Append("\n");
				}
			}
			return s_tempStringBuilder.ToString();
		}

		// 0x06000a55. Stack trace lines become "+ " bullets, then two characters
		// starting three from the end are removed (the final "\n+" for a trace
		// ending in a newline). The styled header uses the per-type CSS row.
		public string LogGetInMarkdownFormat()
		{
			s_tempStringBuilder.Length = 0;
			int count = m_logMessages.Count;
			for (int i = 0; i < count; i++)
			{
				if (m_logMessages[i].HasStackTrace)
				{
					string stackTrace = m_logMessages[i].StackTrace.Replace("\n", "\n+ ");
					stackTrace = stackTrace.Remove(stackTrace.Length - StackTraceSuperfluousCharactersToRemoveOffset, StackTraceSuperfluousCharactersToRemoveCount);
					s_tempStringBuilder.Append("**<span style=\"");
					s_tempStringBuilder.Append(s_cssStylePerLogType[m_logMessages[i].Type]);
					s_tempStringBuilder.Append("\">");
					s_tempStringBuilder.Append(m_logMessages[i].Type.ToString());
					s_tempStringBuilder.Append("</span>**: ");
					s_tempStringBuilder.Append(m_logMessages[i].Message);
					s_tempStringBuilder.Append("\n\n**Stack trace**:\n+ ");
					s_tempStringBuilder.Append(stackTrace);
					s_tempStringBuilder.Append("\n*****\n");
				}
				else
				{
					s_tempStringBuilder.Append("**");
					s_tempStringBuilder.Append(m_logMessages[i].Type.ToString());
					s_tempStringBuilder.Append("**: ");
					s_tempStringBuilder.Append(m_logMessages[i].Message);
					s_tempStringBuilder.Append("\n*****\n");
				}
			}
			return s_tempStringBuilder.ToString();
		}

		// 0x06000a56: one pretty-printed JsonUtility object per line group.
		public string LogGetInJsonFormat()
		{
			s_tempStringBuilder.Length = 0;
			int count = m_logMessages.Count;
			for (int i = 0; i < count; i++)
			{
				s_tempStringBuilder.Append(JsonUtility.ToJson(m_logMessages[i], true));
				s_tempStringBuilder.Append("\n");
			}
			return s_tempStringBuilder.ToString();
		}

		// 0x06000a57: only the first list with an equal name is changed.
		public void EnableExclusionList(string name, bool enabled)
		{
			for (int i = 0; i < m_exclusionLists.Length; i++)
			{
				if (m_exclusionLists[i].Name != name)
					continue;
				m_exclusionLists[i].Enabled = enabled;
				break;
			}
		}
	}
}
