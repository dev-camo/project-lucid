using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000491; bodies translated from the 1.10.1 x86-64 binary (0x53F200..0x53F240).
	[CreateAssetMenu(fileName = "BossAbilityDefinition_ProjectileSpawnAndShoot", menuName = "HardlightProject/DefinitionData/AbilityDef/ProjectileSpawnAndShoot", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class BossAbilityDefinition_ProjectileSpawnAndShoot : AbilityDefinition
	{
		[Tooltip("Time to complete attack as defined by the animation and animator.")]
		[FormerlySerializedAs("AttackAnimationLength")]
		public float AttackAnimationLengthSeconds = 1f;

		[FormerlySerializedAs("ProjectileGroupSpawnTime")]
		[Tooltip("Time to spawn projectiles in a group. This can be more or less than ProjectileGroupLaunchTimeSeconds.")]
		public float ProjectileGroupSpawnTimeSeconds = 1f;

		[FormerlySerializedAs("CullTimeOnceBehindPlayer")]
		[Tooltip("Once the projectile is behind the player, how long until it explodes?")]
		public float CullTimeOnceBehindPlayerSeconds = 0.5f;

		public ProjectileType KillerProjectile;

		public ProjectileType ReturningProjectile;

		public float ProjectileRadiansPerSecond = 0.04f;

		public float ReturnRadiansPerSecond = 0.06f;

		public int ProjectileGroupToContainReturnable;

		public GuardianProjectile.ProjectileMovementState InitialMovementState;

		[Tooltip("Adjust the Z on this to send the player Towards (-ve) or away from (+ve) the boss.")]
		public Vector3 ReturnableProjectileSpawnAngle = new Vector3(0f, 0f, -50f);

		public ActorAttachPointType SpawnPointAttach;
	}
}
