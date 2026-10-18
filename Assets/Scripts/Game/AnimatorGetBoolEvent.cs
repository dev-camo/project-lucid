using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 0200009d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorGetBoolEvent : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private string m_boolName;

		[SerializeField]
		private UnityEvent m_trueEvent;

		[SerializeField]
		private UnityEvent m_falseEvent;

		private bool m_boolValue;

		private bool m_wasInState;

		private int m_boolHash;

		// 0600052e (1.10.1 RVA 0x52EFD0)
		private void Awake()
		{
			m_boolHash = Animator.StringToHash(m_boolName);
		}

		// 0600052f (1.10.1 RVA 0x52EFF0): the two edges are separate tests that both
		// read the m_boolValue field, so a listener of the true event that clears it
		// gets the false event in the same frame.
		private void Update()
		{
			m_boolValue = m_animator.GetBool(m_boolHash);
			if (m_boolValue && !m_wasInState)
			{
				m_trueEvent?.Invoke();
				m_wasInState = true;
			}
			if (!m_boolValue && m_wasInState)
			{
				m_falseEvent?.Invoke();
				m_wasInState = false;
			}
		}

		// 06000530 (1.10.1 RVA 0x52F060)
		public AnimatorGetBoolEvent()
		{
		}
	}
}
