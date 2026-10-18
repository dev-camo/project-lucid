using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200050c; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_DetectorLightspeedDash", menuName = "HardlightProject/DefinitionData/AbilityDef/DetectorLightspeedDash", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbilityDefinition_DetectorLightspeedDash : CharacterAbilityDefinition_Detector
	{
		// 06001bad (1.10.1 RVA 0x545F00)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_DetectorLightspeedDash);
			}
		}

		// 06001bae (1.10.1 RVA 0x545F60)
		public CharacterAbilityDefinition_DetectorLightspeedDash()
		{
		}
	}
}
