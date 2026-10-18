using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002a0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class AnimatorModifyFloat : StateMachineBehaviour
	{
		[SerializeField]
		protected string m_sourceFloatParameter;

		[SerializeField]
		protected string m_targetFloatParameter;

		private bool m_valid;

		private int m_sourceFloatParameterHash;

		private int m_targetFloatParameterHash;

		// 06000e79 (1.10.1 RVA 0x726C00): an invalid entry keeps the hashes from
		// the last valid one and skips DoOnEnter.
		public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			m_valid = !string.IsNullOrEmpty(m_sourceFloatParameter) && !string.IsNullOrEmpty(m_targetFloatParameter);
			if (!m_valid)
				return;
			m_sourceFloatParameterHash = Animator.StringToHash(m_sourceFloatParameter);
			m_targetFloatParameterHash = Animator.StringToHash(m_targetFloatParameter);
			DoOnEnter();
		}

		// 06000e7a (1.10.1 RVA 0x726C80)
		public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			if (!m_valid)
				return;
			animator.SetFloat(m_targetFloatParameterHash, ProcessFloat(animator.GetFloat(m_sourceFloatParameterHash)));
		}

		// 06000e7b
		protected abstract float ProcessFloat(float sourceValue);

		// 06000e7c
		protected abstract void DoOnEnter();

		// 06000e7d (1.10.1 RVA 0x726CD0)
		protected AnimatorModifyFloat()
		{
		}
	}
}
