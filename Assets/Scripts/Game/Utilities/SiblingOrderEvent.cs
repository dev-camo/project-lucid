using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 02000a26; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class SiblingOrderEvent : MonoBehaviour
	{
		[SerializeField]
		private Transform m_targetTransform;
		[SerializeField]
		private bool m_triggerOnFirst;
		[SerializeField]
		private bool m_triggerOnLast;
		[Tooltip("Trigger this event if child is Xth in the order, -1 will be ignored.")]
		[SerializeField]
		private int m_triggerOnSpecificChild = -1;
		[SerializeField]
		private UnityEvent m_event;
		private int m_siblingIndex;
		private int m_childCount;

		// 06003a1d (1.10.1 RVA 0x6B4240)
		private void Awake()
		{
			if (m_targetTransform == null)
				m_targetTransform = transform;
		}

		// 06003a1e (1.10.1 RVA 0x6B42C0)
		private void Reset()
		{
			m_targetTransform = transform;
		}

		// 06003a1f (1.10.1 RVA 0x6B42F0)
		private void OnEnable()
		{
			if (CheckConditions())
				m_event.Invoke();
		}

		// 06003a20 (1.10.1 RVA 0x6B4360): a first-child match returns before the parent's
		// child count is read. The specific-child test is a plain comparison, so -1 never
		// matches a sibling index.
		private bool CheckConditions()
		{
			m_siblingIndex = m_targetTransform.GetSiblingIndex();
			if (m_triggerOnFirst && m_siblingIndex == 0) return true;
			m_childCount = m_targetTransform.parent.childCount;
			if (m_triggerOnLast && m_siblingIndex == m_childCount - 1) return true;
			return m_triggerOnSpecificChild == m_siblingIndex;
		}
	}
}
