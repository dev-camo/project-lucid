using System;
using System.Collections.Generic;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using Unity.Profiling;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000044; bodies translated from the 1.10.1 x86-64 binary.
	// The retail bodies show no use of the two overhead markers beyond tracking them.
	[RequireComponent(typeof(HLProfilerDataManager))]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLProfilerConsole : MonoBehaviour
	{
		// Original 02000045. The names are written into rich-text color tags.
		private enum GUIValueColor
		{
			Cyan,
			Blue,
			Green,
			Yellow,
			Orange,
			Red,
			Magenta,
			White,
			Grey,
			Black,
			Count
		}

		// Original 02000046.
		internal enum GUISkinModifierTarget
		{
			HorizontalSliderFixedHeight,
			HorizontalSliderThumbFixedHeight,
			HorizontalSliderThumbFixedWidth,
			LabelFontSize,
			LabelRichText,
			ButtonFixedHeight,
			ButtonFixedWidth,
			Count
		}

		// Original 02000047.
		private enum GUIStates
		{
			None,
			Window,
			Groups,
			Settings
		}

		// Original 02000048.
		[Flags]
		private enum GUIGeneralStats
		{
			None = 0,
			CPUFramerate = 2,
			GPUFramerate = 4,
			AverageFPS = 8
		}

		private const string ProfilerOverheadGroupName = "HLProfiler Overhead";
		private const string StringBuildingMarkerName = "Strings building";
		private const string UITimeMarkerName = "UI time";

		private readonly Vector2 m_referenceResolution = new Vector2(480f, 800f);
		// x and y size the window; z and w place it, relative to the scaled screen.
		private readonly List<Vector4> m_statsWindowScalePresets = new List<Vector4>
		{
			Vector4.zero,
			new Vector4(0.5f, 0.3f, 1f, 1f),
			new Vector4(1f, 0.3f, 1f, 1f)
		};
		private readonly ProfilerMarker m_fixedUpdateOverheadMarker = new ProfilerMarker("HLProfilerConsole.FixedUpdate");
		private readonly ProfilerMarker m_guiOverheadMarker = new ProfilerMarker("HLProfilerConsole.OnGUI");

		// 0600019a / 0600019b
		public event Action<bool> OnProfilerBlockInputRequestEvent;

		[Header("HL Profiler")]
		[SerializeField]
		[Tooltip("Assign a UI element that blocks raycasts.")]
		private GameObject m_raycastUIBlocker;
		[SerializeField]
		private int m_minFontSize = 12;
		[SerializeField]
		private int m_maxFontSize = 30;
		[SerializeField]
		private GUISkin m_optionalGUISkin;
		[SerializeField]
		private bool m_useCustomPreset;
		[SerializeField]
		[HideIf("m_useCustomPreset", false)]
		private Vector4 m_customPresetRect;

		private HLProfilerDataManager m_dataHandler;
		private HLProfilerGroup m_currentActiveProfilerGroup;
		private readonly Dictionary<GUISkinModifierTarget, float> m_skinDefaultValues = new Dictionary<GUISkinModifierTarget, float>();
		private GUIValueColor m_valuesColor = GUIValueColor.Orange;
		private GUIGeneralStats m_generalStatsFlags = GUIGeneralStats.CPUFramerate | GUIGeneralStats.GPUFramerate | GUIGeneralStats.AverageFPS;
		private readonly StringBuilder m_workingStringBuilder = new StringBuilder(500);
		private readonly string[] m_statsText = new string[2];
		private string m_richTextValueColorString;
		private string m_generalStatsString;
		private bool m_useTwoColumns;
		private bool m_isPaused;
		private bool m_displayStatsWindow = true;
		private bool m_displayHeader = true;
		private bool m_forceBlockInput;
		private bool m_switchTitleToStats;
		private GUIStates m_currentGUIState;
		private Rect m_rectWindow;
		private Vector2 m_windowScale = Vector2.zero;
		private Vector2 m_textScrollPosition;
		private float m_windowPositionX = float.MaxValue;
		private float m_windowPositionY = float.MaxValue;
		private float m_userScaleUI = 1f;
		private float m_currentScaleUI;
		private int m_currentLabelFontSize = 13;
		private int m_currentScalePresetIndex = 1;
		private int m_frameCount;
		private bool m_profilingStarted;

		// 0600019c
		private float UIScaledWidth => Screen.safeArea.width / m_currentScaleUI;
		// 0600019d
		private float UIScaledHeight => Screen.safeArea.height / m_currentScaleUI;

		// 0600019e. The console starts disabled until OpenProfiler.
		private void Awake()
		{
			m_dataHandler = GetComponent<HLProfilerDataManager>();
			m_richTextValueColorString = string.Format("<color={0}>", m_valuesColor);
			for (int i = 0; i < (int)GUISkinModifierTarget.Count; i++)
				m_skinDefaultValues.Add((GUISkinModifierTarget)i, 0f);
			if (m_useCustomPreset)
			{
				m_statsWindowScalePresets.Add(m_customPresetRect);
				m_currentScalePresetIndex = m_statsWindowScalePresets.Count - 1;
			}
			m_windowScale = m_statsWindowScalePresets[m_currentScalePresetIndex];
			enabled = false;
		}

		// 0600019f
		private void OnEnable()
		{
			m_dataHandler.StartTrackingProfilerMarker(m_fixedUpdateOverheadMarker, HLProfilerCategory.Scripts, ProfilerOverheadGroupName, StringBuildingMarkerName, 60);
			m_dataHandler.StartTrackingProfilerMarker(m_guiOverheadMarker, HLProfilerCategory.Scripts, ProfilerOverheadGroupName, UITimeMarkerName, 60);
			m_dataHandler.StartGroupProfiling();
			m_profilingStarted = true;
		}

		// 060001a0
		private void OnDisable()
		{
			if (!m_profilingStarted)
				return;
			m_dataHandler.StopTrackingProfilerMarker(HLProfilerCategory.Scripts, ProfilerOverheadGroupName, StringBuildingMarkerName);
			m_dataHandler.StopTrackingProfilerMarker(HLProfilerCategory.Scripts, ProfilerOverheadGroupName, UITimeMarkerName);
			m_dataHandler.StopGroupProfiling();
			m_profilingStarted = false;
		}

		// 060001a1
		private void FixedUpdate()
		{
			if (m_isPaused)
				return;
			m_frameCount = Time.frameCount;
			GeneralStatsStringWrite();
			GroupsStatsStringWrite();
		}

		// 060001a2. A disabled console returns before the GUI matrix is reset.
		private void OnGUI()
		{
			GUIRenderScaleUpdate();
			if (m_displayHeader)
				GUIHeaderDraw();
			if (!enabled)
				return;
			if (m_currentGUIState != GUIStates.None && m_displayHeader)
				BlockInput(true);
			else
				BlockInput(m_forceBlockInput);
			float width = UIScaledWidth;
			float height = UIScaledHeight;
			if (m_displayStatsWindow && m_currentActiveProfilerGroup == null)
			{
				m_rectWindow = new Rect(m_windowPositionX, m_windowPositionY, m_windowScale.x * width, m_windowScale.y * height);
				GUILayout.BeginArea(m_rectWindow, GUI.skin.textArea);
				Rect rect = GUILayout.Window(0, m_rectWindow, GUIStatsWindowDraw, m_generalStatsString);
				m_windowPositionX = Mathf.Clamp(rect.x, 0f, width - m_rectWindow.width);
				m_windowPositionY = Mathf.Clamp(rect.y, 0f, height - m_rectWindow.height);
				GUILayout.EndArea();
			}
			GUI.matrix = Matrix4x4.identity;
		}

		// 060001a3. Each tab is preceded by a zero-range slider.
		private void GUIHeaderDraw()
		{
			float width = UIScaledWidth;
			HLProfilerUtils.GUIBeginGlobalReskin(m_skinDefaultValues, m_currentLabelFontSize, m_optionalGUISkin);
			GUILayout.BeginVertical(GUI.skin.textArea, GUILayout.Width(width));
			GUILayout.BeginHorizontal();
			GUILayout.Label(m_switchTitleToStats ? m_generalStatsString : "<b>HL Profiler</b>", GUILayout.Width(width * 0.7f));
			if (GUILayout.Button("Close"))
				CloseProfiler();
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUITabsDraw(GUIStates.Window);
			GUITabsDraw(GUIStates.Groups);
			GUITabsDraw(GUIStates.Settings);
			GUILayout.EndHorizontal();
			switch (m_currentGUIState)
			{
				case GUIStates.Window:
					GUILayout.HorizontalSlider(0f, 0f, 0f);
					GUIWindowTabDraw();
					break;
				case GUIStates.Groups:
					GUILayout.HorizontalSlider(0f, 0f, 0f);
					GUIGroupsTabDraw();
					break;
				case GUIStates.Settings:
					GUILayout.HorizontalSlider(0f, 0f, 0f);
					GUISettingsTabDraw();
					break;
			}
			GUILayout.EndVertical();
			HLProfilerUtils.GUIEndGlobalReskin(m_skinDefaultValues);
		}

		// 060001a4. The mode button names the layout it switches to.
		private void GUIWindowTabDraw()
		{
			float buttonWidth = UIScaledWidth * 0.33f;
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			m_displayStatsWindow = GUILayout.Toggle(m_displayStatsWindow, m_displayStatsWindow ? "Hide" : "Show", GUI.skin.button, GUILayout.Width(buttonWidth));
			bool isPaused = GUILayout.Toggle(m_isPaused, m_isPaused ? "Resume" : "Pause", GUI.skin.button, GUILayout.Width(buttonWidth));
			m_useTwoColumns = GUILayout.Toggle(m_useTwoColumns, m_useTwoColumns ? "Mode: []" : "Mode: [][]", GUI.skin.button);
			if (m_isPaused != isPaused)
			{
				m_isPaused = isPaused;
				if (isPaused)
					m_dataHandler.StopGroupProfiling();
				else
					m_dataHandler.StartGroupProfiling();
			}
			GUILayout.EndHorizontal();
			if (GUILayout.Button("Log Snapshot"))
				LogSnapshot(m_frameCount, m_generalStatsString, m_statsText, m_useTwoColumns);
			GUIWindowSizePresetDraw();
			GUILayout.EndVertical();
		}

		// 060001a5. The retail body only builds the snapshot; nothing is logged.
		private static void LogSnapshot(long frameCount, string generalStats, string[] stats, bool columnView)
		{
			var stringBuilder = new StringBuilder(500);
			stringBuilder.AppendLine(string.Format("HLProfiler snapshot - frame {0}", frameCount));
			stringBuilder.Append(generalStats);
			stringBuilder.Append("\n");
			stringBuilder.Append(stats[0]);
			if (columnView && stats.Length > 1)
			{
				stringBuilder.Append("\n");
				stringBuilder.Append(stats[1]);
			}
		}

		// 060001a6. Preset 0 is edited with sliders and cannot be removed. Selecting another
		// preset places the window with a width (or height) multiply, divide and multiply.
		private void GUIWindowSizePresetDraw()
		{
			int previousPresetIndex = m_currentScalePresetIndex;
			GUILayout.BeginHorizontal();
			string presetLabel = string.Format("Window size: {0}[{1}]:{2}</color>", m_richTextValueColorString, m_currentScalePresetIndex, m_statsWindowScalePresets[m_currentScalePresetIndex]);
			if (GUILayout.Button(presetLabel, GUILayout.Width(UIScaledWidth * 0.5f)))
				m_currentScalePresetIndex = (m_currentScalePresetIndex + 1) % m_statsWindowScalePresets.Count;
			if (m_currentScalePresetIndex != 0)
			{
				if (GUILayout.Button("Remove preset"))
				{
					m_statsWindowScalePresets.RemoveAt(m_currentScalePresetIndex);
					m_currentScalePresetIndex = Mathf.Max(m_currentScalePresetIndex - 1, 0);
				}
			}
			else if (GUILayout.Button("Add preset"))
			{
				m_statsWindowScalePresets.Add(new Vector4(m_windowScale.x, m_windowScale.y, m_windowPositionX / UIScaledWidth, m_windowPositionY / UIScaledHeight));
				m_currentScalePresetIndex = m_statsWindowScalePresets.Count - 1;
			}
			GUILayout.EndHorizontal();
			if (m_currentScalePresetIndex == 0)
			{
				HLProfilerUtils.GUIBeginSliderWithButtonsReskin(UIScaledHeight, UIScaledWidth);
				m_windowScale.x = HLProfilerUtils.GUILabelSliderWithButton("Window Width", m_windowScale.x, 0.1f, 1f, 0.1f, m_valuesColor.ToString());
				m_windowScale.y = HLProfilerUtils.GUILabelSliderWithButton("Window Height", m_windowScale.y, 0.1f, 1f, 0.1f, m_valuesColor.ToString());
				HLProfilerUtils.GUIEndSliderWithButtonsReskin(m_skinDefaultValues);
			}
			else if (previousPresetIndex != m_currentScalePresetIndex)
			{
				m_windowScale = m_statsWindowScalePresets[m_currentScalePresetIndex];
				m_windowPositionX = m_statsWindowScalePresets[m_currentScalePresetIndex].z * UIScaledWidth / UIScaledWidth * UIScaledWidth;
				m_windowPositionY = m_statsWindowScalePresets[m_currentScalePresetIndex].w * UIScaledHeight / UIScaledHeight * UIScaledHeight;
			}
		}

		// 060001a7
		private void GUITabsDraw(GUIStates state)
		{
			if (GUILayout.Toggle(m_currentGUIState == state, state.ToString()))
				m_currentGUIState = state;
			else if (m_currentGUIState == state)
				m_currentGUIState = GUIStates.None;
		}

		// 060001a8. In two-column mode each group fills the next column, which is then
		// returned; otherwise every group shares column 0.
		private int GUIStatsGroupsAppendToString(HLProfilerGroup data, StringBuilder stringBuilder, int workingColumn)
		{
			stringBuilder.AppendLine(m_richTextValueColorString + "<b>" + data.Name + "</b></color>");
			if (data.IsVisible)
			{
				data.AppendToString(stringBuilder, true);
				stringBuilder.AppendLine("---\n");
			}
			if (!m_useTwoColumns)
				return 0;
			m_statsText[workingColumn] += stringBuilder.ToString();
			stringBuilder.Clear();
			return workingColumn == 0 ? 1 : 0;
		}

		// 060001a9. Hiding the header also closes the open group details.
		private void GUIGroupsTabDraw()
		{
			GUILayout.BeginVertical();
			if (!m_displayHeader)
				m_currentActiveProfilerGroup = null;
			if (m_currentActiveProfilerGroup != null)
			{
				HLProfilerGroup activeGroup = GUIGroupDrawAndReturnIfActive(m_currentActiveProfilerGroup);
				GUIDrawGroupMoreDetails(HLProfilerUtils.GetAllProfilerData(m_currentActiveProfilerGroup));
				m_currentActiveProfilerGroup = activeGroup;
			}
			else
			{
				GUILayout.BeginHorizontal();
				bool averageFPS = GUILayout.Toggle((m_generalStatsFlags & GUIGeneralStats.AverageFPS) != 0, "FPS");
				bool cpuFramerate = GUILayout.Toggle((m_generalStatsFlags & GUIGeneralStats.CPUFramerate) != 0, "CPU");
				bool gpuFramerate = GUILayout.Toggle((m_generalStatsFlags & GUIGeneralStats.GPUFramerate) != 0, "GPU");
				m_generalStatsFlags = (averageFPS ? GUIGeneralStats.AverageFPS : GUIGeneralStats.None)
					| (cpuFramerate ? GUIGeneralStats.CPUFramerate : GUIGeneralStats.None)
					| (gpuFramerate ? GUIGeneralStats.GPUFramerate : GUIGeneralStats.None);
				GUILayout.EndHorizontal();
				IReadOnlyList<HLProfilerGroup> groupList = m_dataHandler.GroupList;
				for (int i = 0; i < groupList.Count; i++)
				{
					HLProfilerGroup activeGroup = GUIGroupDrawAndReturnIfActive(groupList[i]);
					if (activeGroup != null)
						m_currentActiveProfilerGroup = activeGroup;
				}
			}
			GUILayout.EndVertical();
		}

		// 060001aa
		private HLProfilerGroup GUIGroupDrawAndReturnIfActive(HLProfilerGroup currentGroup)
		{
			HLProfilerGroup activeGroup = null;
			GUILayout.BeginHorizontal();
			currentGroup.ToggleVisible(GUILayout.Toggle(currentGroup.IsVisible, "<b>" + currentGroup.Name + "</b>", GUILayout.Width(UIScaledWidth * 0.5f)));
			if (currentGroup.IsVisible && GUILayout.Toggle(m_currentActiveProfilerGroup == currentGroup, "More..."))
				activeGroup = currentGroup;
			GUILayout.EndHorizontal();
			return activeGroup;
		}

		// 060001ab. Values are listed by stat name rather than display name.
		private static void GUIDrawGroupMoreDetails(IReadOnlyList<HLProfilerDataReference> dataArray)
		{
			GUILayout.BeginVertical(GUI.skin.textArea);
			GUILayout.BeginHorizontal();
			bool displayAll = GUILayout.Button("Display All");
			if (displayAll || GUILayout.Button("Hide All"))
				GUISetGroupVisibility(dataArray, displayAll);
			GUILayout.EndHorizontal();
			for (int i = 0; i < dataArray.Count; i++)
			{
				HLProfilerDataReference data = dataArray[i];
				data.ToggleVisible(GUILayout.Toggle(data.IsVisible, data.StatName));
			}
			GUILayout.EndVertical();
		}

		// 060001ac
		private static void GUISetGroupVisibility(IReadOnlyList<HLProfilerDataReference> array, bool isVisible)
		{
			for (int i = 0; i < array.Count; i++)
				array[i].ToggleVisible(isVisible);
		}

		// 060001ad
		private void GUIStatsWindowDraw(int windowId)
		{
			HLProfilerUtils.GUIBeginGlobalReskin(m_skinDefaultValues, m_currentLabelFontSize, m_optionalGUISkin);
			m_textScrollPosition = GUILayout.BeginScrollView(m_textScrollPosition);
			GUILayout.BeginVertical();
			if (!m_displayHeader && GUILayout.Button("Show header"))
				m_displayHeader = true;
			GUILayout.BeginHorizontal();
			if (m_useTwoColumns)
			{
				GUILayout.Label(m_statsText[0], GUILayout.Width(m_rectWindow.width * 0.45f));
				GUILayout.Label(m_statsText[1]);
			}
			else
			{
				GUILayout.Label(m_statsText[0]);
			}
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			HLProfilerUtils.GUIEndGlobalReskin(m_skinDefaultValues);
			GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
		}

		// 060001ae
		private void GUIRenderScaleUpdate()
		{
			Vector2 screenRatio = new Vector2(Screen.width, Screen.height) / m_referenceResolution;
			m_currentScaleUI = (screenRatio.x + screenRatio.y) * 0.5f * m_userScaleUI;
			GUI.matrix = Matrix4x4.TRS(new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3(m_currentScaleUI, m_currentScaleUI, 1f));
		}

		// 060001af. Color names cycle through every GUIValueColor except Count.
		private void GUISettingsTabDraw()
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			m_switchTitleToStats = GUILayout.Toggle(m_switchTitleToStats, "Switch title for stats", GUI.skin.button, GUILayout.Width(UIScaledWidth * 0.45f));
			if (GUILayout.Button("Hide Header"))
				m_displayHeader = false;
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(string.Format("Values color: {0}{1}</color>", m_richTextValueColorString, m_valuesColor), GUILayout.Width(UIScaledWidth * 0.45f)))
			{
				m_valuesColor = (GUIValueColor)((int)(m_valuesColor + 1) % (int)GUIValueColor.Count);
				m_richTextValueColorString = string.Format("<color={0}>", m_valuesColor);
			}
			m_forceBlockInput = GUILayout.Toggle(m_forceBlockInput, "Force block inputs: " + m_richTextValueColorString + "<b>" + (m_forceBlockInput ? "On" : "Off") + "</b></color>");
			GUILayout.EndHorizontal();
			HLProfilerUtils.GUIBeginSliderWithButtonsReskin(UIScaledHeight, UIScaledWidth);
			m_userScaleUI = HLProfilerUtils.GUILabelSliderWithButton("Interface Scale", m_userScaleUI, 0.8f, 1.5f, 0.1f, m_valuesColor.ToString());
			m_currentLabelFontSize = (int)HLProfilerUtils.GUILabelSliderWithButton("Font Size", m_currentLabelFontSize, m_minFontSize, m_maxFontSize, 1f, m_valuesColor.ToString());
			HLProfilerUtils.GUIEndSliderWithButtonsReskin(m_skinDefaultValues);
			GUILayout.EndVertical();
		}

		// 060001b0
		private void BlockInput(bool value)
		{
			OnProfilerBlockInputRequestEvent?.Invoke(value);
			if (m_raycastUIBlocker)
				m_raycastUIBlocker.SetActive(value);
		}

		// 060001b1. Without a CPU timer frequency, the average frame delta time is shown.
		private void GeneralStatsStringWrite()
		{
			m_workingStringBuilder.Clear();
			if ((m_generalStatsFlags & GUIGeneralStats.AverageFPS) != 0)
				m_workingStringBuilder.Append(string.Format("FPS:<b>{0}{1}</color></b> ", m_richTextValueColorString, (int)m_dataHandler.AverageFPS));
			if ((m_generalStatsFlags & GUIGeneralStats.CPUFramerate) != 0)
			{
				float cpuTime = m_dataHandler.CPUFrequency != 0f ? m_dataHandler.CPUFrequency : m_dataHandler.AverageFrameDeltaTime;
				m_workingStringBuilder.Append(string.Format("CPU:<b>{0}{1:F1}</color></b>ms ", m_richTextValueColorString, cpuTime * 1000.0));
			}
			if ((m_generalStatsFlags & GUIGeneralStats.GPUFramerate) != 0)
			{
				m_workingStringBuilder.Append(m_dataHandler.GPUFrequency == 0f
					? "GPU:<b><color=red>NA</color></b>"
					: string.Format("GPU:<b>{0}{1:F1}</color></b>ms", m_richTextValueColorString, m_dataHandler.GPUFrequency * 1000f));
			}
			m_generalStatsString = m_workingStringBuilder.ToString();
		}

		// 060001b2
		private void GroupsStatsStringWrite()
		{
			m_workingStringBuilder.Clear();
			m_statsText[0] = null;
			m_statsText[1] = null;
			int workingColumn = 0;
			IReadOnlyList<HLProfilerGroup> groupList = m_dataHandler.GroupList;
			for (int i = 0; i < groupList.Count; i++)
			{
				if (groupList[i].IsVisible)
					workingColumn = GUIStatsGroupsAppendToString(groupList[i], m_workingStringBuilder, workingColumn);
			}
			if (!m_useTwoColumns)
				m_statsText[0] = m_workingStringBuilder.ToString();
		}

		// 060001b3
		public void OpenProfiler()
		{
			enabled = true;
			m_displayStatsWindow = true;
		}

		// 060001b4
		public void CloseProfiler()
		{
			BlockInput(false);
			enabled = false;
		}

		// 060001b5
		public void Action_ProfilerShow()
		{
			OpenProfiler();
		}

		// 060001b6
		public void Action_ProfilerHide()
		{
			CloseProfiler();
		}
	}
}
