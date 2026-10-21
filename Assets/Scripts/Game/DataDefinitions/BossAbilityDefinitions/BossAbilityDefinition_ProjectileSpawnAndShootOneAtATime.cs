using System;
using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000494; bodies translated from the 1.10.1 x86-64 binary (0x53F620..0x53F6D0).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime", menuName = "HardlightProject/DefinitionData/AbilityDef/ProjectileSpawnAndShootOneAtATime", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime : BossAbilityDefinition_ProjectileSpawnAndShoot
	{
		[FormerlySerializedAs("ProjectileGroupLaunchTimeSeconds")]
		[Tooltip("Time to launch all projectiles in a group.")]
		public float ProjectileGroupLaunchTimeSeconds = 1f;

		[FormerlySerializedAs("TimeBetweenSpawnAndLaneMove")]
		[Tooltip("Time between spawning and starting to move to lanes.")]
		public float TimeBetweenSpawnAndLaneMoveSeconds = 0.25f;

		public float ProjectileMoveToLaneVelocity = 20f;

		public float ProjectileSpawnCircleRadius = 6f;

		[Tooltip("Lanes will be ignored from the spawn pool for X number of spawns after they are used.")]
		public int LaneIgnoreCount = 2;

		public override Type ScriptType => typeof(BossAbility_ProjectileSpawnAndShootOneAtATime);
	}
}
