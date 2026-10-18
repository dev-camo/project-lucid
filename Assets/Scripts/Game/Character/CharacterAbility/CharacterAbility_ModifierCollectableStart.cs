using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020002ee; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_ModifierCollectableStart : CharacterAbility_Modifier<CharacterAbilityDefinition_ModifierCollectableStart>
	{
		private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();

		// 060011e7 (1.10.1 RVA 0x74BEE0)
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			SetStartingCount(Definition.StartingCount);
		}

		// 060011e8 (1.10.1 RVA 0x74BF70)
		private void SetStartingCount(int startingCount)
		{
			m_collectableManagerRef.Get().SetCollectableTierStartingCount(Definition.CollectableType, startingCount);
		}

		// 060011e9 (1.10.1 RVA 0x74BFD0)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			SetStartingCount(0);
		}

		// 060011ea (1.10.1 RVA 0x74C030)
		public CharacterAbility_ModifierCollectableStart()
		{
		}
	}
}
