using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002cf; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_InHub : CharacterAbility_MovementGround<CharacterAbilityDefinition_InHub>, IVelocityStore
	{
		// 0600110e (1.10.1 RVA 0x747790)
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			GravityMultiplier = Definition.GravityMultiplier;
			VelocityStorage.Initialise(Definition.VelocityStorageParameters, m_character);
		}

		// 0600110f (1.10.1 RVA 0x747830)
		public CharacterVelocityStorage VelocityStorage { get; } = new CharacterVelocityStorage();

		// 06001110 (1.10.1 RVA 0x747840)
		public CharacterAbility_InHub()
		{
		}
	}
}
