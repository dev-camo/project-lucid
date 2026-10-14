using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0200004b; bodies translated from the 1.10.1 x86-64 binary.
	[Serializable]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLProfilerDataDefinition
	{
		[Tooltip("See profiler stats names in Unity documentation.")]
		[SerializeField]
		private HLProfilerMarker m_marker;
		[SerializeField]
		[Tooltip("Friendly name for the stat. If empty the stat name is used instead.")]
		private string m_displayName;
		[Tooltip("Size of the sample buffer. Value of 0 (default) or 1 does no buffering. Increase this value to get avg/min/max values over a number of frames.")]
		[SerializeField]
		[Range(0, 60)]
		private uint m_sampleBufferSize;
		[SerializeField]
		[Tooltip("Whether the HLProfiler console shows this value by default, or if it needs to be toggled on.")]
		private bool m_isShownByDefault;
		[SerializeField]
		[Tooltip("The type of this stat's sample. Controls whether to display the minimum, maximum, average or last value for the buffer period. If last is used then no buffering is used.")]
		private HLProfilerData.SampleType m_sampleType;

		// 060001cb
		public string StatName => HLProfilerMarkerConverter.GetName(m_marker);
		// 060001cc
		public string DisplayName => m_displayName;
		// 060001cd
		public uint SampleBufferSize => m_sampleBufferSize;
		// 060001ce
		public bool IsShownByDefault => m_isShownByDefault;
		// 060001cf
		public HLProfilerData.SampleType SampleType => m_sampleType;

		// 060001d0
		public HLProfilerDataDefinition(HLProfilerMarker marker, string displayName = null, uint sampleBufferSize = 0,
			bool isShownByDefault = true, HLProfilerData.SampleType sampleType = HLProfilerData.SampleType.Average)
		{
			m_marker = marker;
			m_displayName = displayName;
			m_sampleBufferSize = sampleBufferSize;
			m_isShownByDefault = isShownByDefault;
			m_sampleType = sampleType;
		}
	}
}
