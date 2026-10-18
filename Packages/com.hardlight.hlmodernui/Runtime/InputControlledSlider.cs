using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLModernUI.Runtime 02000037; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class InputControlledSlider : MonoBehaviour, ISelectHandler, IEventSystemHandler, IDeselectHandler
	{
		[SerializeField]
		protected GameInput m_inputPositive;

		[SerializeField]
		protected GameInput m_inputNegative;

		[SerializeField]
		protected Slider m_slider;

		[SerializeField]
		private float m_minInputValue;

		[SerializeField]
		private float m_maxInputValue = 1f;

		[SerializeField]
		private InputTrigger m_inputTriggerType = InputTrigger.Down;

		protected bool m_isSelected;

		private Coroutine m_delayedSelected;

		// 06000180 (1.10.1 RVA 0x1A47EA0)
		protected virtual void OnEnable()
		{
			ControlMapping.Subscribe(m_inputPositive, OnPositiveInput, m_inputTriggerType);
			ControlMapping.Subscribe(m_inputNegative, OnNegativeInput, m_inputTriggerType);
		}

		// 06000181 (1.10.1 RVA 0x1A47FA0)
		private void OnDisable()
		{
			ControlMapping.Unsubscribe(m_inputPositive, OnPositiveInput);
			ControlMapping.Unsubscribe(m_inputNegative, OnNegativeInput);
			ClearSelected();
		}

		// 06000182 (1.10.1 RVA 0x1A480C0): selection only takes effect at the end of the frame.
		public void OnSelect(BaseEventData eventData)
		{
			this.SafeStopCoroutine(ref m_delayedSelected);
			m_delayedSelected = StartCoroutine(DelaySelected());
		}

		// 06000183 (1.10.1 RVA 0x1A481D0)
		public void OnDeselect(BaseEventData eventData)
		{
			ClearSelected();
		}

		// 06000184 (1.10.1 RVA 0x1A480A0)
		private void ClearSelected()
		{
			m_isSelected = false;
			this.SafeStopCoroutine(ref m_delayedSelected);
		}

		// 06000185 (1.10.1 RVA 0x1A481F0)
		private void OnPositiveInput(float inputValue)
		{
			TriggerInputEvent(Mathf.Abs(inputValue));
		}

		// 06000186 (1.10.1 RVA 0x1A48330): a zero input still moves the slider up by the minimum,
		// because Mathf.Sign(-0f) is positive.
		private void OnNegativeInput(float inputValue)
		{
			TriggerInputEvent(-Mathf.Abs(inputValue));
		}

		// 06000187 (1.10.1 RVA 0x1A48290)
		private void TriggerInputEvent(float inputValue)
		{
			if (!m_isSelected)
				return;
			float delta = Mathf.Clamp(Mathf.Abs(inputValue), m_minInputValue, m_maxInputValue) * Mathf.Sign(inputValue);
			m_slider.value = m_slider.value + delta;
		}

		// 06000188 (1.10.1 RVA 0x1A48160); iterator <DelaySelected>d__16 0600018a..0600018f.
		private IEnumerator DelaySelected()
		{
			yield return new WaitForEndOfFrame();
			m_isSelected = true;
			m_delayedSelected = null;
		}

		// 06000189 (1.10.1 RVA 0x1A483F0)
		public InputControlledSlider()
		{
		}
	}
}
