using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200074e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_CharacterArchetype : TriggerCondition
	{
		[SerializeField]
		private CharacterArchetype[] m_requiredCharacterArchetypes;

		// 0600290f (1.10.1 RVA 0x5CD4C0): an empty list matches no character.
		protected override bool DoEvaluate(Character character)
		{
			foreach (CharacterArchetype archetype in m_requiredCharacterArchetypes)
			{
				if (archetype == character.Definition.Archetype)
					return true;
			}
			return false;
		}
	}
}
