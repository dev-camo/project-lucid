using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public static class HLOutput
	{
		private static readonly HLOutputPlugin s_hlOutputPlugin;

		private static readonly List<Func<string>> s_bugInfoProvider;

		private static readonly Dictionary<BugInfo, List<Func<string>>> s_specificBugInfoProvider;

		private static readonly Stack<StringBuilder> s_stringBuilderPool;

		private static readonly Action<StringBuilder> s_errorScopedStringBuilderDisposeCallback;

		private static bool s_quitting;

		static HLOutput()
        {
            s_bugInfoProvider = new List<Func<string>>(0);
            s_specificBugInfoProvider = new Dictionary<BugInfo, List<Func<string>>>(0);
            s_stringBuilderPool = new Stack<StringBuilder>(0);
            s_errorScopedStringBuilderDisposeCallback = OnErrorScopedStringBuilderDisposed;
            s_hlOutputPlugin = new HLOutputPluginMacOS();
        }

		[RuntimeInitializeOnLoadMethod]
		private static void OnRuntimeMethodLoad()
        {
            s_hlOutputPlugin.InternalSetDevelopmentBuild(false);
            Application.quitting += OnApplicationQuitting;
            var configuration = SystemConfiguration.GetConfig<HLUnityCoreConfigurationAsset>();
            UnityEngine.Debug.unityLogger.logEnabled = configuration.AllowErrorLoggingInDistribution;
            UnityEngine.Debug.unityLogger.filterLogType = LogType.Error;
        }

		private static void OnApplicationQuitting()
        {
            s_quitting = true;
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void Log(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.Log(message, context);
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void Log(bool condition, object message, UnityEngine.Object context = null)
        {
            // Original shipped retail body is RET; the nested development-only call was stripped.
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void Log(string message, string color)
        {
            UnityEngine.Debug.LogFormat("<color={0}>{1}</color>", new object[] { color, message });
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void Log(bool condition, string message, string color)
        {
            // Original shipped retail body is RET.
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void Log(object message, UnityEngine.Object context, string color)
        {
            UnityEngine.Debug.LogFormat(context, "<color={0}>{1}</color>", new object[] { color, message });
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void Log(bool condition, object message, UnityEngine.Object context, string color)
        {
            // Original shipped retail body is RET.
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void LogFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogFormat(format, args);
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void LogFormat(bool condition, string format, params object[] args)
        {
            // Original shipped retail body is RET.
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void LogWarning(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.LogWarning(message, context);
        }

		[Conditional("BUILD_DEVELOPMENT")]
		public static void LogWarning(bool condition, object message, UnityEngine.Object context = null)
        {
            // Original shipped retail body is RET.
        }

		public static void LogError(object message, UnityEngine.Object context = null)
        {
            if (ProcessManager.IsSystemNull<HLUnityCore>()) return;
            ProcessManager.GetSystem<HLUnityCore>().LogError(message.ToString(), 0);
        }

		public static void LogError(bool condition, object message, UnityEngine.Object context = null)
        {
            if (condition) LogError(message, context);
        }

		public static void LogException(Exception exception, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.LogException(exception, context);
            if (ProcessManager.IsSystemNull<HLUnityCore>()) return;
            ProcessManager.GetSystem<HLUnityCore>().LogException(exception.Message, exception.StackTrace, 0);
        }

		public static void AddBugInfoProvider(Func<string> providerFunc)
        {
            s_bugInfoProvider.Add(providerFunc);
        }

		public static void AddBugInfoProvider(BugInfo bugInfo, Func<string> providerFunc)
        {
            s_specificBugInfoProvider.TryGetOrNew(bugInfo).Add(providerFunc);
        }

		public static bool RemoveBugInfoProvider(Func<string> providerFunc)
        {
            return s_bugInfoProvider.Remove(providerFunc);
        }

		public static bool RemoveBugInfoProvider(BugInfo bugInfo, Func<string> providerFunc)
        {
            if (!s_specificBugInfoProvider.TryGetValue(bugInfo, out var providers)) return false;
            bool removed = providers.Remove(providerFunc);
            if (providers.Count == 0) s_specificBugInfoProvider.Remove(bugInfo);
            return removed;
        }

		public static void LogBugInfo(in BugInfo bugInfo, string optionalMessage = "")
        {
            if (!string.IsNullOrWhiteSpace(optionalMessage)) optionalMessage = string.Concat("Message: ", optionalMessage, " - ");
            string message = string.Format("{0} - {1}Quitting: {2}", bugInfo, optionalMessage, s_quitting);
            bool hasSpecific = s_specificBugInfoProvider.TryGetValue(bugInfo, out var providers) && providers.Count > 0;
            if (s_bugInfoProvider.Count > 0 || hasSpecific) message += " - Extra Info: ";
            foreach (Func<string> provider in s_bugInfoProvider) message = string.Concat(message, provider(), " - ");
            if (hasSpecific)
                foreach (Func<string> provider in providers) message = string.Concat(message, provider(), " - ");
            message = message.TrimEndString(" - ");
            if (ProcessManager.IsSystemNull<HLUnityCore>()) return;
            ProcessManager.GetSystem<HLUnityCore>().LogError(message, 0);
        }

		public static void Breadcrumb(string message)
        {
            if (ProcessManager.IsSystemNull<HLUnityCore>()) return;
            ProcessManager.GetSystem<HLUnityCore>().Breadcrumb(message);
        }

		public static void Breadcrumb(bool condition, string message)
        {
            if (condition) Breadcrumb(message);
        }

		public static void DevBreadcrumb(string message)
        {
            if (ProcessManager.IsSystemNull<HLUnityCore>()) return;
            ProcessManager.GetSystem<HLUnityCore>().DevBreadcrumb(message);
        }

		public static void DevBreadcrumb(bool condition, string message)
        {
            if (condition) DevBreadcrumb(message);
        }

		public static ScopedStringBuilder GetLogScopedStringBuilder()
        {
            return default;
        }

		public static ScopedStringBuilder GetWarningScopedStringBuilder()
        {
            return default;
        }

		public static ScopedStringBuilder GetErrorScopedStringBuilder()
        {
            var builder = s_stringBuilderPool.Count > 0 ? s_stringBuilderPool.Pop() : new StringBuilder();
            return new ScopedStringBuilder(builder, s_errorScopedStringBuilderDisposeCallback);
        }

		private static void OnErrorScopedStringBuilderDisposed(StringBuilder stringBuilder)
        {
            if (stringBuilder.Length > 0) LogError(stringBuilder.ToString());
            s_stringBuilderPool.Push(stringBuilder);
        }

		public static void TestNativeLogError()
        {
            s_hlOutputPlugin.InternalTestNativeLogError();
        }
	}
}
