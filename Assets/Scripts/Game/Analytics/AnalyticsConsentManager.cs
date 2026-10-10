using System;
using System.Collections;
using System.Diagnostics;
using Hardlight.Utils;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x0200001b. Original platform and save routes are preserved.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AnalyticsConsentManager : ISystem
    {
        // Original 0x0600004d/4e; the backing field precedes the original constants.
        public bool IsReady { get; private set; }

        private const int SessionCountTriggerValid = 2;
        private const string DebugMenuPath = "Analytics";
        private SystemRef<SaveManager> m_saveManagerRef;
        private SystemRef<AnalyticsSessionManager> m_analyticsSessionManagerRef;
        private SystemRef<GameCenter> m_gameCenterRef;
        private AnalyticsConsentState m_consentState;
        private long m_consentStateLastUpdated;

        // Original 0x0600004f.
        public AnalyticsConsentState ConsentState => m_consentState;

        // Original 0x06000050: initialise subscription precedes shutdown subscription.
        public AnalyticsConsentManager()
        {
            ProcessManager.SubscribeToAction(this, SystemAction.AppInitialise, AppInitialiseProcess);
            ProcessManager.SubscribeToAction(this, SystemAction.AppShutdown, AppShutdownProcess);
        }

        // Original 0x06000051.
        private void AppInitialiseProcess(object context)
        {
            m_analyticsSessionManagerRef = ProcessManager.GetSystemRef<AnalyticsSessionManager>();
            m_gameCenterRef = ProcessManager.GetSystemRef<GameCenter>();
            m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
            m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
        }

        // Original 0x06000052: IsReady and consent fields are retained.
        private void AppShutdownProcess(object context)
        {
            m_analyticsSessionManagerRef = null;
            m_gameCenterRef = null;
            m_saveManagerRef = null;
        }

        // Original 0x06000053.
        private void OnSaveManagerValid(SaveManager saveManager) => LoadFromSaveData(saveManager);

        // Original 0x06000054 and genuine iterator 0x0200001c/0x06000067..6c.
        private IEnumerator WaitForAnalyticsSaveToLoad(SaveManager saveManager)
        {
            while (!saveManager.HasSaveDataAnalytics() || !GameCenterReady())
                yield return null;

            SetConsentVariablesFromSaveAnalytics(saveManager);
            IsReady = true;
        }

        // Original 0x06000055: readiness is not cleared before starting a deferred load.
        public void LoadFromSaveData(SaveManager saveManager)
        {
            if (saveManager.HasSaveDataAnalytics() && GameCenterReady())
            {
                SetConsentVariablesFromSaveAnalytics(saveManager);
                IsReady = true;
            }
            else
                CoroutineUtils.RunCoroutine(WaitForAnalyticsSaveToLoad(saveManager));
        }

        // Original 0x06000056: a null account resets only the consent state.
        private void SetConsentVariablesFromSaveAnalytics(SaveManager saveManager)
        {
            string accountID = m_gameCenterRef.GetSafe()?.GetAccountId();
            if (accountID == null)
                m_consentState = AnalyticsConsentState.None;
            else
            {
                SaveDataAnalyticsConsentState data = saveManager.GetSaveDataAnalytics().GetOrCreateConsentData(accountID);
                m_consentState = data.ConsentState;
                m_consentStateLastUpdated = data.ConsentUpdatedTimestamp;
            }
        }

        // Original 0x06000057.
        public bool CanShowConsentScreenDuringBoot()
        {
            if (!IsTriggerValid())
                return false;
            if (CanTrigger(out bool underage))
                return true;
            if (underage && m_consentState != AnalyticsConsentState.Underage)
            {
                UpdateUnderageStatus();
                SaveConsentData();
            }
            return false;
        }

        // Original 0x06000058: missing and logged-out players count as underage.
        public bool IsPlayerUnderage()
        {
            GameCenter gameCenter = m_gameCenterRef.GetSafe();
            return gameCenter == null || !gameCenter.IsLoggedIn() || gameCenter.IsUnderage();
        }

        // Original 0x06000059.
        private bool GameCenterReady() => m_gameCenterRef.IsValid() && m_gameCenterRef.Get().SystemIsReady;

        // Original 0x0600005a: the settings button skips the session-count requirement.
        public bool CanShowConsentScreenButtonOnSettings() =>
            IsTriggerValid(false) && !IsPlayerUnderage() && m_consentState != AnalyticsConsentState.Underage;

        // Original 0x0600005b.
        private bool CanTrigger(out bool underage)
        {
            GameCenter gameCenter = m_gameCenterRef.GetSafe();
            if (gameCenter == null || !gameCenter.IsLoggedIn())
            {
                underage = true;
                return false;
            }
            underage = gameCenter.IsUnderage();
            return !underage && (m_consentState == AnalyticsConsentState.None || m_consentState == AnalyticsConsentState.Underage);
        }

        // Original 0x0600005c, including the original optional true default.
        private bool IsTriggerValid(bool checkSessionNumber = true) =>
            Analytics.IsSupported && IsReady &&
            (!checkSessionNumber || m_analyticsSessionManagerRef.Get().SessionNumber >= SessionCountTriggerValid) &&
            GameCenterReady();

        // Original 0x0600005d.
        public void Accepted() => TerminateConsentFlow(true);

        // Original 0x0600005e: selection, underage update, then save.
        private void TerminateConsentFlow(bool consent)
        {
            SetConsentOnSelection(consent);
            UpdateUnderageStatus();
            SaveConsentData();
        }

        // Original 0x0600005f.
        public void Declined() => TerminateConsentFlow(false);

        // Original 0x06000060.
        private void SetConsentOnSelection(bool consent)
        {
            m_consentState = consent ? AnalyticsConsentState.Consented : AnalyticsConsentState.Rejected;
            m_consentStateLastUpdated = TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
        }

        // Original 0x06000061.
        private void UpdateUnderageStatus()
        {
            if (IsPlayerUnderage())
            {
                m_consentState = AnalyticsConsentState.Underage;
                m_consentStateLastUpdated = TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
            }
        }

        // Original 0x06000062: the save reference is read before the account guard.
        private void SaveConsentData()
        {
            SaveManager saveManager = m_saveManagerRef.Get();
            GameCenter gameCenter = m_gameCenterRef.GetSafe();
            if (gameCenter != null)
                saveManager.SaveAnalyticsConsentData(gameCenter.GetAccountId(), m_consentState, m_consentStateLastUpdated);
        }

        // Original 0x06000063 retains the current-menu read and string check.
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetUpDebugMenus()
        {
            _ = string.IsNullOrEmpty(DebugMenu.CurrentMenuPath);
        }

        // Original 0x06000064.
        private string GetConsentStateDebugButtonName() => string.Format("Consent State: {0}", m_consentState);

        // Original 0x06000065 deliberately uses FromUnixTime, despite storing milliseconds.
        private string GetConsentStateUpdatedDebugButtonName() =>
            string.Format("Consent State Updated At: {0:yyyy/MM/dd HH:mm:ss}", TimeUtils.FromUnixTime(m_consentStateLastUpdated));

        // Original 0x06000066 is empty.
        [Conditional("BUILD_DEVELOPMENT")]
        private void RemoveDebugButtons() { }
    }
}
