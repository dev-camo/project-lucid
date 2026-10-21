using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000495; bodies translated from the 1.10.1 x86-64 binary (0x53F6D0..0x53F750).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_RangedAttack", menuName = "HardlightProject/DefinitionData/AbilityDef/RangedAttack", order = 1)]
	public class BossAbilityDefinition_RangedAttack : AbilityDefinition
	{
		[Tooltip("Number of projectiles per wave.")]
		public int ProjectileCount;

		[Tooltip("Number of waves of projectiles to fire.")]
		public int WaveCount;

		[Tooltip("Time between waves of shots.")]
		public float WaveCooldownSeconds = 0.5f;

		[Tooltip("Time between each projectile.")]
		public float ProjectileCooldownSeconds = 0.05f;

		[Tooltip("Array of Projectiles.")]
		public Projectile[] Projectiles;

		[Tooltip("Random +- values on the target transform to simulate inaccuracy.")]
		public Vector3 ProjectileAccuracy;

		[Tooltip("Minimum Shoot Range: Boss will start shooting once target is inside this range.")]
		public float MinRange;

		[Tooltip("Maximum Shoot Range: Boss will stop shooting once target is outside this range.")]
		public float MaxRange;

		public override Type ScriptType => typeof(BossAbility_RangedAttack);
	}
}
