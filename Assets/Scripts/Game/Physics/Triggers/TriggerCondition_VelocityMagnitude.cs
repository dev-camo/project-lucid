using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200075e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_VelocityMagnitude : TriggerCondition
	{
		[SerializeField]
		private float m_velocityMagnitudeThreshold;
		[SerializeField]
		private ComparisonType m_comparisonType;

		// 06002942 (1.10.1 RVA 0x5CE2B0)
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(character.WorldVelocityMagnitude, m_velocityMagnitudeThreshold);
		}
	}
}
