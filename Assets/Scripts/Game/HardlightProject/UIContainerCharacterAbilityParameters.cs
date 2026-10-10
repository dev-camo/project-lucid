using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000861: a sealed parameter object with two readonly
    // auto-property fields. The original interface carries no methods or fields.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCharacterAbilityParameters : IUIContainerParameters
    {
        // 0600308e/8f return the two original fields without lifetime filtering.
        public Character Character { get; }
        public CharacterAbility Ability { get; }

        // 06003090: Object construction precedes character, then ability publication.
        // Null parameter values are accepted and retained by the original constructor.
        public UIContainerCharacterAbilityParameters(Character character, CharacterAbility ability)
        {
            Character = character;
            Ability = ability;
        }
    }
}
