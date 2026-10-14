using System;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0200004e; bodies translated from the 1.10.1 x86-64 binary.
	// Collected data reports "Disposed" names, a "0" string and zero values.
	public class HLProfilerDataReference
	{
		private readonly WeakReference<HLProfilerData> m_data;

		// 060001fb / 060001fc
		public bool IsActive { get; private set; }
		// 060001fd / 060001fe
		public bool IsVisible { get; private set; }

		// 060001ff
		public HLProfilerDataReference(HLProfilerData data)
		{
			IsActive = data.IsActive;
			IsVisible = data.VisibleByDefault;
			m_data = new WeakReference<HLProfilerData>(data);
		}

		// 06000200
		public void ToggleVisible(bool visible)
		{
			IsVisible = visible;
			if (visible)
				Start();
			else
				Stop();
		}

		// 06000201
		public void Start()
		{
			if (IsActive || !IsVisible)
				return;
			if (m_data.TryGetTarget(out HLProfilerData data))
			{
				IsActive = true;
				data.Start();
			}
		}

		// 06000202
		public void Stop()
		{
			if (!IsActive)
				return;
			IsActive = false;
			if (m_data.TryGetTarget(out HLProfilerData data))
				data.Stop();
		}

		// 06000203
		public string StatName => m_data.TryGetTarget(out HLProfilerData data) ? data.StatName : "Disposed";

		// 06000204
		public string DisplayName => m_data.TryGetTarget(out HLProfilerData data) ? data.DisplayName : "Disposed";

		// 06000205
		public string ToFormattedString()
		{
			return m_data.TryGetTarget(out HLProfilerData data) ? data.ToFormattedString() : "0";
		}

		// 06000206
		public long LastValue => m_data.TryGetTarget(out HLProfilerData data) ? data.GetLastValue() : 0;

		// 06000207
		public long MinValue => m_data.TryGetTarget(out HLProfilerData data) ? data.GetMinValue() : 0;

		// 06000208
		public long MaxValue => m_data.TryGetTarget(out HLProfilerData data) ? data.GetMaxValue() : 0;

		// 06000209
		public long AverageValue => m_data.TryGetTarget(out HLProfilerData data) ? data.GetAverageValue() : 0;
	}
}
