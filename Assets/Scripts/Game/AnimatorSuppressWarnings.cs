using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000a0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[RequireComponent(typeof(Animator))]
	public class AnimatorSuppressWarnings : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		// 06000534 (1.10.1 RVA 0x52F110)
		private void Reset()
		{
			m_animator = GetComponent<Animator>();
		}

		// 06000535 (1.10.1 RVA 0x52F160)
		private void Awake()
		{
			m_animator.logWarnings = false;
		}

		// 06000536 (1.10.1 RVA 0x52F180)
		public AnimatorSuppressWarnings()
		{
		}
	}
}
