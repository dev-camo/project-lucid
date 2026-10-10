using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    // Game.Runtime original 020007ec, methods 06002db8..06002ddb.
    // Whole original rendering/profile controller inferred from both native architectures.
    // Original arithmetic, callback order and faults are retained for preservation.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class VisualQualityManager_SDT : VisualQualityManager
    {
        [SerializeField] private ScalableFeature m_urpRenderFeaturesConfig;
        [SerializeField] private ScriptableRenderFeaturesConfiguration[] m_renderFeatureConfigurations;
        [SerializeField] private ScalableFeature m_renderScaleFeatureConfig;
        [SerializeField] private RenderScaleConfiguration[] m_defaultRenderScaleConfigurations;
        [SerializeField] private UIContainerIdentifier m_debugFPS;
        [Tooltip("Scalable features that are persistant on device: these will not be updated by user preference.")]
        [SerializeField] private ScalableFeature[] m_deviceLockedFeatures;
        [SerializeField] private PerformanceAttribute m_allowGraphicsQualitySwitching;
        [SerializeField, Header("Cutscenes")] private ScalableFeature m_cutsceneScaleFeature;
        [SerializeField] private CutsceneRenderScale[] m_cutsceneRenderScales;
        [Header("Max resolution"), SerializeField] private ScalableFeature m_maxResolutionFeature;
        [SerializeField] private ScreenResolutionSetting[] m_screenResolutionMax;
        [Header("FPS Settings"), SerializeField] private float m_timeToAverageFPSOver = 1f;
        [SerializeField] private float m_timeToleranceForLongFrames = 10f;
        [SerializeField] private float m_toleranceOfFrameTimeMultiplier = 1.5f;

        // 06002db8/9: original compiler-generated field and accessors.
        public float GameplayRenderScale { get; set; } = 0.5f;
        // 06002dba/b: expose the original array and strict accumulated-time comparison.
        public ScalableFeature[] DeviceLockedFeatures => m_deviceLockedFeatures;
        public bool LevelDippedFrames => m_timeOverDesiredFrameTime > m_timeToleranceForLongFrames;
        // 06002dbc/d: original format literal, culture and static storage.
        public static string FramesPerSecond => s_framesPerSecond.ToString("N0");
        public static bool FPSDisplay => s_debugShowFrames;
        // 06002dbe/f: original compiler-generated private setter.
        public Vector2 CutsceneRenderSize { get; private set; }

        public readonly Vector2 DefaultRenderSize = new Vector2(1280f, 720f);
        private const float MenuRenderScale = 1f;
        private const float MinimumGameplayRenderScale = 0.5f;
        private static bool s_debugShowFrames;
        private static float s_framesPerSecond;
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private readonly SystemRef<ScalablePerformance> m_scalablePerformanceRef = ProcessManager.GetSystemRef<ScalablePerformance>();
        private readonly Queue<float> m_frameTimes = new Queue<float>();
        private float m_defaultShadowDistance;
        private bool m_levelActive;
        private float m_desiredFrameTime;
        private float m_currentAverageFrameTime;
        private float m_timeOverDesiredFrameTime;
        private LevelManager m_levelManager;
        private CoreGameConfiguration m_coreGameConfiguration;
        private Vector2Int m_cachedResolution;

        // 06002dc0: keep the original registration and immediate-callback order.
        private void OnEnable()
        {
            DontDestroyOnLoad(gameObject);
            ProcessManager.SubscribeToAction(this, SystemAction.Update, OnUpdate);
            m_coreGameConfiguration = SystemConfiguration.GetConfig<CoreGameConfiguration>();
            DebugManager debugManager = DebugManager.instance;
            if (debugManager != null)
                debugManager.enableRuntimeUI = m_coreGameConfiguration.AllowUnityShaderDebuggingWindow;
            ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(LevelManagerValid);
            m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
        }

        // 06002dc1: no original level-exit unsubscription or tracker reset occurs here.
        private void OnDisable()
        {
            ProcessManager.UnsubscribeFromAction(this, SystemAction.Update);
            if (m_saveManagerRef.IsValid())
                m_saveManagerRef.Get().OnLoadCompleted -= OnSettingsDataLoadCompleted;
        }

        // 06002dc2: original optional null context is unused.
        private void OnUpdate(object context = null) { ValidateResolution(); }

        // 06002dc3: subscribe on the passed manager, rather than fetching another instance.
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            saveManager.OnLoadCompleted += OnSettingsDataLoadCompleted;
        }

        // 06002dc4: retain settings/performance evaluation and callback-induced field rereads.
        private void OnSettingsDataLoadCompleted()
        {
            SaveDataSettings settings = m_saveManagerRef.Get().GetSaveDataSettings();
            ScalablePerformance scalablePerformance = m_scalablePerformanceRef.Get();
            PerformanceProfile profile = string.IsNullOrEmpty(settings.PerformanceProfileName)
                ? null : scalablePerformance.GetPerformanceProfile(settings.PerformanceProfileName);
            SetPerformanceProfile(profile);
            GameplayRenderScale = GetGameplayRenderScale(settings.RenderScale, profile);
        }

        // 06002dc5: NaN follows the original fallback branch; no clamp or sanitization.
        private float GetGameplayRenderScale(float savedRenderScale, PerformanceProfile performanceProfile)
        {
            if (!(savedRenderScale > MinimumGameplayRenderScale))
            {
                if (performanceProfile == null)
                    return MinimumGameplayRenderScale;
                PerformanceAttribute attribute = performanceProfile.GetAttribute(m_renderScaleFeatureConfig);
                RenderScaleConfiguration[] configurations = m_defaultRenderScaleConfigurations;
                if (configurations.Length == 0) return MinimumGameplayRenderScale;
                PerformanceProfile.QualityLevel quality = attribute.Level;
                foreach (RenderScaleConfiguration configuration in configurations)
                    if (configuration.QualityLevel == quality)
                        return configuration.RenderScale;
                return MinimumGameplayRenderScale;
            }
            return savedRenderScale;
        }

        // 06002dc6: base updates precede game features and the unguarded URP shadow read.
        protected override void OnPerformanceProfileUpdated(PerformanceProfile profile)
        {
            base.OnPerformanceProfileUpdated(profile);
            UpdateVisualFeature(profile, m_urpRenderFeaturesConfig, m_renderFeatureConfigurations);
            UpdateVisualFeature(profile, m_cutsceneScaleFeature, m_cutsceneRenderScales);
            UpdateVisualFeature(profile, m_maxResolutionFeature, m_screenResolutionMax);
            m_defaultShadowDistance = (QualitySettings.renderPipeline as UniversalRenderPipelineAsset).shadowDistance;
            int targetFrameRate = Application.targetFrameRate;
            m_desiredFrameTime = m_toleranceOfFrameTimeMultiplier * (1f / targetFrameRate);
            ResetFPSTracker();
        }

        // 06002dc7: queue faults precede the two stores; static FPS remains unchanged.
        private void ResetFPSTracker()
        {
            m_frameTimes.Clear();
            m_currentAverageFrameTime = 0f;
            m_timeOverDesiredFrameTime = 0f;
        }

        // 06002dc8/9: preserve unguarded URP casts and captured restore distance.
        public void OverrideShadowDistance(float shadowDistance)
        {
            (QualitySettings.renderPipeline as UniversalRenderPipelineAsset).shadowDistance = shadowDistance;
        }
        public void RestoreDefaultShadowDistance()
        {
            float shadowDistance = m_defaultShadowDistance;
            (QualitySettings.renderPipeline as UniversalRenderPipelineAsset).shadowDistance = shadowDistance;
        }

        // 06002dca/b: Unity object equality guards only the pipeline, before camera calls.
        public void EnableGameplayRenderScale()
        {
            UniversalRenderPipelineAsset asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            asset.renderScale = GameplayRenderScale;
            GUICameraManager.Instance.ToggleCameras(false);
        }
        public void EnableMenuRenderScale()
        {
            UniversalRenderPipelineAsset asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            asset.renderScale = MenuRenderScale;
            GUICameraManager.Instance.ToggleCameras(true);
        }

        // 06002dcc; original generated 06002dda/b lambda bodies retain fresh field reads.
        private void LevelManagerValid(LevelManager manager)
        {
            m_levelManager = manager;
            m_levelManager.InvokeOnLevelActivated(_ => { m_levelActive = true; ResetFPSTracker(); }, true);
            m_levelManager.OnLevelExit += () => { m_levelActive = false; };
        }

        // 06002dcd: original duration window and harmonic weighting intentionally preserved.
        private void Update()
        {
            if (m_desiredFrameTime == 0f) return;
            if (!m_coreGameConfiguration.CalculateFPSCounter) return;
            m_frameTimes.Enqueue(Time.unscaledDeltaTime);
            while (true)
            {
                float duration = 0f;
                foreach (float frameTime in m_frameTimes) duration += frameTime;
                if (!(duration > m_timeToAverageFPSOver)) break;
                m_frameTimes.Dequeue();
            }
            float weightedTime = 0f;
            float weights = 0f;
            int remaining = m_frameTimes.Count;
            foreach (float frameTime in m_frameTimes)
            {
                weightedTime += frameTime / remaining;
                weights += 1f / remaining;
                remaining--;
                m_currentAverageFrameTime = weightedTime / weights;
            }
            s_framesPerSecond = 1f / m_currentAverageFrameTime;
            if (m_levelActive && m_currentAverageFrameTime > m_desiredFrameTime)
                m_timeOverDesiredFrameTime += Time.unscaledDeltaTime;
        }

        // 06002dce: profile selection callbacks happen before render-scale feature rereads.
        public void SetDefaultPerformanceProfile()
        {
            PerformanceProfile profile = GetDefaultPerformanceProfile();
            SetPerformanceProfile(profile);
            UpdateVisualFeature(profile, m_renderScaleFeatureConfig, m_defaultRenderScaleConfigurations);
        }

        // 06002dcf: the shipping code tests DEFAULT profile support, not the supplied profile.
        // Keep this original quirk. A port policy must be visibly separate from this owner.
        public void SetPerformanceProfile(PerformanceProfile profile)
        {
            if (profile == null || !GetDefaultPerformanceProfile().IsSupported(m_allowGraphicsQualitySwitching))
                profile = GetDefaultPerformanceProfile();
            m_scalablePerformanceRef.Get().SetPerformanceProfile(profile);
        }

        // 06002dd0: original instance default-profile query with fresh attribute field.
        public bool DeviceCanChangeGraphicsQuality()
        {
            return GetDefaultPerformanceProfile().IsSupported(m_allowGraphicsQualitySwitching);
        }

        // 06002dd1: preserve ContainsKey/indexer separation and genuine virtual device match.
        public static PerformanceProfile GetDefaultPerformanceProfile(Dictionary<RuntimePlatform, DevicePerformance> devicePerformanceMatches, PerformanceProfile defaultProfile = null)
        {
            RuntimePlatform platform = Application.platform;
            if (devicePerformanceMatches.ContainsKey(platform))
                return devicePerformanceMatches[platform].GetMatch();
            return defaultProfile;
        }

        // 06002dd2: data lookup precedes performance lookup and dictionary field reads.
        public PerformanceProfile GetDefaultPerformanceProfile()
        {
            DataManager dataManager = ProcessManager.GetSystem<DataManager>();
            ScalablePerformance scalablePerformance = ProcessManager.GetSystemRef<ScalablePerformance>().Get();
            PerformanceProfile defaultProfile = scalablePerformance.ActiveProfile;
            return GetDefaultPerformanceProfile(dataManager.DevicePerformanceMatches, defaultProfile);
        }

        // 06002dd3: direct original auto-property store, no validation.
        public void SetCutsceneRenderSize(Vector2 size) { CutsceneRenderSize = size; }

        // 06002dd4: complete shipping release body is a real empty return.
        public static void AddDebugButton() { }
        // 06002dd5: original literals and current static display state.
        private static string Debug_GetButtonName() { return "FPS Display: " + (s_debugShowFrames ? "ON" : "OFF"); }

        // 06002dd6/7: two system lookups precede the static store and GUI identifier read.
        private static void Debug_ToggleFPSCounter()
        {
            UIManager uiManager = ProcessManager.GetSystem<UIManager>();
            VisualQualityManager_SDT qualityManager = ProcessManager.GetSystem<VisualQualityManager_SDT>();
            bool active = !s_debugShowFrames;
            s_debugShowFrames = active;
            UIContainerIdentifier identifier = qualityManager.m_debugFPS;
            if (active) uiManager.GetOrCreate(identifier);
            else uiManager.Close(identifier);
        }
        public static void Debug_SetFPSCounter(bool active)
        {
            UIManager uiManager = ProcessManager.GetSystem<UIManager>();
            VisualQualityManager_SDT qualityManager = ProcessManager.GetSystem<VisualQualityManager_SDT>();
            s_debugShowFrames = active;
            UIContainerIdentifier identifier = qualityManager.m_debugFPS;
            if (active) uiManager.GetOrCreate(identifier);
            else uiManager.Close(identifier);
        }

        // 06002dd8: original macOS player-only display-change handling, no new port policy.
        private void ValidateResolution()
        {
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            DisplayInfo display = Screen.mainWindowDisplayInfo;
            Vector2Int resolution = new Vector2Int(display.width, display.height);
            if (m_cachedResolution != Vector2Int.zero)
            {
                if (m_cachedResolution == resolution) return;
                if (!m_scalablePerformanceRef.TryGet(out ScalablePerformance scalablePerformance)) return;
                UpdateVisualFeature(scalablePerformance.ActiveProfile, m_maxResolutionFeature, m_screenResolutionMax);
            }
            m_cachedResolution = resolution;
        }

        // 06002dd9: all authored initializers run before the genuine base constructor.
        public VisualQualityManager_SDT() { }
    }
}
