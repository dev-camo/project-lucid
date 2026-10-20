using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002e1; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_MovementRailBoost : CharacterAbility_MovementRailBoostBase<CharacterAbilityDefinition_MovementRailBoost>
	{
		// 06001196 (1.10.1 RVA 0x74A8C0); true while the one-shot boost flag is absent or false.
		protected override bool CanTrigger()
		{
			return !m_character.TryGetValue(ActorFSMKeys.OneShotCanTriggerBoost);
		}

		// 06001197 (1.10.1 RVA 0x74A930)
		public CharacterAbility_MovementRailBoost()
		{
		}
	}
}
