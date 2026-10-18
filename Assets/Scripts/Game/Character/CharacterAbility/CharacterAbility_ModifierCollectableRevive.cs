using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020002ed; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_ModifierCollectableRevive : CharacterAbility_Modifier<CharacterAbilityDefinition_ModifierCollectableRevive>
	{
		private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();

		// 060011e3 (1.10.1 RVA 0x74BCE0)
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			SetReviveMultiplier(Definition.Multiplier);
		}

		// 060011e4 (1.10.1 RVA 0x74BD70)
		private void SetReviveMultiplier(float reviveMultiplier)
		{
			m_collectableManagerRef.Get().SetCollectableTierReviveMultiplier(Definition.CollectableType, reviveMultiplier);
		}

		// 060011e5 (1.10.1 RVA 0x74BDE0)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			SetReviveMultiplier(0f);
		}

		// 060011e6 (1.10.1 RVA 0x74BE40)
		public CharacterAbility_ModifierCollectableRevive()
		{
		}
	}
}
