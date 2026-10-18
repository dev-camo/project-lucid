using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000753; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_CharacterFSMValueInt : TriggerCondition_CharacterFSMValue<int>
	{
		// 0600291d (1.10.1 RVA 0x5CD730)
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(GetValue(character), m_valueThreshold);
		}

		// 0600291e (1.10.1 RVA 0x5CD790): the fallback when the storage holds no value is -1, not 0.
		protected override int GetValueDefault()
		{
			return -1;
		}
	}
}
