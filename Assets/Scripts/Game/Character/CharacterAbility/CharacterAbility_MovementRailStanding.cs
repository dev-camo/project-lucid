using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002e4; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_MovementRailStanding : CharacterAbility_MovementRail<CharacterAbilityDefinition_MovementRailStanding>
	{
		// 060011a7 (1.10.1 RVA 0x74AE90)
		public override void SetActive(bool active)
		{
			IsTriggered = active;
		}

		// 060011a8 (1.10.1 RVA 0x74AED0)
		public CharacterAbility_MovementRailStanding()
		{
		}
	}
}
