using System;
using System.Collections;
using System.Diagnostics;
using Hardlight.Utils;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.Analytics
{
    // Original Game.Runtime owner 0x02000020; full source from both shipping CPUs.
    // The two yield bodies represent all twelve original iterator methods.
    // Original generated names, fields, flags and exact emission remain held.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AnalyticsSessionManager : ISystem
    {
        public const int SessionQueueCategory = 1;

        public bool IsSessionManagementReady { get; private set; }

        private const int UnknownActiveTime = -1;
        private const int LowestValidSessionNumber = 1;
        private const int InactiveGracePeriodSeconds = 60;
        private const int SessionMaxLengthSeconds = 86400;
        private const int InactiveGracePeriodMilliseconds = 60000;
        private const int MaxInactiveSessionTimeMinutes = 5;
        private const string DebugMenuPath = "Analytics";

        private SystemRef<SaveManager> m_saveManagerRef;
        private string m_sessionID;
        private int m_sessionNumber;
        private long m_sessionSuspendedTimestamp;
        private long m_sessionStartTimestamp;
        private float m_currentSessionLengthSeconds;
        private bool m_createdDebugButtons;

        // 0x0600008e.
        public string SessionID => m_sessionID;

        // 0x0600008f.
        public int SessionNumber => m_sessionNumber;

        // 0x06000090. The shipping constructor only subscribes these actions.
        public AnalyticsSessionManager()
        {
            ProcessManager.SubscribeToAction(this, SystemAction.AppInitialise, AppInitialiseProcess);
            ProcessManager.SubscribeToAction(this, SystemAction.Update, OnUpdate);
            ProcessManager.SubscribeToAction(this, SystemAction.AppShutdown, AppShutdownProcess);
        }

        // 0x06000091.
        private void AppInitialiseProcess(object context)
        {
            m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
            m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
        }

        // 0x06000092.
        private void AppShutdownProcess(object context)
        {
            m_saveManagerRef = null;
        }

        // 0x06000093.
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            LoadFromSaveData(saveManager);
        }

        // 0x06000094 and original <WaitForAnalyticsToLoad>d__27, a3-a8.
        private IEnumerator WaitForAnalyticsToLoad(SaveManager saveManager)
        {
            while (!saveManager.HasSaveDataAnalytics())
                yield return null;

            SetSessionVariablesFromSaveAnalytics(saveManager);
        }

        // 0x06000095.
        public void LoadFromSaveData(SaveManager saveManager)
        {
            if (saveManager.HasSaveDataAnalytics())
                SetSessionVariablesFromSaveAnalytics(saveManager);
            else
                CoroutineUtils.RunCoroutine(WaitForAnalyticsToLoad(saveManager));
        }

        // 0x06000096.
        private void SetSessionVariablesFromSaveAnalytics(SaveManager saveManager)
        {
            SaveDataAnalytics analytics = saveManager.GetSaveDataAnalytics();
            m_sessionID = analytics.SessionID;
            m_sessionNumber = analytics.SessionNumber;
            m_sessionSuspendedTimestamp = analytics.SessionSuspendedTimestamp;
            m_sessionStartTimestamp = analytics.SessionStartTimestamp;
            m_currentSessionLengthSeconds = analytics.CurrentSessionLengthSeconds;
            IsSessionManagementReady = true;
        }

        // 0x06000097.
        private void OnUpdate(object obj)
        {
            if (!IsSessionManagementReady)
                return;

            m_currentSessionLengthSeconds += Time.unscaledDeltaTime;
            if (m_currentSessionLengthSeconds > SessionMaxLengthSeconds)
                CheckForNewSession(true);
        }

        // 0x06000098.
        public void ActivateAnalyticsSessionManagement()
        {
            if (IsSessionManagementReady)
            {
                CheckForNewSession();
                AnalyticsEventCollector.AppStartEvent();
            }
            else
            {
                CoroutineUtils.RunCoroutine(WaitForInitialisedToActivate());
            }
        }

        // 0x06000099 and original <WaitForInitialisedToActivate>d__32, a9-ae.
        private IEnumerator WaitForInitialisedToActivate()
        {
            while (!IsSessionManagementReady)
                yield return null;

            ActivateAnalyticsSessionManagement();
        }

        // 0x0600009a.
        private void CheckForNewSession(bool skipSuspendedTimeCheck = false)
        {
            bool accountChanged = false;
            if (!IsSessionManagementReady)
                return;

            if (m_sessionNumber < LowestValidSessionNumber)
            {
                SaveManager saveManager = m_saveManagerRef.GetSafe();
                if (saveManager != null)
                    saveManager.OnAnalyticsNewInstall();

                StartNewSession();
                return;
            }

            if (!skipSuspendedTimeCheck && m_sessionSuspendedTimestamp <= 0L)
            {
                EndSession(true);
                StartNewSession();
                return;
            }

            if (ProcessManager.GetSystem<App>().Storage.TryGetValue(AppFSMKeys.GameCenterAccountChanged, out accountChanged)
                && accountChanged)
            {
                EndSession(false, true);
                StartNewSession();
                return;
            }

            long now = TimeUtils.ToUnixTimeMs(DateTime.Now);
            long inactiveMilliseconds = unchecked(now - m_sessionSuspendedTimestamp);
            if (skipSuspendedTimeCheck || inactiveMilliseconds > InactiveGracePeriodMilliseconds)
            {
                float inactiveMinutes = (float)inactiveMilliseconds / InactiveGracePeriodMilliseconds;
                if ((!skipSuspendedTimeCheck && inactiveMinutes > MaxInactiveSessionTimeMinutes)
                    || unchecked(now - m_sessionStartTimestamp) >= SessionMaxLengthSeconds * 1000L)
                {
                    EndSession();
                    StartNewSession();
                }
            }
        }

        // 0x0600009b.
        private void StartNewSession()
        {
            m_sessionStartTimestamp = TimeUtils.ToUnixTimeMs(DateTime.Now);
            m_sessionSuspendedTimestamp = m_sessionStartTimestamp;
            m_currentSessionLengthSeconds = 0f;
            m_sessionID = Guid.NewGuid().ToString();
            m_sessionNumber = unchecked(m_sessionNumber + 1);
            Analytics.ResetEventIndex();
            SaveAnalyticsSessionState(true);

            SaveManager saveManager = m_saveManagerRef.Get();
            SaveDataSettings settings = saveManager.GetSaveDataSettings();
            SaveDataAnalytics analytics = saveManager.GetSaveDataAnalytics();
            bool cameraInversion = settings.CameraInvertedControls;
            CameraRecenterHeadingType cameraMode = settings.CameraRecenterHeadingType;
            string installDate = analytics.InstallDate;
            AnalyticsEventCollector.SessionStartEvent(m_sessionNumber, cameraInversion, cameraMode, installDate);
        }

        // 0x0600009c. Both CPUs convert Single to Int32, then widen to Int64.
        // Exceptional Single conversions differ between the original CPU slices;
        // cross-platform native parity for those inputs remains unaccepted.
        private void EndSession(bool unknownSessionTime = false, bool processSessionData = false)
        {
            if (m_sessionNumber < LowestValidSessionNumber)
                return;

            long activeTime = unknownSessionTime ? UnknownActiveTime : unchecked((int)m_currentSessionLengthSeconds);
            AnalyticsEventCollector.SessionEndEvent(m_sessionNumber, activeTime);
            if (processSessionData)
                Analytics.ProcessManualQueue(SessionQueueCategory);
        }

        // 0x0600009d.
        public void ApplicationFocused(bool focused, bool saveImmediately)
        {
            if (!IsSessionManagementReady)
                return;

            if (focused)
            {
                CheckForNewSession();
            }
            else
            {
                m_sessionSuspendedTimestamp = TimeUtils.ToUnixTimeMs(DateTime.Now);
                SaveAnalyticsSessionState(saveImmediately);
                Analytics.ProcessManualQueue(SessionQueueCategory);
            }
        }

        // 0x0600009e.
        private void SaveAnalyticsSessionState(bool saveImmediately)
        {
            SaveManager saveManager = m_saveManagerRef.GetSafe();
            if (saveManager != null)
            {
                saveManager.SaveAnalyticsSessionData(m_sessionID, m_sessionNumber,
                    m_sessionSuspendedTimestamp, m_sessionStartTimestamp,
                    m_currentSessionLengthSeconds, saveImmediately);
            }
        }

        // 0x0600009f. Retail metadata retains this body after menu calls strip.
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetUpDebugMenus()
        {
            if (m_createdDebugButtons)
                return;

            m_createdDebugButtons = true;
            string.IsNullOrEmpty(DebugMenu.CurrentMenuPath);
        }

        // 0x060000a0.
        private string GetSessionIDDebugButtonName()
        {
            return string.Concat("Session ID: ", m_sessionID);
        }

        // 0x060000a1.
        private string GetSessionNumberDebugButtonName()
        {
            return string.Format("Session Number: {0}", m_sessionNumber);
        }

        // 0x060000a2. Retail body only clears the original local menu flag.
        [Conditional("BUILD_DEVELOPMENT")]
        private void RemoveDebugButtons()
        {
            if (m_createdDebugButtons)
                m_createdDebugButtons = false;
        }
    }
}
