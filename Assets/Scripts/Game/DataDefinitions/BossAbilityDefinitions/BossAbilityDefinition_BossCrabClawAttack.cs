using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000482; bodies translated from the 1.10.1 x86-64 binary (0x53E7B0..0x53E870).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_BossCrabClawAttack", menuName = "HardlightProject/DefinitionData/AbilityDef/MeleeAttack", order = 1)]
	public class BossAbilityDefinition_BossCrabClawAttack : AbilityDefinition
	{
		[Tooltip("Time to complete attack as defined by the animation and animator.")]
		public float AttackAnimationLength = 2f;

		[Tooltip("Time after windup until the boss just attacks.")]
		public float PostWindUpAttackTimeOut = 4f;

		[Tooltip("Delay time for third attack so it feels bigger.")]
		public float ExtraWindUpSeconds = 2f;

		[Tooltip("Telegraph range for attack.")]
		public float TelegraphClawChoiceRange;

		[Tooltip("Further than this means no target.")]
		public float TargetValidRangeFar;

		[Tooltip("Closer than this means no target, unless player is within targeting cone.")]
		public float TargetValidRangeNear;

		[Tooltip("Meters of range growth per second when player is out of range.")]
		public float TargetGrowthPerSecond;

		[Tooltip("Max angle of travel to consider a target valid.")]
		public int ValidAttackAngle;

		[Tooltip("Prefab of projectile.")]
		public PrefabPoolType Projectile;

		[Tooltip("Lifetime of projectile, will despawn after this time expires.")]
		public float ProjectileLifetime;

		[Tooltip("Number that is compared to the number of clawAttackCount that has happened until it does the long wind up attack.")]
		public int LongWindUpAttackComparison = 2;

		[NonSerialized]
		public float TargetValidRangeNearSquared;

		public override Type ScriptType => typeof(BossAbility_BossCrabClawAttack);

		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			TargetValidRangeNearSquared = TargetValidRangeNear * TargetValidRangeNear;
		}
	}
}
