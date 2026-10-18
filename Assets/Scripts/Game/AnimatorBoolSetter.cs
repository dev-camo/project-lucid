using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200029d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorBoolSetter : StateMachineBehaviour
	{
		[SerializeField]
		private bool m_onEnter;

		[SerializeField]
		private bool m_onExit;

		[SerializeField]
		private string m_boolParameter;

		[SerializeField]
		private bool m_boolValue;

		private int m_boolParameterHash;

		// 06000e6f (1.10.1 RVA 0x726680): the hash is refreshed on every entry,
		// whether or not m_onEnter is set.
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (string.IsNullOrEmpty(m_boolParameter))
				return;
			m_boolParameterHash = Animator.StringToHash(m_boolParameter);
			if (m_onEnter)
				animator.SetBool(m_boolParameterHash, m_boolValue);
		}

		// 06000e70 (1.10.1 RVA 0x7266D0): exit uses the hash cached by the last entry.
		public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (!string.IsNullOrEmpty(m_boolParameter) && m_onExit)
				animator.SetBool(m_boolParameterHash, m_boolValue);
		}

		// 06000e71 (1.10.1 RVA 0x726710)
		public AnimatorBoolSetter()
		{
		}
	}
}
