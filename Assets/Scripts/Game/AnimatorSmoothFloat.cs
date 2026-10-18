using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002a5; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimatorSmoothFloat : AnimatorModifyFloat
	{
		[SerializeField]
		[Min(2f)]
		private int m_sampleCount = 10;

		[SerializeField]
		private float m_defaultValue;

		private int m_index;

		private float[] m_samples;

		// 06000e8e (1.10.1 RVA 0x7275D0): the buffer is allocated once and refilled
		// on every entry.
		protected override void DoOnEnter()
		{
			if (m_samples == null)
				m_samples = new float[m_sampleCount];
			Array.Fill(m_samples, m_defaultValue);
			m_index = 0;
		}

		// 06000e8f (1.10.1 RVA 0x727660): a moving average over the buffer, divided
		// by the current m_sampleCount rather than the buffer length.
		protected override float ProcessFloat(float sourceValue)
		{
			m_samples[m_index] = sourceValue;
			m_index = m_index + 1 < m_sampleCount ? m_index + 1 : 0;
			float total = 0f;
			foreach (float sample in m_samples)
				total += sample;
			return total / m_sampleCount;
		}

		// 06000e90 (1.10.1 RVA 0x727730)
		public AnimatorSmoothFloat()
		{
		}
	}
}
