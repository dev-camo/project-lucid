using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020005ff; bodies translated from the 1.10.1 x86-64 binary.
    // The overlay lists every profiler stat that the active quality level's configurations track and opens
    // itself when one leaves its valid range. Its nested data row is 02000600 and the lambda cache is 02000601.
    [RequireComponent(typeof(HLProfilerDataManager))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RuntimeIssueTracker : MonoBehaviour
    {
        // 02000600: one tracked stat, either a profiler data reference or (when Data is null) a frame statistic.
        // Declared before SetVisible, which the compiler-generated lambda names (ordinals 26 and 35) require.
        private class RuntimeIssueData
        {
            public HLProfilerDataReference Data;
            public RuntimeIssueConfiguration Configuration;
        }

        private readonly string ProfilerFrameGroupName = "Frame";
        private readonly Vector2 m_referenceResolution = new Vector2(800f, 480f);
        private const string m_valuesColour = "Orange";
        private const string m_invalidColour = "Red";

        [SerializeField]
        [Tooltip("Assign a UI element that blocks raycasts.")]
        private GameObject m_raycastUIBlocker;

        // 06002029 / 0600202a
        public bool IsVisible { get; private set; }

        private HLProfilerDataManager m_dataHandler;
        private IReadOnlyCollection<RuntimeIssueConfiguration> m_configurations;
        private readonly List<RuntimeIssueData> m_dataList = new List<RuntimeIssueData>();
        private PerformanceProfile.QualityLevel m_qualityLevel;
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<ScalablePerformance> m_scalablePerformanceRef = ProcessManager.GetSystemRef<ScalablePerformance>(null, true);
        private bool m_canOpen;
        private bool m_canOpenWhenInvalid;
        private float m_currentScaleUI;
        private float m_userScaleUI = 1f;

        // 0600202b (1.10.1 RVA 0x5606F0)
        private float UIScaledWidth => Screen.safeArea.width / m_currentScaleUI;

        // 0600202c (1.10.1 RVA 0x560710)
        private float UIScaledHeight => Screen.safeArea.height / m_currentScaleUI;

        // 0600202d (1.10.1 RVA 0x560450): a closed tracker opens only when allowed to, or when forced.
        public void OpenTracker(bool forceOpen)
        {
            if (!m_canOpen && !forceOpen) return;
            enabled = true;
            m_canOpen = true;
            SetVisible(true);
        }

        // 0600202e (1.10.1 RVA 0x560420): closing disarms the invalid-data auto-open until a fully valid scan
        // or a level activation re-arms it.
        public void CloseTracker(bool canReopen)
        {
            SetVisible(false);
            m_canOpen = canReopen;
            m_canOpenWhenInvalid = false;
        }

        // 0600202f (1.10.1 RVA 0x560730): the close and end inputs are listened to only while visible. Their
        // unsubscription is deferred to the next frame.
        private void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;
            IsVisible = visible;
            if (m_raycastUIBlocker) m_raycastUIBlocker.SetActive(IsVisible);
            if (visible)
            {
                ControlMapping.Subscribe(GameInput.IssueTrackerClose, GameInputCloseTrackerDown);
                ControlMapping.Subscribe(GameInput.IssueTrackerEnd, GameInputEndTrackerDown);
            }
            else
            {
                CoroutineUtils.OnNextFrame(() =>
                {
                    ControlMapping.Unsubscribe(GameInput.IssueTrackerClose, GameInputCloseTrackerDown);
                    ControlMapping.Unsubscribe(GameInput.IssueTrackerEnd, GameInputEndTrackerDown);
                });
            }
        }

        // 06002030 (1.10.1 RVA 0x5608F0): the native body inlines CloseTracker(true).
        private void GameInputCloseTrackerDown(float value)
        {
            CloseTracker(true);
        }

        // 06002031 (1.10.1 RVA 0x560910): the native body inlines CloseTracker(false).
        private void GameInputEndTrackerDown(float value)
        {
            CloseTracker(false);
        }

        // 06002032 (1.10.1 RVA 0x560930)
        private void Start()
        {
            m_dataHandler = GetComponent<HLProfilerDataManager>();
            ScalablePerformance scalablePerformance = m_scalablePerformanceRef.Get();
            OnPerformanceProfileUpdated(scalablePerformance.ActiveProfile);
            scalablePerformance.OnProfileUpdated += OnPerformanceProfileUpdated;
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        // 06002033 (1.10.1 RVA 0x560C40)
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            levelManager.InvokeOnLevelActivated(OnLevelActivated);
            levelManager.OnLevelExit += OnLevelExit;
        }

        // 06002034 (1.10.1 RVA 0x560D60): each subscription is removed only while its system is alive.
        private void OnDestroy()
        {
            ScalablePerformance scalablePerformance = m_scalablePerformanceRef.GetSafe();
            if (scalablePerformance != null)
                scalablePerformance.OnProfileUpdated -= OnPerformanceProfileUpdated;
            LevelManager levelManager = m_levelManagerRef.GetSafe();
            if (levelManager != null)
            {
                levelManager.RemoveLevelActivatedAction(OnLevelActivated);
                levelManager.OnLevelExit -= OnLevelExit;
            }
        }

        // 06002035 (1.10.1 RVA 0x560F50): a level may open the tracker, including on invalid data.
        private void OnLevelActivated(LevelManagerLevel levelManagerLevel)
        {
            m_canOpen = true;
            m_canOpenWhenInvalid = true;
        }

        // 06002036 (1.10.1 RVA 0x560F60)
        private void OnLevelExit()
        {
            m_canOpen = false;
        }

        // 06002037 (1.10.1 RVA 0x560A70): the first runtime issue attribute that the profile supports supplies the
        // configurations and quality level. Without a configuration the tracker is disabled; when none matches, the
        // previous configurations remain.
        private void OnPerformanceProfileUpdated(PerformanceProfile performanceProfile)
        {
            RuntimeIssueProfile runtimeIssueProfile = SystemConfiguration.GetConfig<CoreGameConfiguration>().RuntimeIssueProfile;
            if (runtimeIssueProfile == null)
            {
                enabled = false;
                return;
            }
            foreach (RuntimeIssueAttribute attribute in runtimeIssueProfile.m_attributes)
            {
                if (performanceProfile.IsExactlySupported(attribute.PerformanceAttribute))
                {
                    m_configurations = attribute.Configurations;
                    m_qualityLevel = attribute.QualityLevel;
                    break;
                }
            }
            enabled = m_configurations != null;
            if (enabled) UpdateTrackers();
        }

        // 06002038 (1.10.1 RVA 0x560F70): a configuration path is a frame stat, or a group/layout/stat path that
        // resolves one profiler data reference at a time (each level consumes its name and one separator).
        private void UpdateTrackers()
        {
            m_dataList.Clear();
            foreach (RuntimeIssueConfiguration configuration in m_configurations)
            {
                string path = new string(configuration.Path);
                if (GetFrameData(path, out float frameValue))
                {
                    m_dataList.Add(new RuntimeIssueData { Data = null, Configuration = configuration });
                }
                else if (GetTrackerList(m_dataHandler.GroupList, ref path, out HLProfilerGroup group, profilerGroup => profilerGroup.Name) &&
                         GetTrackerList(group.LayoutList, ref path, out HLProfilerLayout layout, profilerLayout => profilerLayout.Name) &&
                         GetTrackerList(layout.DataList, ref path, out HLProfilerDataReference dataReference, profilerReference => profilerReference.StatName))
                {
                    m_dataList.Add(new RuntimeIssueData { Data = dataReference, Configuration = configuration });
                }
            }
            m_dataHandler.StartGroupProfiling(false);
        }

        // 06002039 (1.10.1 RVA 0x5617D0): the path must begin with the frame group name and one separator, then
        // name one of the data manager's frame statistics.
        private bool GetFrameData(string path, out float frameValue)
        {
            frameValue = 0f;
            if (string.Compare(ProfilerFrameGroupName, 0, path, 0, ProfilerFrameGroupName.Length) != 0) return false;
            switch (path.Substring(ProfilerFrameGroupName.Length + 1))
            {
                case "AverageFPS":
                    frameValue = m_dataHandler.AverageFPS;
                    return true;
                case "AverageFrameDeltaTime":
                    frameValue = m_dataHandler.AverageFrameDeltaTime;
                    return true;
                case "MinFrameDeltaTime":
                    frameValue = m_dataHandler.MinFrameDeltaTime;
                    return true;
                case "MaxFrameDeltaTime":
                    frameValue = m_dataHandler.MaxFrameDeltaTime;
                    return true;
                case "CurrentFrameDeltaTime":
                    frameValue = m_dataHandler.CurrentFrameDeltaTime;
                    return true;
                case "CPUFrequency":
                    frameValue = m_dataHandler.CPUFrequency;
                    return true;
                case "GPUFrequency":
                    frameValue = m_dataHandler.GPUFrequency;
                    return true;
            }
            return false;
        }

        // 0600203a (shared generic 1.10.1 RVA 0x9B5C50 / 0x9B5FD0): the first entry whose name prefixes the path is
        // the match; the path then drops that name and the separator that follows, if any characters remain.
        private static bool GetTrackerList<T>(IEnumerable<T> list, ref string path, out T entry, Func<T, string> getString)
        {
            foreach (T item in list)
            {
                string name = getString(item);
                if (string.Compare(name, 0, path, 0, name.Length) != 0) continue;
                if (path.Length > name.Length) path = path.Substring(name.Length + 1);
                entry = item;
                return true;
            }
            entry = default;
            return false;
        }

        // 0600203b (1.10.1 RVA 0x561A50): the first stat outside its valid range opens the tracker when allowed
        // and ends the scan. Only a scan that finds every stat valid allows the next invalid reading to open it.
        private void FixedUpdate()
        {
            foreach (RuntimeIssueData data in m_dataList)
            {
                RuntimeIssueConfiguration configuration = data.Configuration;
                if (data.Data != null)
                {
                    if (configuration.IsValidBytes(data.Data.AverageValue)) continue;
                }
                else if (GetFrameData(configuration.Path, out float frameValue) && configuration.IsValid(frameValue)) continue;
                if (m_canOpenWhenInvalid && m_canOpen) OpenTracker(true);
                return;
            }
            m_canOpenWhenInvalid = true;
        }

        // 0600203c (1.10.1 RVA 0x561C60)
        private void OnGUI()
        {
            if (!IsVisible) return;
            GUIRenderScaleUpdate();
            float overlayWidth = UIScaledWidth;
            GUILayout.BeginVertical(GUI.skin.textArea, GUILayout.Width(overlayWidth));
            GUIHeaderDraw(overlayWidth);
            GUIBodyDraw(overlayWidth);
            GUILayout.EndVertical();
            GUI.matrix = Matrix4x4.identity;
        }

        // 0600203d (1.10.1 RVA 0x561DD0): the UI scale is the mean of the two screen-to-reference ratios times the
        // user scale; the GUI matrix translates to the safe area origin and scales x and y.
        private void GUIRenderScaleUpdate()
        {
            Vector2 resolutionScale = new Vector2(Screen.width, Screen.height) / m_referenceResolution;
            m_currentScaleUI = (resolutionScale.x + resolutionScale.y) * 0.5f * m_userScaleUI;
            GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.safeArea.x, Screen.safeArea.y, 0f), Quaternion.identity,
                new Vector3(m_currentScaleUI, m_currentScaleUI, 1f));
        }

        // 0600203e (1.10.1 RVA 0x561F20): the native body inlines CloseTracker for the Close and End buttons.
        private void GUIHeaderDraw(float overlayWidth)
        {
            GUILayout.BeginHorizontal();
            string deviceType = DeviceTypeUtilities.GetAsString();
            GUILayout.Label(string.Format("<b>Issue Tracker - {0} - {1}</b>", m_qualityLevel, deviceType), GUILayout.Width(overlayWidth * 0.3f));
            m_userScaleUI = GUILabelSliderWithButton("Interface Scale", m_userScaleUI, 0.8f, 1.5f, 0.1f, m_valuesColour);
            if (GUILayout.Button("Close")) CloseTracker(true);
            else if (GUILayout.Button("End")) CloseTracker(false);
            GUILayout.EndHorizontal();
        }

        // 0600203f (1.10.1 RVA 0x5622C0): the width is not read.
        private void GUIBodyDraw(float overlayWidth)
        {
            GUILayout.BeginVertical();
            foreach (RuntimeIssueData data in m_dataList)
            {
                if (data.Data != null)
                {
                    float averageValue = data.Data.AverageValue;
                    string formattedValue = data.Data.ToFormattedString();
                    GUIStatDraw(data.Data.DisplayName, averageValue, formattedValue, data.Configuration, m_invalidColour);
                }
                else if (GetFrameData(data.Configuration.Path, out float frameValue))
                    GUIStatDraw(data.Configuration.Path, frameValue, string.Format("{0:F2} ", frameValue), data.Configuration, m_invalidColour);
            }
            GUILayout.EndVertical();
        }

        // 06002040 (1.10.1 RVA 0x562E40): a formatted stat is a number, a space and its units. A number is
        // reformatted to two decimals; without a space the value is the whole string and the units are empty.
        private static bool TryGetStat(string formattedStr, out string valueStr, out string unitsStr)
        {
            valueStr = formattedStr;
            int unitsIndex = formattedStr.IndexOf(' ');
            if (unitsIndex >= 0)
            {
                unitsStr = formattedStr.Substring(unitsIndex);
                if (!float.TryParse(formattedStr.Substring(0, unitsIndex), out float value)) return false;
                valueStr = string.Format("{0:F2}", value);
                return true;
            }
            unitsStr = "";
            return false;
        }

        // 06002041 (1.10.1 RVA 0x562A90): the configured limit is shown beside the value, in GiB when the stat's
        // units are, and the line turns the invalid colour when the stat is outside its range. Units longer than
        // one character (a leading space and a name) mark byte statistics, which are compared in megabytes.
        private static void GUIStatDraw(string displayName, float statValue, string formattedStr, RuntimeIssueConfiguration configuration, string invalidColour)
        {
            if (!TryGetStat(formattedStr, out string valueStr, out string unitsStr)) return;
            bool isGiB = unitsStr.Substring(1, unitsStr.Length - 1) == "GiB";
            float limit = configuration.Value;
            if (isGiB) limit /= 1024f;
            string text = string.Format("{0}: {1} / {2:F2} {3}", displayName, valueStr, limit, unitsStr);
            bool valid = unitsStr.Length >= 2 ? configuration.IsValidBytes(statValue) : configuration.IsValid(statValue);
            if (!valid) text = "<color=" + invalidColour + ">" + text + "</color>";
            GUILayout.Label(text);
        }

        // 06002042 (1.10.1 RVA 0x5625D0): a labelled slider with a step button either side, clamped to its range.
        private static float GUILabelSliderWithButton(string labelName, float sliderValue, float minValue, float maxValue, float incrementalValue, string valueColour = "white")
        {
            GUILayout.BeginHorizontal();
            GUIRightAlignedLabel(string.Format("{0} [<color={1}>{2:0.#}</color>]", labelName, valueColour, sliderValue));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-")) sliderValue -= incrementalValue;
            sliderValue = GUILayout.HorizontalSlider(sliderValue, minValue, maxValue);
            if (GUILayout.Button("+")) sliderValue += incrementalValue;
            GUILayout.EndHorizontal();
            return Mathf.Clamp(sliderValue, minValue, maxValue);
        }

        // 06002043 (1.10.1 RVA 0x562F70): the skin's shared label style is right aligned for one label and restored.
        private static void GUIRightAlignedLabel(string text)
        {
            TextAnchor previousAlignment = GUI.skin.label.alignment;
            GUI.skin.label.alignment = TextAnchor.UpperRight;
            GUILayout.Label(text);
            GUI.skin.label.alignment = previousAlignment;
        }

        // 06002044 (1.10.1 RVA 0x5630D0): field initialisers precede the MonoBehaviour base call.
        public RuntimeIssueTracker() { }
    }
}
