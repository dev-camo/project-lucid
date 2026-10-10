using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020000b5, shipping release 1.10.1.
    // This is the original Apple account flow. Offline routing belongs in a
    // separate adapter and must leave this preservation source available.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenter : ISystem
    {
        public bool SystemIsReady { get; private set; }
#if !PROJECT_LUCID_ORIGINAL_GAMECENTER
        private readonly ProjectLucid.Offline.LocalGameCenterLifecycle m_offlineLifecycle = new ProjectLucid.Offline.LocalGameCenterLifecycle();
        private ProjectLucid.Offline.LocalGameCenterProviderLease m_offlineProviderLease;
#endif
        private string m_lastKnownAccountId;
        private string m_lastKnownAccountNickname;
        private bool m_lastKnownLoggedOut;
        private SystemRef m_gcLeaderboard;
        private static bool s_debugIsLoggedIn;
        private static bool s_debugIsUnderage;
        // 0x06000613: original initializer stores these three literal values
        // in this order. All other static state retains zero initialization.
        private static readonly string[] s_debugAccountIDArray =
            { "12345678", "ABCDEFGH", "98765432" };
        private static int s_debugAccountIDIndex;
        private static bool s_debugReady;
        private const string DebugMenuPath = "GameCenter";
        private Achievements m_achievements;
        private const string ApplicationGameObjectName = "Application";

        // 0x060005f4..0x060005f8: true original extern declarations. Both
        // native architectures call linked _HL* symbols directly, with no
        // dynamic import resolver. __Internal is an inferred reconstruction
        // of that static-link contract; the shipping metadata does not retain
        // the original managed import-module spelling. Native execution and
        // exact module-string parity remain unverified.
        [DllImport("__Internal")]
        private static extern void HLLoginInitialise(string gameObjectName);
        [DllImport("__Internal")]
        private static extern bool HLIsUnderage();
        [DllImport("__Internal")]
        private static extern string HLGetAccountId();
        [DllImport("__Internal")]
        private static extern string HLGetAccountNickname();
        [DllImport("__Internal")]
        private static extern void HLReportAchievement(string id, float progress);

        // 0x060005fb; ARM64 0x50f4a8, x86_64 0x539190.
        // Register the wrapper before constructing the original native local
        // player. Its default false factory route and faults are preserved.
        // Action ordinal four is AppShutdown, not ordinary Shutdown.
        public GameCenter()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            ProcessManager.RegisterSystem(new GameCenterLocalPlayer(false), null, false, false);
            ProcessManager.SubscribeToAction(this, SystemAction.Initialise, new Action<object>(OnInitialise));
            ProcessManager.SubscribeToAction(this, SystemAction.AppShutdown, new Action<object>(OnShutdown));
            ProcessManager.SubscribeToAction(this, SystemAction.Update, new Action<object>(OnUpdate));
        }

        // 0x060005fc: only the achievements reference is nullable. A shutdown
        // exception stops unregistration; readiness and fields are not reset.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void OnShutdown(object context = null)
        {
            if (m_achievements != null) m_achievements.Shutdown();
            ProcessManager.UnregisterSystem(this);
        }
