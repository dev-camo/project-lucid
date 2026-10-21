using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000308; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_RailsProximityGround : CharacterAbility_RailsProximity<CharacterAbilityDefinition_RailsProximityGround>
	{
		// 06001277 (1.10.1 RVA 0x750720); only a grounded character attaches.
		protected override bool ValidToTrigger()
		{
			return m_character.Collider.GroundHit.Hit;
		}

		// 06001278 (1.10.1 RVA 0x750740)
		public CharacterAbility_RailsProximityGround()
		{
		}
	}
}
