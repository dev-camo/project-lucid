using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000751; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_CharacterFSMValueBool : TriggerCondition_CharacterFSMValue<bool>
	{
		// 06002917 (1.10.1 RVA 0x5CD5D0): booleans compare as 0 and 1 through the integer overload.
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(GetValue(character) ? 1 : 0, m_valueThreshold ? 1 : 0);
		}

		// 06002918 (1.10.1 RVA 0x5CD630)
		protected override bool GetValueDefault()
		{
			return false;
		}
	}
}
