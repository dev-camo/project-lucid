using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200075a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_ValueFloat : TriggerCondition_Value<float>
	{
		// 06002931 (1.10.1 RVA 0x5CDDB0)
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(m_value, m_valueThreshold);
		}

		// 06002932 (1.10.1 RVA 0x5CDDD0)
		public override void IncrementValue(float delta)
		{
			m_value += delta;
		}
	}
}
