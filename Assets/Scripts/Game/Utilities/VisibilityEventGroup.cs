using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000a41; bodies translated from the 1.10.1 x86-64 binary.
	// Raises one event when the first renderer becomes visible and another when none is left.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class VisibilityEventGroup : MonoBehaviour
	{
		[SerializeField]
		private EventOnVisibility[] m_visibleEvents;
		[SerializeField]
		private UnityEvent m_onAnyVisibleEvent;
		[SerializeField]
		private UnityEvent m_onAllInvisibleEvent;
		private bool m_anyWasVisible;

		// 06003aa2 (1.10.1 RVA 0x6B99D0)
		private void OnEnable()
		{
			foreach (EventOnVisibility visibleEvent in m_visibleEvents)
			{
				visibleEvent.OnVisibleEvent.AddListener(OnVisible);
				visibleEvent.OnInvisibleEvent.AddListener(OnHide);
			}
		}

		// 06003aa3 (1.10.1 RVA 0x6B9AD0)
		private void OnDisable()
		{
			foreach (EventOnVisibility visibleEvent in m_visibleEvents)
			{
				visibleEvent.OnVisibleEvent.RemoveListener(OnVisible);
				visibleEvent.OnInvisibleEvent.RemoveListener(OnHide);
			}
		}

		// 06003aa4 (1.10.1 RVA 0x6B9BD0): the event runs only for the first visible renderer.
		private void OnVisible()
		{
			if (m_anyWasVisible)
				return;
			m_anyWasVisible = true;
			m_onAnyVisibleEvent.Invoke();
		}

		// 06003aa5 (1.10.1 RVA 0x6B9BF0): every renderer is polled, and the event runs for each
		// hide that leaves none visible, even when nothing was reported visible before.
		private void OnHide()
		{
			bool anyVisible = false;
			foreach (EventOnVisibility visibleEvent in m_visibleEvents)
				anyVisible |= visibleEvent.IsVisible;
			if (!anyVisible)
			{
				m_onAllInvisibleEvent.Invoke();
				m_anyWasVisible = false;
			}
		}

		// 06003aa6 (1.10.1 RVA 0x6B9CC0)
		[ContextMenu("Get EventOnVisibility in children")]
		private void GetRenderersInChildren()
		{
			m_visibleEvents = GetComponentsInChildren<EventOnVisibility>();
		}
	}
}
