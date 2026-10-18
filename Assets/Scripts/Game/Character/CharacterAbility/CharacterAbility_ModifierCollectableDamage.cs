using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020002ec; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_ModifierCollectableDamage : CharacterAbility_Modifier<CharacterAbilityDefinition_ModifierCollectableDamage>
	{
		private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();

		// 060011df (1.10.1 RVA 0x74BAF0)
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			SetDamageTiers(Definition.MaxDamage);
		}

		// 060011e0 (1.10.1 RVA 0x74BB80)
		private void SetDamageTiers(int maxDamage)
		{
			m_collectableManagerRef.Get().SetCollectableTierMaxDamage(Definition.CollectableType, maxDamage);
		}

		// 060011e1 (1.10.1 RVA 0x74BBE0)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			SetDamageTiers(0);
		}

		// 060011e2 (1.10.1 RVA 0x74BC40)
		public CharacterAbility_ModifierCollectableDamage()
		{
		}
	}
}
