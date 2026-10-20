using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004d9; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbilityDefinition_HomingAttackBase : CharacterAbilityDefinition_Targeting
	{
		[Tooltip("Layers the character looks for to target.")]
		public LayerMask HomingMask = -1;

		[Tooltip("If disabled, collider will always project from character position.")]
		public bool ProjectFromCameraWhenNoDirectionInput = true;

		[Tooltip("Project collider from character orientation.")]
		public bool ProjectFromCharacterForward;

		[Tooltip("Size of box collider used to detect potential targets.")]
		public Vector3 TargetZoneSize;

		[ShowIf("ProjectFromCameraWhenNoDirectionInput", (string)null)]
		[Tooltip("Size of box collider when projecting from the camera (when no input is made) to detect potential targets.")]
		public Vector3 TargetZoneSizeFromCamera;

		[Tooltip("Used when controller input is being made. Center offset of box collider used to detect potential targets.")]
		public Vector3 CharacterTargetZoneOffset;

		[Tooltip("Used when no controller input is being made. Center offset of box collider used to detect potential targets.")]
		[ShowIf("ProjectFromCameraWhenNoDirectionInput", (string)null)]
		public Vector3 CameraTargetZoneOffset;

		[Tooltip("The homing attack prefab to detect and monitor possible targets.")]
		public HomingAttack HomingAttackPrefab;

		// 06001b1d (1.10.1 RVA 0x5435E0); the depth of the detection box.
		public float TargetDistance
		{
			get
			{
				return TargetZoneSize.z;
			}
		}

		// 06001b1e (1.10.1 RVA 0x5432A0)
		protected CharacterAbilityDefinition_HomingAttackBase()
		{
		}
	}
}