#else
        private void OnShutdown(object context = null)
        {
            m_offlineLifecycle.Stop();
            var errors = new System.Collections.Generic.List<Exception>();
            CleanupOfflineProviders(errors);
            try { ProjectLucid.Offline.LocalPersonalRecords.Shutdown(); }
            catch (Exception error) { errors.Add(error); }
            SystemIsReady = false;
            try
            {
                string name = ProcessManager.GetDefaultName<GameCenter>();
                SystemRef reference = ProcessManager.GetSystemRef(name, false);
                if (reference != null && ReferenceEquals(reference.GetSafe(), this))
                    ProcessManager.UnregisterSystem(name);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Owned local GameCenter shutdown failed.", errors);
        }
#endif

        // 0x060005fd: release native body directly enters NativeInitialise.
        private void OnInitialise(object context = null) => NativeInitialise();

        // 0x060005fe: retain native initialization before authentication.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void NativeInitialise()
        {
            HLLoginInitialise(ApplicationGameObjectName);
            NativeLoginRequest();
        }
#else
        private void NativeInitialise()
        {
            OnLoginComplete(false);
        }
#endif

        // 0x060005ff/0x06000600: debug switches do not replace the original
        // release's Social authentication or native underage queries.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public bool IsLoggedIn() => Social.localUser.authenticated;
#else
        public bool IsLoggedIn()
        {
            return false;
        }
#endif
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public bool IsUnderage() => HLIsUnderage();
#else
        public bool IsUnderage()
        {
            return false;
        }
#endif

        // 0x06000601: allocate the real callback after fetching localUser.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void NativeLoginRequest() => Social.localUser.Authenticate(new Action<bool>(OnLoginComplete));
#else
        private void NativeLoginRequest()
        {
            OnLoginComplete(false);
        }
#endif

        // 0x06000602: success is genuinely ignored. Preserve the query and
        // publication order, including initialization on authentication failure.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void OnLoginComplete(bool success)
        {
            m_lastKnownAccountId = GetAccountId();
            m_lastKnownLoggedOut = !IsLoggedIn();
            m_lastKnownAccountNickname = GetAccountNickname();
            App app = ProcessManager.GetSystem<App>(null, true);
            app.RegisterAndInitialiseSystem<LeaderboardManager>();
            m_achievements = new Achievements();
            m_achievements.Initialise(app);
            SystemIsReady = true;
        }
#else
        public void OnLoginComplete(bool success)
        {
            if (SystemIsReady) return;
            m_offlineLifecycle.Begin(() =>
            {
                m_lastKnownAccountId = ProjectLucid.Offline.LocalProfileIdentity.Identifier;
                m_lastKnownLoggedOut = true;
                m_lastKnownAccountNickname = ProjectLucid.Offline.LocalProfileIdentity.DisplayName;
                App app = ProcessManager.GetSystem<App>(null, true);
                m_offlineProviderLease = new ProjectLucid.Offline.LocalGameCenterProviderLease();
                try
                {
                    m_offlineProviderLease.Start();
                    m_achievements = new Achievements();
                    m_achievements.Initialise(app);
                    // Local account, record transport and achievement catalog are initialized.
                    // Deferred original gameplay/save tracker binding is a separate lifecycle.
                    SystemIsReady = true;
                }
                catch (Exception original)
                {
                    var errors = new System.Collections.Generic.List<Exception>();
                    CleanupOfflineProviders(errors);
                    SystemIsReady = false;
                    if (errors.Count != 0)
                    {
                        errors.Insert(0, original);
                        throw new AggregateException("Local GameCenter initialization and owned cleanup failed.", errors);
                    }
                    throw;
                }
            });
        }
#endif

        // 0x06000603..0x06000606: original native strings/UI paths.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public string GetAccountId() => HLGetAccountId();
#else
        public string GetAccountId()
        {
            return ProjectLucid.Offline.LocalProfileIdentity.Identifier;
        }
#endif
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public string GetAccountNickname() => HLGetAccountNickname();
#else
        public string GetAccountNickname()
        {
            return ProjectLucid.Offline.LocalProfileIdentity.DisplayName;
        }
#endif
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void ShowAchievementPage() => Social.ShowAchievementsUI();
#else
        public void ShowAchievementPage()
        {
            ProjectLucid.Offline.LocalProfilePresentation.ShowAchievements();
        }
#endif
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void ShowLeaderboardPage() => Social.ShowLeaderboardUI();
#else
        public void ShowLeaderboardPage()
        {
            ProjectLucid.Offline.LocalProfilePresentation.ShowRecords(null);
        }
#endif

        // 0x06000607: Conditional attribute belongs to the original method;
        // the callback can run synchronously for an already valid SaveManager.
        [Conditional("BUILD_DEVELOPMENT")]
        private void InitialiseDebugMenu() => ProcessManager.GetSystemRef<SaveManager>(null, true)
            .InvokeOnValid(new Action<SaveManager>(OnSaveManagerValid));

        // 0x06000608: settings-ready path is immediate; otherwise use the
        // original coroutine host. A null manager is not tolerated.
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            if (saveManager.HasSaveDataSettings()) SetUpDebugMenus(saveManager);
            else CoroutineUtils.RunCoroutine(WaitForSettingsToLoad(saveManager));
        }

        // 0x06000609 and original natural owner 0x020000b6 / six methods
        // 0x06000614..0x06000619. Yield null until settings exist, then invoke
        // the original setup exactly once. Dispose has no cleanup or finally.
        private IEnumerator WaitForSettingsToLoad(SaveManager saveManager)
        {
            while (!saveManager.HasSaveDataSettings()) yield return null;
            SetUpDebugMenus(saveManager);
        }

        // 0x0600060a: read each Debug property separately and publish ready
        // before fetching the native account. Retail stripped its debug-menu
        // additions but retains the CurrentMenuPath getter/empty-string query.
        private void SetUpDebugMenus(SaveManager saveManager)
        {
            SaveDataSettings settings = saveManager.GetSaveDataSettings();
            s_debugIsLoggedIn = settings.Debug.GameCenterDebugIsLoggedIn;
            s_debugIsUnderage = settings.Debug.GameCenterDebugIsUnderage;
            s_debugAccountIDIndex = settings.Debug.GameCenterDebugAccountIDIndex;
            s_debugReady = true;
            m_lastKnownAccountId = GetAccountId();
            AnalyticsConsentManager consentManager = ProcessManager.GetSystemSafe<AnalyticsConsentManager>(null, true);
            if (consentManager != null) consentManager.LoadFromSaveData(saveManager);
            string.IsNullOrEmpty(DebugMenu.CurrentMenuPath);
        }

        // 0x0600060b..0x0600060d: exact format/prefix literals and raw array
        // indexing. The native release disabled array bounds checks.
        private string GetLoggedInDebugButtonName() => string.Format("Logged In: {0}", s_debugIsLoggedIn);
        private string GetUnderageDebugButtonName() => string.Format("Underage: {0}", s_debugIsUnderage);
        private string GetUnderageDebugAccountIDButtonName() => "Account ID: " + s_debugAccountIDArray[s_debugAccountIDIndex];

        // 0x0600060e: mutate shared state before resolving the save system.
        // A missing manager still leaves the toggled state in place.
        private void ToggleDebugIsLoggedIn()
        {
            if (!s_debugReady) return;
            s_debugIsLoggedIn = !s_debugIsLoggedIn;
            SaveManager saveManager = ProcessManager.GetSystemSafe<SaveManager>(null, true);
            if (saveManager != null)
            {
                saveManager.GetSaveDataSettings().Debug.GameCenterDebugIsLoggedIn = s_debugIsLoggedIn;
                saveManager.RequestSaveSettings();
            }
        }

        // 0x0600060f: same original sequencing for the underage debug switch.
        private void ToggleDebugIsUnderage()
        {
            if (!s_debugReady) return;
            s_debugIsUnderage = !s_debugIsUnderage;
            SaveManager saveManager = ProcessManager.GetSystemSafe<SaveManager>(null, true);
            if (saveManager != null)
            {
                saveManager.GetSaveDataSettings().Debug.GameCenterDebugIsUnderage = s_debugIsUnderage;
                saveManager.RequestSaveSettings();
            }
        }

        // 0x06000610: increment wraps as an Int32, then uses signed remainder.
        // No sanitization of a saved negative/out-of-range index is added.
        private void CycleDebugAccountID()
        {
            if (!s_debugReady) return;
            s_debugAccountIDIndex = unchecked(s_debugAccountIDIndex + 1) % s_debugAccountIDArray.Length;
            SaveManager saveManager = ProcessManager.GetSystemSafe<SaveManager>(null, true);
            if (saveManager != null)
            {
                saveManager.GetSaveDataSettings().Debug.GameCenterDebugAccountIDIndex = s_debugAccountIDIndex;
                saveManager.RequestSaveSettings();
            }
        }

        // 0x06000611: original Update action polls the account every dispatch.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void OnUpdate(object context = null) => CheckForGameCenterAccountIDChange();
