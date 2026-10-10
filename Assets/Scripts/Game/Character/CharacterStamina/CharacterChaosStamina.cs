using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterChaosStamina : CharacterStamina
    {
        // 060014ac / ARM74ab7c: genuine base-only constructor.
        public CharacterChaosStamina(Character character) : base(character) { }

        // 060014ad / ARM74ab80: native hash0xabfd5d32, genuine enum literal.
        public override bool IsCollectableType(CollectableType collectableType) => collectableType == CollectableType.ChaosEnergy;
    }
}
