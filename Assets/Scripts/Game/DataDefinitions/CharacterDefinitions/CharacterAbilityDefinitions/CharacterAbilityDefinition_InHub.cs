using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004de; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_InHub", menuName = "HardlightProject/DefinitionData/AbilityDef/InHub", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_InHub : CharacterAbilityDefinition_MovementGround
	{
		public GameplayLevelDefinition HubLevelDefinition;

		public CameraType CameraType;

		public VelocityRestoreParameters VelocityStorageParameters;

		[Tooltip("The number of seconds your control is reduced on exit (Y) based on the speed that will be restored.")]
		public AnimationCurve ControlDeadzoneForExitSpeed;

		[Tooltip("How much to dampen movement input by during the deadzone seconds.")]
		public AnimationCurve ControlRestoration;

		// 06001b2a (1.10.1 RVA 0x543920)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_InHub);
			}
		}

		// 06001b2b (1.10.1 RVA 0x543980)
		public CharacterAbilityDefinition_InHub()
		{
		}
	}
}
