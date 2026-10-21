using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004fb; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_RailsTargeting", menuName = "HardlightProject/DefinitionData/AbilityDef/RailsTargeting", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_RailsTargeting : CharacterAbilityDefinition_RailsHoming
	{
		[Tooltip("The cone ellipse angles.")]
		[Min(0f)]
		public Vector2 HomingProjectConeAngle = new Vector2(30f, 30f);

		[Tooltip("The distance to step when projecting through the world to find a target.")]
		[Min(0.1f)]
		public float HomingProjectDistanceStep = 1f;

		[Tooltip("Velocity when homing to a target.")]
		[Min(0f)]
		public float HomingVelocity = 50f;

		[Tooltip("If disabled, collider will always project from character position.")]
		public bool ProjectFromCameraWhenNoDirectionInput;

		// 06001b7b (1.10.1 RVA 0x544DE0)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_RailsTargeting);
			}
		}

		// 06001b7c/06001b7d (1.10.1 RVA 0x544E40/0x544E50)
		public Vector2 HomingProjectConeTangents { get; private set; }

		// 06001b7e (1.10.1 RVA 0x544E60); the native body inlines the targeting and rails homing cache refresh.
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			HomingProjectConeTangents = new Vector2(Mathf.Tan(HomingProjectConeAngle.x * Mathf.Deg2Rad), Mathf.Tan(HomingProjectConeAngle.y * Mathf.Deg2Rad));
		}

		// 06001b7f (1.10.1 RVA 0x544F30)
		public CharacterAbilityDefinition_RailsTargeting()
		{
		}
	}
}
