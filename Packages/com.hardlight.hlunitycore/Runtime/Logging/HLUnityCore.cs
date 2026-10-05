using System;
using System.Runtime.ExceptionServices;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLUnityCore : MonoSingleton<HLUnityCore>
	{
		private static IHLUnityCoreNativeBridge s_unityCoreNativeBridge;

		private static IHLTrackingAuthorisationNativeBridge s_trackingAuthorisationNativeBridge;

		private FastAction<string, int> m_logErrorHandler = new FastAction<string, int>();

		private FastAction<string, string, int> m_logExceptionHandler = new FastAction<string, string, int>();

		private FastAction<string> m_devBreadcrumbHandler = new FastAction<string>();

		private FastAction<string> m_breadcrumbHandler = new FastAction<string>();

		private FastAction<string, string> m_customKeyStringHandler = new FastAction<string, string>();

		private FastAction<string, int> m_customKeyIntHandler = new FastAction<string, int>();

		private FastAction<string, float> m_customKeyFloatHandler = new FastAction<string, float>();

		private FastAction<string, bool> m_customKeyBoolHandler = new FastAction<string, bool>();

		private FastAction<int> m_trackingAuthorisationHandler = new FastAction<int>();

		private HLUnityCoreConfigurationAsset m_unityCoreConfigurationAsset;

		private float m_lastLogTime = float.MinValue;

		private bool m_breadcrumbHandlersEnabled = true;

		private bool m_customKeyHandlersEnabled = true;

		// 0x060008d4; original named ARM64 HLUnityCore body.
        protected override void Awake()
        {
            base.Awake();
            EnsureInitialised();
            AppDomain.CurrentDomain.UnhandledException += OnHandleUnresolvedException;
            AppDomain.CurrentDomain.FirstChanceException += OnHandleFirstChanceException;
            m_unityCoreConfigurationAsset = SystemConfiguration.GetConfig<HLUnityCoreConfigurationAsset>();
            if (m_unityCoreConfigurationAsset.LowMemoryConfig.Enabled) Application.lowMemory += OnLowMemory;
        }

		// 0x060008d5; original named ARM64 HLUnityCore body.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            AppDomain.CurrentDomain.UnhandledException -= OnHandleUnresolvedException;
            AppDomain.CurrentDomain.FirstChanceException -= OnHandleFirstChanceException;
            Application.lowMemory -= OnLowMemory;
        }

		// 0x060008d6; original named ARM64 HLUnityCore body.
        public static void OnHandleFirstChanceException(object sender, FirstChanceExceptionEventArgs args)
        {
            if (args == null) return;
            Exception exception = args.Exception;
            if (exception == null) return;
            if (NotNull()) Instance.LogException(exception.Message, exception.StackTrace, exception.HResult);
            HLOutput.LogError(string.Format("First Chance Exception {0}", exception));
        }

		[HandleProcessCorruptedStateExceptions]
		// 0x060008d7; original named ARM64 HLUnityCore body.
        public static void OnHandleUnresolvedException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args == null) return;
            Exception exception = args.ExceptionObject as Exception;
            if (exception == null) return;
            if (NotNull()) Instance.LogException(exception.Message, exception.StackTrace, exception.HResult);
            HLOutput.LogError(string.Format("Unhandled Exception {0}", exception));
        }

		// 0x060008d8; original named ARM64 HLUnityCore body.
        public static void LogOrThrowException(string message)
        {
            if (IsNull() || !Instance.m_unityCoreConfigurationAsset.LogInPlaceOfException) throw new NullReferenceException(message);
            Instance.LogException(message, string.Empty, 0);
        }

		// 0x060008d9; original named ARM64 HLUnityCore body.
        private void OnLowMemory()
        {
            var configuration = m_unityCoreConfigurationAsset.LowMemoryConfig;
            bool emptyMessage = string.IsNullOrEmpty(configuration.BreadcrumbMessage);
            float currentTime = Time.unscaledTime;
            if (!emptyMessage && currentTime - m_lastLogTime >= configuration.MinimumLogIntervalSeconds)
            {
                m_lastLogTime = Time.unscaledTime;
                HLOutput.Breadcrumb(configuration.BreadcrumbMessage);
            }
            if (configuration.TriggerResourcesUnloadAssets) Resources.UnloadUnusedAssets();
        }

		// 0x060008da; original named ARM64 HLUnityCore body.
        private void EnsureInitialised()
        {
            if (s_unityCoreNativeBridge != null) return;
            s_unityCoreNativeBridge = new HLUnityCoreNativeBridge();
            s_unityCoreNativeBridge.Initialise(this);
            s_trackingAuthorisationNativeBridge = HLTrackingAuthorisationNativeBridge.Initialise(this);
            Application.logMessageReceived += OnHandleLog;
        }

		// 0x060008db; original named ARM64 HLUnityCore body.
        private void OnHandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) m_logExceptionHandler.Invoke(logString, stackTrace, 0);
        }

		// 0x060008dc; original named ARM64 HLUnityCore body.
        public static string GetDeviceLocale()
        {
            string language = s_unityCoreNativeBridge.GetDeviceLanguageCode();
            string country = s_unityCoreNativeBridge.GetDeviceISO2CountryCode();
            return string.Concat(language, "_", country);
        }

		// 0x060008dd; original named ARM64 HLUnityCore body.
        public static string GetDeviceRawLocale()
        {
            return s_unityCoreNativeBridge.GetDeviceRawLocale();
        }

		// 0x060008de; original named ARM64 HLUnityCore body.
        public static string GetDeviceLanguageCode()
        {
            return s_unityCoreNativeBridge.GetDeviceLanguageCode();
        }

		// 0x060008df; original named ARM64 HLUnityCore body.
        public static string GetDeviceOS()
        {
            return s_unityCoreNativeBridge.GetDeviceOS();
        }

		// 0x060008e0; original named ARM64 HLUnityCore body.
        public static string GetDeviceID()
        {
            return s_unityCoreNativeBridge.GetDeviceID();
        }

		// 0x060008e1; original named ARM64 HLUnityCore body.
        public static string GetDeviceISO2CountryCode()
        {
            return s_unityCoreNativeBridge.GetDeviceISO2CountryCode();
        }

		// 0x060008e2; original named ARM64 HLUnityCore body.
        public static int GetClientCode(out uint clientCode)
        {
            return s_unityCoreNativeBridge.GetClientCode(out clientCode);
        }

		// 0x060008e3; original named ARM64 HLUnityCore body.
        public static int GetScreenUnusableHeaderHeightInPixels()
        {
            return s_unityCoreNativeBridge.GetScreenUnusableHeaderHeightInPixels();
        }

		// 0x060008e4; original named ARM64 HLUnityCore body.
        public static int GetScreenUnusableFooterHeightInPixels()
        {
            return s_unityCoreNativeBridge.GetScreenUnusableFooterHeightInPixels();
        }

		// 0x060008e5; original named ARM64 HLUnityCore body.
        public static bool DoesThisDeviceHaveANotch()
        {
            return s_unityCoreNativeBridge.DoesThisDeviceHaveANotch();
        }

		// 0x060008e6; original named ARM64 HLUnityCore body.
        public static bool DoesThisDeviceHaveASafeArea()
        {
            return Screen.safeArea.y > 0f || Screen.safeArea.yMax < Screen.height || Screen.safeArea.x > 0f || Screen.safeArea.xMax < Screen.width;
        }

		// 0x060008e7; original named ARM64 HLUnityCore body.
        public static bool DoesThisDeviceHaveATopNotch()
        {
            return Screen.safeArea.y > 0f;
        }

		// 0x060008e8; original named ARM64 HLUnityCore body.
        public static bool DoesThisDeviceHaveABottomNotch()
        {
            return Screen.safeArea.yMax < Screen.height;
        }

		// 0x060008e9; original named ARM64 HLUnityCore body.
        public static bool DoesThisDeviceHaveALeftNotch()
        {
            return Screen.safeArea.x > 0f;
        }

		// 0x060008ea; original named ARM64 HLUnityCore body.
        public static bool DoesThisDeviceHaveARightNotch()
        {
            return Screen.safeArea.xMax < Screen.width;
        }

		// 0x060008eb; original named ARM64 HLUnityCore body.
        public static int GetPixelsFromNativeUnitDistance(float distance)
        {
            return s_unityCoreNativeBridge.GetPixelsFromNativeUnitDistance(distance);
        }

		// 0x060008ec; original named ARM64 HLUnityCore body.
        public static int GetBottomSafeAreaInsetInPixels()
        {
            return s_unityCoreNativeBridge.GetBottomSafeAreaInsetInPixels();
        }

		// 0x060008ed; original named ARM64 HLUnityCore body.
        public static int GetTopSafeAreaInsetInPixels()
        {
            return s_unityCoreNativeBridge.GetTopSafeAreaInsetInPixels();
        }

		// 0x060008ee; original named ARM64 HLUnityCore body.
        public static int CalculateApplicationChecksum(string buildIdentifier)
        {
            return s_unityCoreNativeBridge.CalculateApplicationChecksum() ^ HLCRC32.GenerateInt(buildIdentifier);
        }

		// 0x060008ef; original named ARM64 HLUnityCore body.
        public static bool CanDeviceRequestTrackingAuthorisation()
        {
            return s_trackingAuthorisationNativeBridge.CanDeviceRequestTrackingAuthorisation();
        }

		// 0x060008f0; original named ARM64 HLUnityCore body.
        public static void RequestTrackingAuthorisation()
        {
            s_trackingAuthorisationNativeBridge.RequestTrackingAuthorisation();
        }

		// 0x060008f1; original named ARM64 HLUnityCore body.
        public static int RequestTrackingAuthorisationStatus()
        {
            return s_trackingAuthorisationNativeBridge.RequestTrackingAuthorisationStatus();
        }

		// 0x060008f2; original named ARM64 HLUnityCore body.
        public void Native_TrackingAuthorisationStatus(string response)
        {
            int.TryParse(response, out int status);
            m_trackingAuthorisationHandler.Invoke(status);
        }

		// 0x060008f3; original named ARM64 HLUnityCore body.
        public void AddTrackingAuthorisationHandler(Action<int> action)
        {
            m_trackingAuthorisationHandler += action;
        }

		// 0x060008f4; original named ARM64 HLUnityCore body.
        public void RemoveTrackingAuthorisationHandler(Action<int> action)
        {
            m_trackingAuthorisationHandler -= action;
        }

		// 0x060008f5; original named ARM64 HLUnityCore body.
        public static void LogToDeviceConsole(string message)
        {
            s_unityCoreNativeBridge.LogToDeviceConsole(message);
        }

		// 0x060008f6; original named ARM64 HLUnityCore body.
        public void AddLogErrorHandler(Action<string, int> action)
        {
            m_logErrorHandler += action;
        }

		// 0x060008f7; original named ARM64 HLUnityCore body.
        public void RemoveLogErrorHandler(Action<string, int> action)
        {
            m_logErrorHandler -= action;
        }

		// 0x060008f8; original named ARM64 HLUnityCore body.
        public void LogError(string errorMessage, int errorCode = 0)
        {
            m_logErrorHandler.Invoke(errorMessage, errorCode);
        }

		// 0x060008f9; original named ARM64 HLUnityCore body.
        public void AddLogExceptionHandler(Action<string, string, int> action)
        {
            m_logExceptionHandler += action;
        }

		// 0x060008fa; original named ARM64 HLUnityCore body.
        public void RemoveLogExceptionHandler(Action<string, string, int> action)
        {
            m_logExceptionHandler -= action;
        }

		// 0x060008fb; original named ARM64 HLUnityCore body.
        public void LogException(string errorMessage, string stackTrace, int errorCode = 0)
        {
            m_logExceptionHandler.Invoke(errorMessage, stackTrace, errorCode);
        }

		// 0x060008fc; original named ARM64 HLUnityCore body.
        public void ToggleBreadcrumbHandlers()
        {
            m_breadcrumbHandlersEnabled = !m_breadcrumbHandlersEnabled;
        }

		// 0x060008fd; original named ARM64 HLUnityCore body.
        public void ToggleBreadcrumbHandlers(bool enabled)
        {
            m_breadcrumbHandlersEnabled = enabled;
        }

		// 0x060008fe; original named ARM64 HLUnityCore body.
        public void ToggleCustomKeyHandler()
        {
            m_customKeyHandlersEnabled = !m_customKeyHandlersEnabled;
        }

		// 0x060008ff; original named ARM64 HLUnityCore body.
        public void ToggleCustomKeyHandler(bool enabled)
        {
            m_customKeyHandlersEnabled = enabled;
        }

		// 0x06000900; original named ARM64 HLUnityCore body.
        public void AddDevBreadcrumbHandler(Action<string> action)
        {
            m_devBreadcrumbHandler += action;
        }

		// 0x06000901; original named ARM64 HLUnityCore body.
        public void RemoveDevBreadcrumbHandler(Action<string> action)
        {
            m_devBreadcrumbHandler -= action;
        }

		// 0x06000902; original named ARM64 HLUnityCore body.
        public void DevBreadcrumb(string message)
        {
            if (m_breadcrumbHandlersEnabled) m_devBreadcrumbHandler.Invoke(message);
        }

		// 0x06000903; original named ARM64 HLUnityCore body.
        public void AddBreadcrumbHandler(Action<string> action)
        {
            m_breadcrumbHandler += action;
        }

		// 0x06000904; original named ARM64 HLUnityCore body.
        public void RemoveBreadcrumbHandler(Action<string> action)
        {
            m_breadcrumbHandler -= action;
        }

		// 0x06000905; original named ARM64 HLUnityCore body.
        public void Breadcrumb(string message)
        {
            if (m_breadcrumbHandlersEnabled) m_breadcrumbHandler.Invoke(message);
        }

		// 0x06000906; original named ARM64 HLUnityCore body.
        public void AddCustomKeyStringHandler(Action<string, string> action)
        {
            m_customKeyStringHandler += action;
        }

		// 0x06000907; original named ARM64 HLUnityCore body.
        public void RemoveCustomKeyStringHandler(Action<string, string> action)
        {
            m_customKeyStringHandler -= action;
        }

		// 0x06000908; original named ARM64 HLUnityCore body.
        public void CustomKeyString(string key, string value)
        {
            if (m_customKeyHandlersEnabled) m_customKeyStringHandler.Invoke(key, value);
        }

		// 0x06000909; original named ARM64 HLUnityCore body.
        public void AddCustomKeyIntHandler(Action<string, int> action)
        {
            m_customKeyIntHandler += action;
        }

		// 0x0600090a; original named ARM64 HLUnityCore body.
        public void RemoveCustomKeyIntHandler(Action<string, int> action)
        {
            m_customKeyIntHandler -= action;
        }

		// 0x0600090b; original named ARM64 HLUnityCore body.
        public void CustomKeyInt(string key, int value)
        {
            if (m_customKeyHandlersEnabled) m_customKeyIntHandler.Invoke(key, value);
        }

		// 0x0600090c; original named ARM64 HLUnityCore body.
        public void AddCustomKeyFloatHandler(Action<string, float> action)
        {
            m_customKeyFloatHandler += action;
        }

		// 0x0600090d; original named ARM64 HLUnityCore body.
        public void RemoveCustomKeyFloatHandler(Action<string, float> action)
        {
            m_customKeyFloatHandler -= action;
        }

		// 0x0600090e; original named ARM64 HLUnityCore body.
        public void CustomKeyFloat(string key, float value)
        {
            if (m_customKeyHandlersEnabled) m_customKeyFloatHandler.Invoke(key, value);
        }

		// 0x0600090f; original named ARM64 HLUnityCore body.
        public void AddCustomKeyBoolHandler(Action<string, bool> action)
        {
            m_customKeyBoolHandler += action;
        }

		// 0x06000910; original named ARM64 HLUnityCore body.
        public void RemoveCustomKeyBoolHandler(Action<string, bool> action)
        {
            m_customKeyBoolHandler -= action;
        }

		// 0x06000911; original named ARM64 HLUnityCore body.
        public void CustomKeyBool(string key, bool value)
        {
            if (m_customKeyHandlersEnabled) m_customKeyBoolHandler.Invoke(key, value);
        }
	}
}
