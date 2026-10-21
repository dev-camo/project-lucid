using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000500; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_TransporterProximity", menuName = "HardlightProject/DefinitionData/AbilityDef/TransporterProximity", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbilityDefinition_TransporterProximity : CharacterAbilityDefinition_RailsHoming
	{
		// 06001b91 (1.10.1 RVA 0x545290)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_TransporterProximity);
			}
		}

		// 06001b92 (1.10.1 RVA 0x5452F0)
		public CharacterAbilityDefinition_TransporterProximity()
		{
		}
	}
}
