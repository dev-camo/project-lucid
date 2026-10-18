using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000752; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_CharacterFSMValueFloat : TriggerCondition_CharacterFSMValue<float>
	{
		// 0600291a (1.10.1 RVA 0x5CD680)
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(GetValue(character), m_valueThreshold);
		}

		// 0600291b (1.10.1 RVA 0x5CD6E0)
		protected override float GetValueDefault()
		{
			return 0f;
		}
	}
}
