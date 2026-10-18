using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000507; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ModifierRingSpeed", menuName = "HardlightProject/DefinitionData/AbilityDef/ModifierRingSpeed", order = 1)]
	public class CharacterAbilityDefinition_ModifierRingSpeed : CharacterAbilityDefinition_Modifier
	{
		// Original nested 02000508.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		public class TierDefinition
		{
			[SerializeField]
			private int m_ringCount;

			[SerializeField]
			private ActorAnimationDefinition m_animationDefinition;

			// 06001ba2 (1.10.1 RVA 0x545D00)
			public int RingCount
			{
				get
				{
					return m_ringCount;
				}
			}

			// 06001ba3 (1.10.1 RVA 0x545D10)
			public ActorAnimationDefinition AnimationDefinition
			{
				get
				{
					return m_animationDefinition;
				}
			}

			// 06001ba4 (1.10.1 RVA 0x545D20)
			public TierDefinition()
			{
			}
		}

		private static readonly Keyframe DefaultKeyframeStart = new Keyframe(0f, 1f);

		private static readonly Keyframe DefaultKeyframeEnd = new Keyframe(1f, 1f);

		[Tooltip("Ring count multiplier to motion acceleration up to the minimum speed.")]
		public AnimationCurve AccelerationMultiplier = new AnimationCurve(new Keyframe[2] { DefaultKeyframeStart, DefaultKeyframeEnd });

		[Tooltip("Ring count multiplier to motion minimum speed.")]
		public AnimationCurve SpeedMinMultiplier = new AnimationCurve(new Keyframe[2] { DefaultKeyframeStart, DefaultKeyframeEnd });

		[Tooltip("Ring count multiplier to motion maximum speed.")]
		public AnimationCurve SpeedMaxMultiplier = new AnimationCurve(new Keyframe[2] { DefaultKeyframeStart, DefaultKeyframeEnd });

		[Tooltip("Ring count multiplier to motion deceleration down to the maximum speed")]
		public AnimationCurve DecelerationMultiplier = new AnimationCurve(new Keyframe[2] { DefaultKeyframeStart, DefaultKeyframeEnd });

		[Tooltip("Defines each ring tier's audio and pfx to trigger.")]
		public List<TierDefinition> TierDefinitions = new List<TierDefinition>();

		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ModifierRingSpeed);
			}
		}

		// 06001b9f (1.10.1 RVA 0x5458B0); the lowest tier is the first entry.
		public int TierRingCountMin
		{
			get
			{
				if (TierDefinitions.Count > 0)
				{
					return TierDefinitions[0].RingCount;
				}
				return 0;
			}
		}

		// 06001ba0 (1.10.1 RVA 0x545910)
		public CharacterAbilityDefinition_ModifierRingSpeed()
		{
		}
	}
}
