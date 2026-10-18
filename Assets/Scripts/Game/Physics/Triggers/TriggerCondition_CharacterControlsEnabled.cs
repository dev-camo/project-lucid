using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200074f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_CharacterControlsEnabled : TriggerCondition
	{
		// 06002911 (1.10.1 RVA 0x5CD570)
		protected override bool DoEvaluate(Character character)
		{
			return character.AreControlsEnabled();
		}
	}
}
