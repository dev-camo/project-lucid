using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020002f0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_ModifierRingSpeed : CharacterAbility_ModifierEffect<CharacterAbilityDefinition_ModifierRingSpeed>
	{
		private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();

		private int m_ringCount;

		// 060011f0 (1.10.1 RVA 0x74C0D0); the modified ability's speed function gains the ring scaling.
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			base.AddModifier(modifyAbility);
			m_ringCount = GetRingCountState();
			modifyAbility.m_modifierSpeed += ApplyModifierRings;
		}

		// 060011f1 (1.10.1 RVA 0x74C290); the held ring count, zero when there is no ring state.
		private int GetRingCountState()
		{
			m_collectableManagerRef.Get().TryGetCollectableState(CollectableType.Ring, out CollectableState collectableState);
			if (collectableState != null)
			{
				return collectableState.Collected;
			}
			return 0;
		}

		// 060011f2 (1.10.1 RVA 0x74C300); each curve is sampled at the ring count. Crossing up through
		// the first tier whose ring count is newly reached plays that tier's animation, and falling
		// below the lowest tier leaves the animation.
		private void ApplyModifierRings(ref float acceleration, ref float speedMin, ref float speedMax, ref float deceleration)
		{
			int ringCount = GetRingCountState();
			acceleration *= Definition.AccelerationMultiplier.Evaluate(ringCount);
			speedMin *= Definition.SpeedMinMultiplier.Evaluate(ringCount);
			speedMax *= Definition.SpeedMaxMultiplier.Evaluate(ringCount);
			deceleration *= Definition.DecelerationMultiplier.Evaluate(ringCount);
			foreach (CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition tierDefinition in Definition.TierDefinitions)
			{
				if (ringCount >= tierDefinition.RingCount && m_ringCount < tierDefinition.RingCount)
				{
					TriggerAnimationEnter(tierDefinition.AnimationDefinition);
					break;
				}
			}
			m_ringCount = ringCount;
			if (ringCount < Definition.TierRingCountMin)
			{
				TriggerAnimationLeave();
			}
		}

		// 060011f3 (1.10.1 RVA 0x74C590)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			base.RemoveModifier(modifyAbility);
			modifyAbility.m_modifierSpeed -= ApplyModifierRings;
		}

		// 060011f4 (1.10.1 RVA 0x74C710)
		public CharacterAbility_ModifierRingSpeed()
		{
		}
	}
}
