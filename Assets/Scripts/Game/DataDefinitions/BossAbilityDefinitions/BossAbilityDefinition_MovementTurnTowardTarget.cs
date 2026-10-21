using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200048a; bodies translated from the 1.10.1 x86-64 binary (0x53ED40..0x53EDC0).
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MovementTurnTowardTarget", menuName = "HardlightProject/DefinitionData/AbilityDef/TurnTowardsTarget", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_MovementTurnTowardTarget : AbilityDefinition
	{
		public float FaceAngleTolerance = 10f;

		public float TurnSpeed = 10f;

		public override Type ScriptType => typeof(BossAbility_TurnTowardTarget);
	}
}
