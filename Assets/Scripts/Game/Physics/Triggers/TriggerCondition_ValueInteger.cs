using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200075b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_ValueInteger : TriggerCondition_Value<int>
	{
		// 06002934 (1.10.1 RVA 0x5CDE20)
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(m_value, m_valueThreshold);
		}

		// 06002935 (1.10.1 RVA 0x5CDE40)
		public override void IncrementValue(int delta)
		{
			m_value += delta;
		}
	}
}
