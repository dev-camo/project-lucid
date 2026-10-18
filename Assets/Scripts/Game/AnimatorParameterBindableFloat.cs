using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020009e4; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimatorParameterBindableFloat : AnimatorParameterBindable<float>
	{
		// 060038e4 (1.10.1 RVA 0x6A6A90)
		protected override float GetAnimatorValue(Animator animator, int propertyHash)
		{
			return animator.GetFloat(propertyHash);
		}

		// 060038e5 (1.10.1 RVA 0x6A6AB0)
		public AnimatorParameterBindableFloat()
		{
		}
	}
}
