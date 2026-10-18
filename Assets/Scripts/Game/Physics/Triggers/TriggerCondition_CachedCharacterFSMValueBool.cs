using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200074d; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(menuName = "HardlightProject/TriggerConditions/TriggerCondition_CachedCharacterFSMValueBool", fileName = "TriggerCondition_CachedCharacterFSMValueBool")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_CachedCharacterFSMValueBool : TriggerCondition_CachedCharacterFSMValue<bool>
	{
		// 0600290c (1.10.1 RVA 0x5CD410)
		protected override bool GetValueDefault()
		{
			return false;
		}

		// 0600290d (1.10.1 RVA 0x5CD420): booleans compare as 0 and 1 through the integer overload.
		protected override bool DoEvaluate(Character character)
		{
			return m_comparisonType.Evaluate(GetValue(character) ? 1 : 0, m_valueThreshold ? 1 : 0);
		}
	}
}
