using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000759; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_ValueBool : TriggerCondition_Value<bool>
	{
		// 0600292e (1.10.1 RVA 0x5CDD40): booleans compare as 0 and 1 through the integer overload.
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(m_value ? 1 : 0, m_valueThreshold ? 1 : 0);
		}

		// 0600292f (1.10.1 RVA 0x5CDD60): a boolean has no increment, so the value is replaced.
		public override void IncrementValue(bool value)
		{
			m_value = value;
		}
	}
}
