using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000754; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_CharacterId : TriggerCondition
	{
		[SerializeField]
		private CharacterId[] m_requiredCharacterIds;

		// 06002920 (1.10.1 RVA 0x5CD7E0): an empty list matches no character.
		protected override bool DoEvaluate(Character character)
		{
			foreach (CharacterId id in m_requiredCharacterIds)
			{
				if (id == character.IdType)
					return true;
			}
			return false;
		}
	}
}
