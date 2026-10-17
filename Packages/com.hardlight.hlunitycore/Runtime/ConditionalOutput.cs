using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 8807, methods 0x06000168..0x06000177.
	// The shipped player was compiled without BUILD_DEVELOPMENT, so every call to
	// one of the [Conditional] members below is absent from the retail bodies,
	// including calls between them. Those call sites are not recoverable and are
	// not reconstructed; each body here is the shipped one.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class ConditionalOutput
	{
		// TypeDefIndex 8806, accessors 0x06000178..0x06000181, .ctor 0x06000182.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		private class LogData
		{
			public Type Type { get; set; }

			public bool Condition { get; set; }

			public object Message { get; set; }

			public UnityEngine.Object Context { get; set; }

			public string Colour { get; set; }
		}

		// The property and event come first: the event's backing field is static
		// slot 0x0 ahead of the four fields, and get_/add_/remove_ are 0x06000168..6a.
		public static IReadOnlyDictionary<Type, bool> ConditionalLoggerLookup => s_conditionalLoggerLookup;

		public static event Action OnConditionalLoggerToggled;

		private static readonly Dictionary<Type, bool> s_conditionalLoggerLookup;

		private static readonly Queue<LogData> s_logQueue;

		private static ConditionalOutputSettings s_conditionalOutputSettings;

		private static bool s_initialised;

		// 0x0600016b, 0x1aa11c0: explicit, so the type is not beforefieldinit
		// (original flags 0x181). The lookup is allocated before the queue.
		static ConditionalOutput()
		{
			s_conditionalLoggerLookup = new Dictionary<Type, bool>();
			s_logQueue = new Queue<LogData>();
		}

		// 0x0600016c, x86-64 0x1aa12a0: the retail body only latches s_initialised.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		[Conditional("BUILD_DEVELOPMENT")]
		public static void Initialise()
		{
			if (s_initialised) return;
			s_initialised = true;
		}

		// 0x0600016d, 0x1aa1340: the first non-abstract type assignable to
		// ConditionalOutputPreInitialiseSettings, the base included, in
		// GetAssemblies/GetTypes order wins. The fallback at 0x1aa156f runs only
		// when no such type is enumerated.
		[Conditional("BUILD_DEVELOPMENT")]
		private static void CreatePreInitialiseSettings()
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			Type preInitialiseSettingsType = typeof(ConditionalOutputPreInitialiseSettings);
			foreach (Assembly assembly in assemblies)
			{
				foreach (Type type in assembly.GetTypes())
				{
					if (type.IsAbstract || !preInitialiseSettingsType.IsAssignableFrom(type)) continue;
					var preInitialiseSettings = (ConditionalOutputPreInitialiseSettings)Activator.CreateInstance(type);
					s_conditionalOutputSettings = preInitialiseSettings.CreateConditionalOutputSettings();
					return;
				}
			}
			s_conditionalOutputSettings = new ConditionalOutputPreInitialiseSettings().CreateConditionalOutputSettings();
		}

		// 0x0600016e, 0x1aa1660: a missing configuration keeps the current settings.
		[Conditional("BUILD_DEVELOPMENT")]
		private static void InitialiseSettings()
		{
			ConditionalOutputConfiguration configuration = Resources.Load<ConditionalOutputConfiguration>(ConditionalOutputConfiguration.FileName);
			if (configuration == null) return;
			s_conditionalOutputSettings = configuration.ConditionalOutputSettings;
		}

		// 0x0600016f, 0x1aa1730: the interface is matched by name and then by
		// identity. The default state is read before the PlayerPrefs key.
		[Conditional("BUILD_DEVELOPMENT")]
		private static void InitialiseLoggerLookup()
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			Type conditionalLoggerType = typeof(IConditionalLogger);
			foreach (Assembly assembly in assemblies)
			{
				foreach (Type type in assembly.GetTypes())
				{
					if (type.IsAbstract || type.GetInterface(conditionalLoggerType.Name) != conditionalLoggerType) continue;
					bool defaultLogState = s_conditionalOutputSettings.GetDefaultLogState(type);
					bool enabled = PlayerPrefs.GetInt(type.FullName, defaultLogState ? 1 : 0) == 1;
					s_conditionalLoggerLookup.Add(type, enabled);
				}
			}
		}

		// 0x06000170, shared x86-64 0x8f15a0: the retail body is RET.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void ConditionalLog<T>(this T conditionalLogger, object message, UnityEngine.Object context = null, string colour = null) where T : IConditionalLogger
		{
		}

		// 0x06000171, shared x86-64 0x8f15b0: a constrained call on the logger
		// supplies the type passed to ConditionalLogInternal.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void ConditionalLog<T>(this T conditionalLogger, bool condition, object message, UnityEngine.Object context = null, string colour = null) where T : IConditionalLogger
		{
			ConditionalLogInternal(conditionalLogger.GetType(), condition, message, context, colour);
		}

		// 0x06000172, 0x1aa1b40. Before initialisation the lookup is not read:
		// ForceLog treats the logger as enabled and Queue defers the call.
		private static void ConditionalLogInternal(Type type, bool condition, object message, UnityEngine.Object context = null, string colour = null)
		{
			bool loggerEnabled;
			if (s_initialised)
			{
				loggerEnabled = s_conditionalLoggerLookup[type];
			}
			else
			{
				switch (s_conditionalOutputSettings.PreInitialiseLogBehaviour)
				{
					case ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog:
						loggerEnabled = true;
						break;
					case ConditionalOutputSettings.UnhandledLogBehaviour.Queue:
						s_logQueue.Enqueue(new LogData { Type = type, Condition = condition, Message = message, Context = context, Colour = colour });
						return;
					default:
						HLOutput.LogError(string.Format("Unhandled PreInitialiseLogBehaviour value '{0}' in ConditionalLogSettings.", s_conditionalOutputSettings.PreInitialiseLogBehaviour));
						return;
				}
			}
			if (loggerEnabled && condition)
			{
				// The retail body formats this text and discards it; the
				// development-only output call that took it, with context and
				// colour, is compiled out.
				_ = string.Format("{0} - {1}", type.Name, message);
			}
		}

		// 0x06000173, shared x86-64 0x8f1700: the retail body is RET.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void ToggleLog<T>(bool? enabled = null) where T : IConditionalLogger
		{
		}

		// 0x06000174, 0x1aa1e40: a null request inverts the current lookup entry.
		// The new state is persisted under the type's full name before the event.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void ToggleLog(Type type, bool? enabled = null)
		{
			enabled ??= !s_conditionalLoggerLookup[type];
			s_conditionalLoggerLookup[type] = enabled.Value;
			PlayerPrefs.SetInt(type.FullName, enabled.Value ? 1 : 0);
			PlayerPrefs.Save();
			OnConditionalLoggerToggled?.Invoke();
		}

		// 0x06000175, 0x1aa2010: replays queued calls through the same gate.
		[Conditional("BUILD_DEVELOPMENT")]
		private static void DumpLogQueue()
		{
			while (s_logQueue.Count > 0)
			{
				LogData logData = s_logQueue.Dequeue();
				// 0x1aa2075..0x1aa209d read Message and format it before loading
				// the other four properties.
				string delayedMessage = string.Format("(Delayed log) {0}", logData.Message);
				ConditionalLogInternal(logData.Type, logData.Condition, delayedMessage, logData.Context, logData.Colour);
			}
		}

		// 0x06000176, 0x1aa2120.
		public static bool GetDefaultLogState(Type type)
		{
			return s_conditionalOutputSettings.GetDefaultLogState(type);
		}

		// 0x06000177, 0x1aa2180: ignored until initialised.
		public static void SetDefaultLogState(Type type, bool enabled)
		{
			if (!s_initialised) return;
			s_conditionalOutputSettings.SetDefaultLogState(type, enabled);
		}
	}
}
