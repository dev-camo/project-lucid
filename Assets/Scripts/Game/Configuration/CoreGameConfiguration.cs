using Hardlight;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000414. This private full declaration retains
    // its real ChallengeManager.IntegrationType dependency; no outer shim.
    [CreateAssetMenu(fileName = "CoreGameConfiguration", menuName = "HardlightProject/Config/Core Game")]
    public class CoreGameConfiguration : SystemConfigurationAsset
    {
        public const string AssetMenu = "HardlightProject/Config/";
        public const string DebugMenu = "DEBUG_MENU";
        [SerializeField] private bool m_useExportedScenes;
        [SerializeField] private bool m_missionIntrosEnabled;
        [SerializeField] private bool m_useFastValidation;
        [SerializeField] private bool m_gameCenter;
        [SerializeField] private ReplayMode m_replayMode = ReplayMode.Off;
        [SerializeField] private bool m_allowUnityShaderDebuggingWindow;
        [SerializeField] private bool m_sendCrashReports;
        [SerializeField] private RuntimeIssueProfile m_runtimeIssueProfile;
        [SerializeField] private bool m_deviceConsoleEnabled;
        [SerializeField] private bool m_logHandlerEnabled;
        [SerializeField] private bool m_calculateFPSCounter;
        [SerializeField] private bool m_tapToSkipEnabled;
        [SerializeField] private bool m_showGameCenterChallenges;
        [SerializeField] private ChallengeManager.IntegrationType m_challengeManagerIntegrationType;
        [SerializeField] private HashedGroupConfiguration m_hashedComponentGroupDefaults;

        // 0x060017ef..0x060017fc: direct native field reads, except the
        // ShowGameCenterChallenges getter's original GameCenter short circuit.
        public bool UseExportedScenes => m_useExportedScenes;
        public bool MissionIntrosEnabled => m_missionIntrosEnabled;
        public bool FastValidation => m_useFastValidation;
        public bool GameCenter => m_gameCenter;
        public ReplayMode ReplayMode => m_replayMode;
        public bool AllowUnityShaderDebuggingWindow => m_allowUnityShaderDebuggingWindow;
        public bool SendCrashReports => m_sendCrashReports;
        public RuntimeIssueProfile RuntimeIssueProfile => m_runtimeIssueProfile;
        public bool DeviceConsoleEnabled => m_deviceConsoleEnabled;
        public bool LogHandlerEnabled => m_logHandlerEnabled;
        public bool CalculateFPSCounter => m_calculateFPSCounter;
        public bool TapToSkipEnabled => m_tapToSkipEnabled;
        public bool ShowGameCenterChallenges => m_gameCenter && m_showGameCenterChallenges;
        public ChallengeManager.IntegrationType ChallengeManagerIntegrationType => m_challengeManagerIntegrationType;
        // 0x060017fd; ARM64 0x76ff1c.
        public HashedGroupConfiguration HashedComponentGroupDefaults => m_hashedComponentGroupDefaults;

        // 0x060017fe; ARM64 0x76ff24 is a genuine single RET.
        public override void Validate() { }
        // 0x060017ff; ARM64 0x76ff28 writes exact ReplayMode.Off
        // (0x42e4e036) before calling the real SystemConfigurationAsset base.
        public CoreGameConfiguration() { }
    }
}
