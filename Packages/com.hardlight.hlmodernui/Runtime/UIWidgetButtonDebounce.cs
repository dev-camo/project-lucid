using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLModernUI.Runtime 02000040; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public sealed class UIWidgetButtonDebounce : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
	{
		[SerializeField]
		private Button m_uiButton;

		// Despite the tooltip, the original builds the delay from this value unconditionally.
		[SerializeField]
		[Tooltip("Timeout in seconds to debounce. Pass 0 and no timeout will be used.")]
		private float m_timeoutInSeconds = 0.5f;

		private bool m_cachedButtonState;

		private Coroutine m_debounceRoutine;

		private WaitForSeconds m_debounceDelay;

		// 060001af (1.10.1 RVA 0x1A491F0)
		private void OnEnable()
		{
			m_debounceDelay = new WaitForSeconds(m_timeoutInSeconds);
		}

		// 060001b0 (1.10.1 RVA 0x1A49260)
		public void OnPointerClick(PointerEventData eventData)
		{
			if (m_debounceRoutine == null && gameObject.activeInHierarchy && m_uiButton.interactable)
				m_debounceRoutine = StartCoroutine(DebounceRoutine());
		}

		// 060001b1 (1.10.1 RVA 0x1A49330); iterator <DebounceRoutine>d__7 060001b5..060001ba.
		private IEnumerator DebounceRoutine()
		{
			m_cachedButtonState = m_uiButton.interactable;
			m_uiButton.interactable = false;
			yield return m_debounceDelay;
			m_uiButton.interactable = m_cachedButtonState;
			m_debounceRoutine = null;
		}

		// 060001b2 (1.10.1 RVA 0x1A493C0): the stopped handle is kept, so later clicks stay
		// ignored until the component is re-created.
		private void ClearActiveDebounce()
		{
			if (m_debounceRoutine != null)
			{
				StopCoroutine(m_debounceRoutine);
				m_uiButton.interactable = m_cachedButtonState;
			}
		}

		// 060001b3 (1.10.1 RVA 0x1A49400)
		private void OnDisable()
		{
			ClearActiveDebounce();
		}

		// 060001b4 (1.10.1 RVA 0x1A49440)
		public UIWidgetButtonDebounce()
		{
		}
	}
}
