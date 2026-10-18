using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
	// Original HLModernUI.Runtime 02000039; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public sealed class InputControlledSliderSeeker : InputControlledSlider
	{
		[SerializeField]
		private UnityEvent m_onInputDown;

		[SerializeField]
		private UnityEvent<float> m_onInputUp;

		// 06000190 (1.10.1 RVA 0x1A48540). The inherited OnDisable is private and only
		// removes the base callbacks; these four subscriptions are never removed.
		protected override void OnEnable()
		{
			base.OnEnable();
			ControlMapping.Subscribe(m_inputPositive, OnPositiveInputDown, InputTrigger.Down);
			ControlMapping.Subscribe(m_inputPositive, OnPositiveInputUp, InputTrigger.Up);
			ControlMapping.Subscribe(m_inputNegative, OnNegativeInputDown, InputTrigger.Down);
			ControlMapping.Subscribe(m_inputNegative, OnNegativeInputUp, InputTrigger.Up);
		}

		// 06000191 (1.10.1 RVA 0x1A486E0)
		private void OnPositiveInputDown(float value)
		{
			if (m_isSelected)
				m_onInputDown?.Invoke();
		}

		// 06000192 (1.10.1 RVA 0x1A48700)
		private void OnPositiveInputUp(float value)
		{
			if (m_isSelected)
				m_onInputUp?.Invoke(m_slider.value);
		}

		// 06000193 (1.10.1 RVA 0x1A48770)
		private void OnNegativeInputDown(float value)
		{
			if (m_isSelected)
				m_onInputDown?.Invoke();
		}

		// 06000194 (1.10.1 RVA 0x1A48790)
		private void OnNegativeInputUp(float value)
		{
			if (m_isSelected)
				m_onInputUp?.Invoke(m_slider.value);
		}

		// 06000195 (1.10.1 RVA 0x1A48800)
		public InputControlledSliderSeeker()
		{
		}
	}
}
