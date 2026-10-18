using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004cb; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbilityDefinition_EnterClimbing : CharacterAbilityDefinition
	{
		[Header("Base climb ability properties")]
		[Tooltip("Layers to check for climbing. Colliders must also have the climbing metaData set.")]
		public LayerMask TrackMask = -1;

		[Tooltip("Minimum vertical angle of surface relative to gravity direction required to allow climbing.")]
		[Range(0f, 360f)]
		public float MinVerticalAngleSurface = 60f;

		[Tooltip("Maximum vertical angle of surface relative to gravity direction required to allow climbing.")]
		[Range(0f, 360f)]
		public float MaxVerticalAngleSurface = 120f;

		[Min(0f)]
		[Tooltip("Ability won't trigger until this many seconds have passed since last being on a climb wall.")]
		public float ReAttachCooldownSeconds = 0.5f;

		// 06001b07 (1.10.1 RVA 0x542FF0); the native call passes the default 1e-4 tolerance.
		public bool SurfaceAngleValid(float angle)
		{
			return MathUtilities.WithinRangeWithTolerance(angle, MinVerticalAngleSurface, MaxVerticalAngleSurface);
		}

		// 06001b08 (1.10.1 RVA 0x542E30)
		protected CharacterAbilityDefinition_EnterClimbing()
		{
		}
	}
}
