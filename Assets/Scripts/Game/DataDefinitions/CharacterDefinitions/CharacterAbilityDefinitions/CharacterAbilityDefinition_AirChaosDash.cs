using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004b9; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_AirChaosDash", menuName = "HardlightProject/DefinitionData/AbilityDef/AirChaosDash", order = 1)]
	public class CharacterAbilityDefinition_AirChaosDash : CharacterAbilityDefinition_AirChaos
	{
		[Tooltip("Minimum time to reach target.")]
		public float MinimumTime = 0.1f;

		[Tooltip("If angle to world up exceeds this, ie for ceilings, then apply offset in the world down direction.")]
		public float OffsetAngleToWorldUp = 160f;

		[Tooltip("Distance to offset target back along dash direction for walls and track.")]
		public float OffsetDistance = 0.5f;

		[Tooltip("Speed to travel in the dash direction.")]
		public float DashSpeed = 100f;

		[Tooltip("Speed to set on exiting chaos dash determined by target slope.")]
		public AnimationCurve ExitSpeedBySlopeCurve = AnimationCurve.Constant(0f, 360f, 20f);

		// 06001ad7 (1.10.1 RVA 0x542120)
		public override Type ScriptType => typeof(CharacterAbility_AirChaosDash);

		// 06001ad8..06001ad9: auto-property accessors (RVA 0x542180, 0x542190).
		public float OffsetCosineAngleToWorldUp { get; private set; }

		// 06001ada (1.10.1 RVA 0x5421A0)
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			OffsetCosineAngleToWorldUp = Mathf.Cos(Mathf.Deg2Rad * OffsetAngleToWorldUp);
		}

		// 06001adb (1.10.1 RVA 0x542230)
		public CharacterAbilityDefinition_AirChaosDash()
		{
		}
	}
}
