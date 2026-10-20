using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020004e2; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_LedgeMount", menuName = "HardlightProject/DefinitionData/AbilityDef/Ledge Mount", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_LedgeMount : CharacterAbilityDefinition_EnterClimbing
	{
		[Header("Dismount properties")]
		[Tooltip("The forward velocity to apply from the mount, along the new surface.")]
		[Range(0f, 50f)]
		public float MountVelocityForward = 20f;

		[Range(0f, 50f)]
		[Tooltip("The upward velocity to apply from the mount, in the direction of the new surface's normal.")]
		public float MountVelocityUpward = 10f;

		[Range(0f, 5f)]
		[Tooltip("Seconds to pause movement before finishing ledge mount and applying velocity.")]
		public float PauseSeconds = 0.5f;

		[Header("Ledge detection properties")]
		[Tooltip("Offset in character local space from the character origin, to raycast from for ledge detection. Increase this to better cope with small gaps or overhangs in the seams of the geometry.")]
		public Vector3 RaycastOriginOffset = new Vector3(0f, 0.25f, 0.25f);

		[Tooltip("Distance to raycast from origin offset to find ledge to mount.")]
		public float RaycastDistance = 0.5f;

		[Range(2f, 16f)]
		[Tooltip("Max number of raycast steps used at different angles, from the character local down to local back (from the climbing wall position).")]
		public int RaycastSteps = 12;

		[Tooltip("If true, ignore any edges detected that are \"behind\" the character in the local rotation.")]
		public bool IgnoreHitsBehind;

		[Tooltip("If true, scans for hits from local-down to local-back. If false, the order is reversed.")]
		public bool ReverseSearchOrder;

		// 06001b39 (1.10.1 RVA 0x543CD0)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_LedgeMount);
			}
		}

		// 06001b3a (1.10.1 RVA 0x543D30)
		public CharacterAbilityDefinition_LedgeMount()
		{
		}
	}
}
