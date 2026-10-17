using System.Collections.Generic;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using Unity.Profiling;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0200004c; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLProfilerDataManager : MonoBehaviour, ISystem
	{
		// Original 0200004d.
		private struct FrameTimeInfo
		{
			// 060001f3 / 060001f4
			public float AverageMs { get; set; }
			// 060001f5 / 060001f6
			public float MinMs { get; set; }
			// 060001f7 / 060001f8
			public float MaxMs { get; set; }
			// 060001f9 / 060001fa
			public float CurrentMs { get; set; }
		}

		[SerializeField]
		private HLProfilerGroupDefinition[] m_groupDefinitionArray;

		// 060001d1
		public IReadOnlyList<HLProfilerGroup> GroupList => m_groupList;
		// 060001d2 / 060001d3
		public float AverageFPS { get; private set; }
		// 060001d4 / 060001d5
		public float AverageFrameDeltaTime { get; private set; }
		// 060001d6 / 060001d7
		public float MinFrameDeltaTime { get; private set; }
		// 060001d8 / 060001d9
		public float MaxFrameDeltaTime { get; private set; }
		// 060001da / 060001db
		public float CurrentFrameDeltaTime { get; private set; }
		// 060001dc / 060001dd. Despite the name, the console shows this as a CPU time in ms.
		public float CPUFrequency { get; private set; }
		// 060001de / 060001df
		public float GPUFrequency { get; private set; }

		private const int FramerateSampleCount = 60;

		private readonly List<HLProfilerData> m_dataList = new List<HLProfilerData>();
		private readonly Dictionary<string, HLProfilerData> m_customDataList = new Dictionary<string, HLProfilerData>();
		private readonly List<HLProfilerGroup> m_groupList = new List<HLProfilerGroup>();
		private readonly Queue<float> m_framerateSamplesMs = new Queue<float>(FramerateSampleCount + 1);

		// 060001e0
		private void Awake()
		{
			ProcessManager.RegisterSystem(this);
			this.SubscribeToAction(SystemAction.AppShutdown, OnAppShutdown);
			InitialiseProfilerData();
		}

		// 060001e1
		private void OnDestroy()
		{
			ProcessManager.UnregisterSystem(this);
		}

		// 060001e2
		private void OnDisable()
		{
			this.UnsubscribeFromAction(SystemAction.AppShutdown);
			for (int i = 0; i < m_groupList.Count; i++)
				m_groupList[i].StopGroupProfiling();
		}

		// 060001e3. Marker data in m_customDataList is neither shut down nor cleared.
		private void OnAppShutdown(object context = null)
		{
			m_groupList.Clear();
			for (int i = 0; i < m_dataList.Count; i++)
				m_dataList[i].Shutdown();
			m_dataList.Clear();
		}

		// 060001e4
		private void InitialiseProfilerData()
		{
			if (m_groupDefinitionArray == null)
				return;
			foreach (HLProfilerGroupDefinition groupDefinition in m_groupDefinitionArray)
			{
				if (groupDefinition == null || groupDefinition.DataLayout == null)
					continue;
				InitialiseDataGroup(groupDefinition);
			}
		}

		// 060001e5
		private void InitialiseDataGroup(HLProfilerGroupDefinition groupDefinition)
		{
			var group = new HLProfilerGroup(groupDefinition.GroupName, groupDefinition.Category, groupDefinition.ShownByDefault);
			foreach (HLProfilerLayoutDefinition layoutDefinition in groupDefinition.DataLayout)
			{
				var dataList = new List<HLProfilerDataReference>();
				foreach (HLProfilerDataDefinition dataDefinition in layoutDefinition.DataDefinitionList)
				{
					var data = new HLProfilerData(dataDefinition, groupDefinition.Category);
					m_dataList.Add(data);
					dataList.Add(data.GetReference());
				}
				group.AddData(layoutDefinition, dataList);
			}
			m_groupList.Add(group);
		}

		// 060001e6. The timer frequencies are stored in place of CPU and GPU frame times.
		private void Update()
		{
			FrameTimingManager.CaptureFrameTimings();
			CPUFrequency = FrameTimingManager.GetCpuTimerFrequency();
			GPUFrequency = FrameTimingManager.GetGpuTimerFrequency();
			StoreCurrentDeltaTime(Time.deltaTime);
			FrameTimeInfo frameTimeInfo = GetFramerateMetricsMs();
			float averageFPS = 1f / frameTimeInfo.AverageMs;
			AverageFrameDeltaTime = frameTimeInfo.AverageMs;
			MinFrameDeltaTime = frameTimeInfo.MinMs;
			MaxFrameDeltaTime = frameTimeInfo.MaxMs;
			CurrentFrameDeltaTime = frameTimeInfo.CurrentMs;
			AverageFPS = Mathf.Round(averageFPS);
		}

		// 060001e7
		private void StoreCurrentDeltaTime(float deltaTime)
		{
			m_framerateSamplesMs.Enqueue(deltaTime);
			if (m_framerateSamplesMs.Count > FramerateSampleCount)
				m_framerateSamplesMs.Dequeue();
		}

		// 060001e8. The maximum starts at zero and the minimum at the oldest sample.
		private FrameTimeInfo GetFramerateMetricsMs()
		{
			var frameTimeInfo = new FrameTimeInfo();
			if (m_framerateSamplesMs.Count == 0)
				return frameTimeInfo;
			float total = 0f;
			float min = m_framerateSamplesMs.Peek();
			float max = 0f;
			float current = 0f;
			foreach (float sample in m_framerateSamplesMs)
			{
				total += sample;
				min = Mathf.Min(min, sample);
				max = Mathf.Max(max, sample);
				current = sample;
			}
			frameTimeInfo.AverageMs = total / m_framerateSamplesMs.Count;
			frameTimeInfo.MinMs = min;
			frameTimeInfo.MaxMs = max;
			frameTimeInfo.CurrentMs = current;
			return frameTimeInfo;
		}

		// 060001e9. The marker's data is keyed in m_customDataList but never added to m_dataList.
		public void StartTrackingProfilerMarker(ProfilerMarker marker, HLProfilerCategory category, string groupName, string markerDisplayName,
			uint sampleBufferSize = 0, bool isNewGroupActiveByDefault = true, HLProfilerData.SampleType sampleType = HLProfilerData.SampleType.Average)
		{
			var data = new HLProfilerData(marker, markerDisplayName, sampleBufferSize, sampleType, isNewGroupActiveByDefault);
			m_customDataList.Add(groupName + category.ToString() + markerDisplayName, data);
			HLProfilerDataReference reference = data.GetReference();
			for (int i = 0; i < m_groupList.Count; i++)
			{
				HLProfilerGroup group = m_groupList[i];
				if (group.Name == groupName && group.Category == category)
				{
					group.AddData(reference, markerDisplayName);
					return;
				}
			}
			var newGroup = new HLProfilerGroup(groupName, category, isNewGroupActiveByDefault);
			newGroup.AddData(reference, markerDisplayName);
			m_groupList.Add(newGroup);
		}

		// 060001ea
		public void StopTrackingProfilerMarker(HLProfilerCategory category, string groupName, string markerName)
		{
			m_customDataList.Remove(groupName + category.ToString() + markerName);
			for (int i = 0; i < m_groupList.Count; i++)
			{
				HLProfilerGroup group = m_groupList[i];
				if (group.Name != groupName || group.Category != category)
					continue;
				group.RemoveData(markerName);
				return;
			}
		}

		// 060001eb
		public void StartGroupProfiling(bool forceVisible = false)
		{
			for (int i = 0; i < m_groupList.Count; i++)
			{
				if (forceVisible)
					m_groupList[i].ToggleVisible(true);
				m_groupList[i].StartGroupProfiling();
			}
		}

		// 060001ec
		public void StopGroupProfiling(bool forceInvisible = false)
		{
			for (int i = 0; i < m_groupList.Count; i++)
			{
				if (forceInvisible)
					m_groupList[i].ToggleVisible(false);
				m_groupList[i].StopGroupProfiling();
			}
		}

		// 060001ed. The retail body only builds the snapshot; nothing is logged.
		private void LogProfilerSnapshot(bool logOnlyActiveData = true)
		{
			GetProfilerSnapshot(logOnlyActiveData);
		}

		// 060001ee
		public StringBuilder GetProfilerSnapshot(bool logOnlyActiveData = true)
		{
			var stringBuilder = new StringBuilder(500);
			stringBuilder.AppendLine(string.Format("HLProfiler snapshot - frame {0}", Time.frameCount));
			GeneralStatsAppendString(stringBuilder);
			for (int i = 0; i < m_groupList.Count; i++)
			{
				HLProfilerGroup group = m_groupList[i];
				if (!group.IsVisible)
				{
					if (logOnlyActiveData)
						continue;
					stringBuilder.Append(HLProfilerUtils.NotActivePrefix);
				}
				stringBuilder.AppendLine(group.Name);
				group.AppendToString(stringBuilder, logOnlyActiveData);
				stringBuilder.AppendLine("---\n");
			}
			return stringBuilder;
		}

		// 060001ef
		private void GeneralStatsAppendString(StringBuilder stringBuilder)
		{
			stringBuilder.Append(string.Format("FPS:{0} | ", (int)AverageFPS));
			float cpuTime = CPUFrequency != 0f ? CPUFrequency : AverageFrameDeltaTime;
			stringBuilder.Append(string.Format("CPU:{0:F1}ms | ", cpuTime * 1000.0));
			stringBuilder.AppendLine(GPUFrequency == 0f ? "GPU:NA" : string.Format("GPU:{0:F1}ms", GPUFrequency * 1000f));
			stringBuilder.AppendLine("---\n");
		}

		// 060001f0
		[ContextMenu("Log Profiler Active Data Snapshot")]
		public void Action_ProfilerLogActiveDataSnapshot()
		{
			LogProfilerSnapshot(true);
		}

		// 060001f1
		[ContextMenu("Log Profiler All Data Snapshot")]
		public void Action_ProfilerLogDataSnapshot()
		{
			LogProfilerSnapshot(false);
		}
	}
}
