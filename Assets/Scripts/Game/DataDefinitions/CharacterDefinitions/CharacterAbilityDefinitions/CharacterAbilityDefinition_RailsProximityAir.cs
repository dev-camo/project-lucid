using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004f9; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_RailsProximityAir", menuName = "HardlightProject/DefinitionData/AbilityDef/RailsProximityAir", order = 1)]
	public class CharacterAbilityDefinition_RailsProximityAir : CharacterAbilityDefinition_RailsHoming
	{
		[Tooltip("Angle of the rail surface normal to character velocity for proximity attachment.")]
		[Range(0f, 360f)]
		public float SurfaceNormalAngleTolerance = 80f;

		[Min(0f)]
		[Tooltip("Rail attach distance when tracking, e.g. from a jump.")]
		public float RailTrackerAttachDistance = 1f;

		// 06001b74 (1.10.1 RVA 0x544BD0)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_RailsProximityAir);
			}
		}

		// 06001b75/06001b76 (1.10.1 RVA 0x544C30/0x544C40)
		public float SurfaceNormalAngleCosineTolerance { get; private set; }

		// 06001b77 (1.10.1 RVA 0x544C50); the native body inlines the targeting and rails homing cache refresh.
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			SurfaceNormalAngleCosineTolerance = Mathf.Cos(SurfaceNormalAngleTolerance * Mathf.Deg2Rad);
		}

		// 06001b78 (1.10.1 RVA 0x544CF0)
		public CharacterAbilityDefinition_RailsProximityAir()
		{
		}
	}
}
