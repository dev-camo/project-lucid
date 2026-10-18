using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002a1; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimatorNormaliseFloat : AnimatorModifyFloat
	{
		[SerializeField]
		private float m_rangeMin;

		[SerializeField]
		private float m_rangeMax;

		// 06000e7e (1.10.1 RVA 0x726CE0): the source magnitude is normalised, so a
		// negative value maps like its positive counterpart.
		protected override float ProcessFloat(float sourceValue)
		{
			return Mathf.InverseLerp(m_rangeMin, m_rangeMax, Mathf.Abs(sourceValue));
		}

		// 06000e7f (1.10.1 RVA 0x726D30)
		protected override void DoOnEnter()
		{
		}

		// 06000e80 (1.10.1 RVA 0x726D40)
		public AnimatorNormaliseFloat()
		{
		}
	}
}
