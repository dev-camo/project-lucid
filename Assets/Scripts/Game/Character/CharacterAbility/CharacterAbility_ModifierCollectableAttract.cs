using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020002ea; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_ModifierCollectableAttract : CharacterAbility_ModifierEffect<CharacterAbilityDefinition_ModifierCollectableAttract>
	{
		private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();

		// 060011d6 (1.10.1 RVA 0x74B410)
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			base.AddModifier(modifyAbility);
			SetAttractTiers(Definition.TierIndex);
		}

		// 060011d7 (1.10.1 RVA 0x74B470); with a collect animation, a positive tier index
		// subscribes to collection updates so the animation plays and any other index unsubscribes. An empty type list means every type.
		private void SetAttractTiers(int tierIndex)
		{
			CollectableManager collectableManager = m_collectableManagerRef.Get();
			CollectableType[] collectableTypes = Definition.CollectableTypes.Length != 0 ? Definition.CollectableTypes : EnumUtilities.GetValues<CollectableType>();
			foreach (CollectableType collectableType in collectableTypes)
			{
				if (collectableType != CollectableType.None)
				{
					collectableManager.SetCollectableTierIndex(collectableType, tierIndex);
				}
			}
			if (Definition.CollectAnimationDefinition == null)
			{
				return;
			}
			if (tierIndex > 0)
			{
				collectableManager.OnCollectableStateUpdate += OnCollectableStateUpdate;
			}
			else
			{
				collectableManager.OnCollectableStateUpdate -= OnCollectableStateUpdate;
			}
		}

		// 060011d8 (1.10.1 RVA 0x74B650)
		private void OnCollectableStateUpdate(CollectableType collectableType, CollectableState collectableState, CollectableChangeMetadata metadata)
		{
			if (Definition.CollectableTypes.Length == 0 || Definition.CollectableTypes.Contains(collectableType))
			{
				TriggerAnimationEnter(Definition.CollectAnimationDefinition);
			}
		}

		// 060011d9 (1.10.1 RVA 0x74B6E0)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			base.RemoveModifier(modifyAbility);
			SetAttractTiers(0);
		}

		// 060011da (1.10.1 RVA 0x74B730)
		public CharacterAbility_ModifierCollectableAttract()
		{
		}
	}
}
