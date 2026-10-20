using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002fc; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_HomingAttackAir<TAbilityDef> : CharacterAbility_HomingAttack<TAbilityDef> where TAbilityDef : CharacterAbilityDefinition_HomingAttackAir
	{
		// 0600122d (1.10.1 RVA 0xE61380)
		protected CharacterAbility_HomingAttackAir()
		{
		}
	}
}
