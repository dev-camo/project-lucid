using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004d0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_HalfPipeAir", menuName = "HardlightProject/DefinitionData/AbilityDef/HalfPipeAir", order = 1)]
	public class CharacterAbilityDefinition_HalfPipeAir : CharacterAbilityDefinition_HalfPipe
	{
		// Original nested 020004d1.
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		public class HalfPipeMotionOverride : HalfPipeMotion
		{
			[Tooltip("Motion is triggered if speed is exceeded.")]
			public float ActivationSpeed;

			[Tooltip("Motion is triggered if the elapsed time since the last half pipe is less than this.")]
			public float ActivationTime;

			// 06001b10 (1.10.1 RVA 0x543140)
			public HalfPipeMotionOverride()
			{
			}
		}

		// Original nested 020004d2.
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		public class HalfPipeMotionExclude
		{
			[Tooltip("The angle of the half pipe to exclude motion.")]
			public float ExcludeAngle;

			[HideInInspector]
			public float ExcludeAngleCosine;

			// 06001b11 (1.10.1 RVA 0x543150)
			public HalfPipeMotionExclude()
			{
			}
		}

		[Tooltip("Maps the lip angle to a trajectory.")]
		public HalfPipeTrajectoryDefinition Trajectory;

		[Tooltip("Default movement if not overriden.")]
		public HalfPipeMotion AirMotion;

		[Tooltip("Movement override on consecutive half pipes.")]
		public HalfPipeMotionOverride[] AirMotionOverrides;

		[Tooltip("Movement exclusion that defaults back to the base air motion.")]
		public HalfPipeMotionExclude AirMotionExclude;

		// 06001b0d (1.10.1 RVA 0x543080)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_HalfPipeAir);
			}
		}

		// 06001b0e (1.10.1 RVA 0x5430E0)
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			AirMotionExclude.ExcludeAngleCosine = Mathf.Cos(AirMotionExclude.ExcludeAngle * Mathf.Deg2Rad);
		}

		// 06001b0f (1.10.1 RVA 0x543120)
		public CharacterAbilityDefinition_HalfPipeAir()
		{
		}
	}
}
