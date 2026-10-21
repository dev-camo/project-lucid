using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000485; bodies translated from the 1.10.1 x86-64 binary (0x53EA30..0x53EAC0).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_LaserFence", menuName = "HardlightProject/DefinitionData/AbilityDef/LaserFence", order = 1)]
	public class BossAbilityDefinition_LaserFence : AbilityDefinition
	{
		[Tooltip("Gap between lasers in world space coords.")]
		[HideIf("HasStaticTargets", null)]
		public float HeightBoostPerLaserPlacement = 0.3f;

		[Tooltip("Gap between lasers in world space coords.")]
		[HideIf("HasStaticTargets", null)]
		public float InitialBoostHeight = 2f;

		[Tooltip("How deep should the hazard be?")]
		public float HazardDepth = 2f;

		[Tooltip("How high should the hazard be?")]
		public float HazardHeight = 8f;

		[Tooltip("How far up should the center of the hazard be?")]
		public float HazardYOffset = 5f;

		[Tooltip("Set to appropriate attach point")]
		public ActorAttachPointType Attachment = ActorAttachPointType.BossFourmen_LaserEmitter;

		[Tooltip("Static targets for Fourman, false for Fourmen")]
		public bool HasStaticTargets;

		[Tooltip("LocalPosition for laser targets")]
		[ShowIf("HasStaticTargets", null)]
		public Vector3 LaserTarget;

		public override Type ScriptType => typeof(BossAbility_LaserFence);
	}
}
