using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020006d9; the static constructor 060024f6 (1.10.1 RVA 0x59C8B0)
	// stores each key's own name in declaration order.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public static class MessageNameKeys
	{
		public static readonly string OnCharacterRespawn = "OnCharacterRespawn";
		public static readonly string OnCharacterTeleport = "OnCharacterTeleport";
		public static readonly string OnHubInteractionStateChanged = "OnHubInteractionStateChanged";
		public static readonly string OnHubInteractionTextChanged = "OnHubInteractionTextChanged";
	}
}
