using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000498; bodies translated from the 1.10.1 x86-64 binary (0x53F860..0x53F8F0).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_StayAheadOfPlayer", menuName = "HardlightProject/DefinitionData/AbilityDef/StayAheadOfPlayer", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_StayAheadOfPlayer : AbilityDefinition
	{
		public float MinRadiansAheadOfPlayer = -0.12f;

		public float MaxRadiansAheadOfPlayer = -0.24f;

		[Tooltip("If the player stops moving closer, this is the speed the boss will move away at.")]
		public float MovementPerSecond = 0.01f;

		public float Radius = 25f;

		public bool Clockwise = true;

		public float TurnSpeed = 90f;

		public override Type ScriptType => typeof(BossAbility_StayAheadOfPlayer);
	}
}
