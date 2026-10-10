using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // HLCrashReport.Runtime 02000009. This preserves the supplied implementation
    // and its actual service boundaries; offline routing is a separate change.
    public sealed class HLCrashReport : MonoSingleton<HLCrashReport>
    {
        [SerializeField] private HLCrashReportAppKeys m_hlCrashReportAppKeys;
        [SerializeField] private bool m_autoStartService;
        [SerializeField] private bool m_logAppState = true;
        [SerializeField] private bool m_breadcrumbApplicationEvents = true;
        private ICrashReportSettings m_crashReportSettings;
        private HLCrashReportConfigurationAsset m_configurationAsset;
        private readonly List<LogErrorData> m_initialisationErrorQueue = new List<LogErrorData>();
        private static HLCrashReportNativeBridge s_crashReportNativeBridge = null;
        private static readonly string[] s_stackTraceIgnoredLinePrefixes =
            { "Hardlight.HLCrashReport", "Hardlight.HLOutput", "Hardlight.FastAction" };
        private const int InitialisationErrorQueueLimit = 100;

        // 06000010 and generated 06000030: the original callback stays inside
        // Awake and captures this. Registrations precede configuration and draining.
        protected override void Awake()
        {
            base.Awake();
            EnsureInitialised();
            ProcessManager.GetSystemRef<HLUnityCore>().InvokeOnValid(unityCore =>
            {
                unityCore.AddLogErrorHandler(TryLogErrorInternal);
                unityCore.AddLogExceptionHandler(LogExceptionInternal);
                unityCore.AddBreadcrumbHandler(BreadcrumbInternal);
                unityCore.AddCustomKeyStringHandler(CustomKeyInternal);
                unityCore.AddCustomKeyIntHandler(CustomKeyInternal);
                unityCore.AddCustomKeyFloatHandler(CustomKeyInternal);
                unityCore.AddCustomKeyBoolHandler(CustomKeyInternal);
                m_configurationAsset = SystemConfiguration.GetConfig<HLCrashReportConfigurationAsset>();
                m_configurationAsset.Initialise();
                if (m_autoStartService &&
                    (m_crashReportSettings == null || m_crashReportSettings.GetServiceEnabled()))
                {
                    SetAppCenterKeys();
                    StartService();
                }
                // A failed exclusion/provider/bridge operation retains the queue.
                // Foreach disposal precedes Clear on the successful path.
                foreach (LogErrorData error in m_initialisationErrorQueue)
                    LogErrorInternal(error.ErrorType, error.ErrorMessage, error.ErrorCode, error.StackTrace);
                m_initialisationErrorQueue.Clear();
            });
        }

        // 06000011: unregister in the original order, then StopService and base.
        // Preserve partial cleanup if an earlier operation throws.
        protected override void OnDestroy()
        {
            HLUnityCore unityCore = ProcessManager.GetSystemSafe<HLUnityCore>();
            if (unityCore != null)
            {
                unityCore.RemoveLogErrorHandler(TryLogErrorInternal);
                unityCore.RemoveLogExceptionHandler(LogExceptionInternal);
                unityCore.RemoveBreadcrumbHandler(BreadcrumbInternal);
                unityCore.RemoveCustomKeyStringHandler(CustomKeyInternal);
                unityCore.RemoveCustomKeyIntHandler(CustomKeyInternal);
                unityCore.RemoveCustomKeyFloatHandler(CustomKeyInternal);
                unityCore.RemoveCustomKeyBoolHandler(CustomKeyInternal);
            }
            StopService();
            base.OnDestroy();
        }

        // 06000012: key setup may fail before replacing the settings reference.
        public void Configure(ICrashReportSettings crashReportSettings)
        {
            SetAppCenterKeys();
            m_crashReportSettings = crashReportSettings;
        }

        // 06000013: a missing configuration is an original fault; Unity object
        // comparisons select authored keys before falling back to module keys.
        private void SetAppCenterKeys()
        {
            HLCrashReportConfigurationAsset configuration =
                SystemConfiguration.GetConfig<HLCrashReportConfigurationAsset>();
            if (configuration.CrashReportService !=
                HLCrashReportConfigurationAsset.SupportedCrashReportService.AppCenter) return;
            HLCrashReportAppKeys appKeys = m_hlCrashReportAppKeys;
            if (appKeys == null) appKeys = configuration.AppCenterConfiguration;
            if (appKeys != null)
            {
                if (TryGetCrashReportAppKey(appKeys, out string crashReportAppKey))
                    SetAppKey(crashReportAppKey);
            }
            else HLOutput.LogError("No crash report app keys exist.", this);
        }

        // 06000014: the out value is written before emptiness and editor checks.
        private bool TryGetCrashReportAppKey(HLCrashReportAppKeys appKeys, out string crashReportAppKey)
        {
            crashReportAppKey = appKeys.GetCrashReportAppKey();
            bool valid = !string.IsNullOrEmpty(crashReportAppKey);
            if (!valid && !Application.isEditor)
                HLOutput.LogError("No crash report app key can be set.", this);
            return valid;
        }

        // 06000015..1a: retain actual concrete bridge calls and uninitialized
        // bridge faults. The supplied bridge methods themselves are empty.
        public static void SetUserID(string id) => s_crashReportNativeBridge.SetUserID(id);
        private void SetAppKey(string key) => s_crashReportNativeBridge.SetAppKey(key);
        public static void GenerateTestCrashReport() => s_crashReportNativeBridge.GenerateTestCrashReport();
        // 06000018: preserved diagnostic recursion; never activate during recovery.
        public static void GenerateManagedTestCrash() => Instance.ForceManagedTestCrash(0);
        public static void StartService() => s_crashReportNativeBridge.StartService();
        public static void StopService() => s_crashReportNativeBridge.StopService();

        // 0600001b: extract and trim the stack before testing configuration.
        // The queue drops entries after 100 without changing prior entries.
        private void TryLogErrorInternal(string errorMessage, int errorCode = 0)
        {
            string stackTrace = TrimStackTrace(StackTraceUtility.ExtractStackTrace());
            if (m_configurationAsset == null)
            {
                if (m_initialisationErrorQueue.Count < InitialisationErrorQueueLimit)
                    m_initialisationErrorQueue.Add(new LogErrorData("error", errorMessage, errorCode, stackTrace));
            }
            else LogErrorInternal("error", errorMessage, errorCode, stackTrace);
        }

        // 0600001c: exclusion applies to errors, and list-null faults remain.
        private void LogErrorInternal(string errorType, string errorMessage, int errorCode, string stackTrace)
        {
            if (!m_configurationAsset.ErrorExclusionList.IsExcluded(errorMessage))
                s_crashReportNativeBridge.LogError(errorType, errorMessage, errorCode, stackTrace);
        }

        // 0600001d: exceptions keep their supplied stack and bypass error exclusions.
        private static void LogExceptionInternal(string errorMessage, string stackTrace, int errorCode = 0) =>
            s_crashReportNativeBridge.LogError("exception", errorMessage, errorCode, stackTrace);
        // 0600001e..23: the development breadcrumb has its own preserved method,
        // although the supplied Awake callback does not register it.
        private static void BreadcrumbInternal(string message) => s_crashReportNativeBridge.Breadcrumb(message);
        private static void DevBreadcrumbInternal(string message) => s_crashReportNativeBridge.Breadcrumb(message);
        private static void CustomKeyInternal(string key, string value) => s_crashReportNativeBridge.CustomKey(key, value);
        private static void CustomKeyInternal(string key, int value) => s_crashReportNativeBridge.CustomKey(key, value);
        private static void CustomKeyInternal(string key, float value) => s_crashReportNativeBridge.CustomKey(key, value);
        private static void CustomKeyInternal(string key, bool value) => s_crashReportNativeBridge.CustomKey(key, value);

        // 06000024: publish the new bridge before its Initialise call. Do not
        // replace the concrete original provider with a factory or offline shim.
        private void EnsureInitialised()
        {
            if (s_crashReportNativeBridge != null) return;
            s_crashReportNativeBridge = new HLCrashReportNativeBridge();
            s_crashReportNativeBridge.Initialise(this);
        }

        // 06000025..26: disabled flags return before touching the bridge.
        private void LogAppState(string message)
        {
            if (m_logAppState) s_crashReportNativeBridge.CustomKey("AppState", message);
        }
        private void BreadcrumbApplicationEvent(string message)
        {
            if (m_breadcrumbApplicationEvents) s_crashReportNativeBridge.Breadcrumb(message);
        }

        // 06000027..29: initialization precedes formatting and event/state reports.
        // Bridge inlining removed the final state argument selection from native
        // code; the retained release literals support these original state labels.
        private void OnApplicationFocus(bool focused)
        {
            EnsureInitialised();
            BreadcrumbApplicationEvent(string.Format("OnApplicationFocus({0})", focused));
            LogAppState(focused ? "Focused" : "LostFocus");
        }
        private void OnApplicationPause(bool paused)
        {
            EnsureInitialised();
            BreadcrumbApplicationEvent(string.Format("OnApplicationPause({0})", paused));
            LogAppState(paused ? "Paused" : "Active");
        }
        private void OnApplicationQuit()
        {
            EnsureInitialised();
            BreadcrumbApplicationEvent("OnApplicationQuit");
            LogAppState("Quitting");
        }

        // 0600002a: only leading ignored lines are removed. With no newline,
        // the original performs a second prefix check before choosing empty.
        public static string TrimStackTrace(string stackTrace)
        {
            if (string.IsNullOrWhiteSpace(stackTrace)) return string.Empty;
            while (stackTrace.Length > 0 && StackTraceStartsWithIgnoredLine(stackTrace))
            {
                int lineEnd = stackTrace.IndexOf('\n', 0);
                if (lineEnd < 0)
                    return StackTraceStartsWithIgnoredLine(stackTrace) ? string.Empty : stackTrace;
                stackTrace = stackTrace.Substring(lineEnd + 1);
            }
            return stackTrace;
        }

        // 0600002b: indexed array reads preserve ordering and ignore case using
        // invariant culture; this helper contains no stack-null normalization.
        private static bool StackTraceStartsWithIgnoredLine(string stackTrace)
        {
            for (int i = 0; i < s_stackTraceIgnoredLinePrefixes.Length; ++i)
                if (stackTrace.StartsWith(s_stackTraceIgnoredLinePrefixes[i], true, CultureInfo.InvariantCulture))
                    return true;
            return false;
        }

        // 0600002c: no trimming or prefix removal is applied to the first line.
        public static string ExtractFirstFunctionFromStackTrace(string stackTrace)
        {
            if (string.IsNullOrWhiteSpace(stackTrace)) return string.Empty;
            int lineEnd = stackTrace.IndexOf('\n', 0);
            return lineEnd < 0 ? stackTrace : stackTrace.Substring(0, lineEnd);
        }

        // 0600002d: both unoptimized native slices increment the local but pass
        // the previous value recursively. This intentional crash stays preserved.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ForceManagedTestCrash(int i) => ForceManagedTestCrash(i++);

        // 0600002e: field initializers precede the real MonoSingleton constructor.
        public HLCrashReport() { }
        // 0600002f is emitted by the ordered static field initializers above;
        // an explicit static constructor would lose original BeforeFieldInit.
    }
}