#else
        private void OnUpdate(object context = null)
        {
            m_achievements?.DrainOfflineSaveListenerRemoval();
            m_offlineLifecycle.Poll();
        }
#endif

        // 0x06000612; ARM64 0x510740, x86_64 0x53a2e0. These three guards
        // really are required together: changed ID, a nonblank previous ID or
        // recorded logout, and changed nickname. A same-nickname account swap
        // is ignored. Keep the repeated ID query and stale nickname cache.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void CheckForGameCenterAccountIDChange()
        {
            if (m_lastKnownAccountId == GetAccountId()) return;
            if (string.IsNullOrEmpty(m_lastKnownAccountId) && !m_lastKnownLoggedOut) return;
            if (GetAccountNickname() == m_lastKnownAccountNickname) return;
            m_lastKnownAccountId = GetAccountId();
            m_lastKnownLoggedOut = !IsLoggedIn();
            App app = ProcessManager.GetSystemSafe<App>(null, true);
            // The native method dereferences a missing app/storage and the
            // achievements field; adding null recovery would change behavior.
            app.Storage.SetValue<bool>(AppFSMKeys.GameCenterAccountChanged, true);
            m_achievements.Shutdown();
            m_achievements.Initialise(app);
            SaveManager saveManager = ProcessManager.GetSystemSafe<SaveManager>(null, true);
            AnalyticsConsentManager consentManager = ProcessManager.GetSystemSafe<AnalyticsConsentManager>(null, true);
            if (saveManager != null && consentManager != null) consentManager.LoadFromSaveData(saveManager);
        }
#else
        private void CheckForGameCenterAccountIDChange()
        {
            m_offlineLifecycle.Poll();
        }

        private void CleanupOfflineProviders(System.Collections.Generic.List<Exception> errors)
        {
            try { m_achievements?.Shutdown(); }
            catch (Exception error) { errors.Add(error); }
            try { m_achievements?.DrainOfflineSaveListenerRemoval(); }
            catch (Exception error) { errors.Add(error); }
            try { m_offlineProviderLease?.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            m_achievements = null;
            m_offlineProviderLease = null;
        }
#endif
    }
}
