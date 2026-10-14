using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using Unity.Profiling;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000049; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public sealed class HLProfilerData
	{
		// Original 0200004a.
		public enum SampleType
		{
			Last,
			Average,
			Min,
			Max
		}

		// 060001b8
		public string DisplayName { get; }
		// 060001b9
		public string StatName { get; }
		// 060001ba / 060001bb
		public bool IsActive { get; private set; }
		// 060001bc
		public bool VisibleByDefault { get; }

		private ProfilerRecorder m_recorder;
		private readonly SampleType m_sampleType;
		private readonly int m_sampleCount;
		private readonly List<HLProfilerDataReference> m_referenceList = new List<HLProfilerDataReference>();

		// 060001bd. The original clamp reverses Min and Max, so every recorder buffer holds
		// SampleBufferSizeMax samples whatever sampleBufferSize is.
		private HLProfilerData(string statName, string displayName, uint sampleBufferSize, SampleType sampleType, bool visibleByDefault)
		{
			m_sampleCount = (int)Math.Max(HLProfilerUtils.SampleBufferSizeMax, Math.Min(1, sampleBufferSize));
			m_sampleType = sampleType;
			StatName = statName;
			DisplayName = string.IsNullOrWhiteSpace(displayName) ? statName : displayName;
			VisibleByDefault = visibleByDefault;
		}

		// 060001be
		public HLProfilerData(HLProfilerDataDefinition data, HLProfilerCategory category)
			: this(data.StatName, data.DisplayName, data.SampleBufferSize, data.SampleType, data.IsShownByDefault)
		{
			m_recorder = new ProfilerRecorder(HLProfilerCategoryConverter.ToProfilerCategory(category), StatName, m_sampleCount);
		}

		// 060001bf. The marker supplies only the recorder; its display name is also the stat name.
		public HLProfilerData(ProfilerMarker marker, string displayName, uint sampleBufferSize, SampleType sampleType, bool visibleByDefault)
			: this(displayName, displayName, sampleBufferSize, sampleType, visibleByDefault)
		{
			m_recorder = new ProfilerRecorder(marker, m_sampleCount);
		}

		// 060001c0
		public HLProfilerDataReference GetReference()
		{
			var reference = new HLProfilerDataReference(this);
			m_referenceList.Add(reference);
			return reference;
		}

		// 060001c1
		public void Start()
		{
			if (IsActive)
				return;
			IsActive = true;
			m_recorder.Start();
		}

		// 060001c2. Recording continues while any reference is still active.
		public void Stop()
		{
			for (int i = 0; i < m_referenceList.Count; i++)
			{
				if (m_referenceList[i].IsActive)
					return;
			}
			IsActive = false;
			m_recorder.Stop();
			m_recorder.Reset();
		}

		// 060001c3
		public void Shutdown()
		{
			IsActive = false;
			m_recorder.Dispose();
		}

		// 060001c4
		public long GetLastValue()
		{
			if (m_recorder.Count == 0)
				return 0;
			return m_recorder.LastValue;
		}

		// 060001c5
		public long GetMinValue()
		{
			return SampleRecorderValue(SampleType.Min);
		}

		// 060001c6
		public long GetMaxValue()
		{
			return SampleRecorderValue(SampleType.Max);
		}

		// 060001c7
		public long GetAverageValue()
		{
			return SampleRecorderValue(SampleType.Average);
		}

		// 060001c8
		public string ToFormattedString()
		{
			return ToFormattedString(m_sampleType);
		}

		// 060001c9. The percent format keeps .NET's multiply-by-100 behaviour of "%".
		public string ToFormattedString(SampleType sampleType)
		{
			if (!m_recorder.Valid || m_recorder.Count == 0)
				return "NA";
			long value = SampleRecorderValue(sampleType);
			switch (m_recorder.UnitType)
			{
				case ProfilerMarkerDataUnit.Undefined:
					return value.ToString();
				case ProfilerMarkerDataUnit.TimeNanoseconds:
					return HLProfilerUtils.FormatMilliseconds(value / 1000000f);
				case ProfilerMarkerDataUnit.Bytes:
					return HLProfilerUtils.FormatBytes(value);
				case ProfilerMarkerDataUnit.Count:
					return HLProfilerUtils.FormatCount(value);
				case ProfilerMarkerDataUnit.Percent:
					return value.ToString(" 0.# %");
				case ProfilerMarkerDataUnit.FrequencyHz:
					return value.ToString(" # Hz");
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		// 060001ca
		private long SampleRecorderValue(SampleType sampleType)
		{
			int count = m_recorder.Count;
			if (count == 0)
				return 0;
			if (sampleType == SampleType.Last)
				return m_recorder.LastValue;
			long min = long.MaxValue;
			long max = long.MinValue;
			long sum = 0;
			for (int i = 0; i < count; i++)
			{
				long value = m_recorder.GetSample(i).Value;
				sum += value;
				min = Math.Min(min, value);
				max = Math.Max(max, value);
			}
			switch (sampleType)
			{
				case SampleType.Average:
					return sum / count;
				case SampleType.Min:
					return min;
				case SampleType.Max:
					return max;
				default:
					throw new ArgumentOutOfRangeException(nameof(sampleType), sampleType, null);
			}
		}
	}
}
