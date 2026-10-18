using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002f1; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_ModifierSwitchAvailable : CharacterAbility_Modifier<CharacterAbilityDefinition_ModifierSwitchAvailable>
	{
		// 060011f5 (1.10.1 RVA 0x74C7B0)
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			modifyAbility.SetSwitchAvailableOverride(true);
		}

		// 060011f6 (1.10.1 RVA 0x74C7C0)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			modifyAbility.SetSwitchAvailableOverride(false);
		}

		// 060011f7 (1.10.1 RVA 0x74C7D0)
		public CharacterAbility_ModifierSwitchAvailable()
		{
		}
	}
}
