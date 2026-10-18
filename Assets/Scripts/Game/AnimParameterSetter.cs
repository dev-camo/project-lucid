using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000a7; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimParameterSetter : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private AnimationParameterWrapper m_parameterWrapper;

		// 06000551 (1.10.1 RVA 0x52FC10)
		public void SetBool(bool value)
		{
			if (m_parameterWrapper.TryGetAnimationHash(out int hash))
				m_animator.SetBool(hash, value);
		}

		// 06000552 (1.10.1 RVA 0x52FC60)
		public void SetFloat(float value)
		{
			if (m_parameterWrapper.TryGetAnimationHash(out int hash))
				m_animator.SetFloat(hash, value);
		}

		// 06000553 (1.10.1 RVA 0x52FCB0)
		public void SetInteger(int value)
		{
			if (m_parameterWrapper.TryGetAnimationHash(out int hash))
				m_animator.SetInteger(hash, value);
		}

		// 06000554 (1.10.1 RVA 0x52FD00)
		public AnimParameterSetter()
		{
		}
	}
}
