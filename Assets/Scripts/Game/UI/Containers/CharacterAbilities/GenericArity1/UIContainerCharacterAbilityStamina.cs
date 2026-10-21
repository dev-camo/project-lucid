using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000867; shared generic bodies 0xCA7690..0xCA7720 (1.10.1 x86-64).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UIContainerCharacterAbilityStamina<TAbility> : UIContainerCharacterAbilityStamina
        where TAbility : CharacterAbility
    {
        // 0600309d / 0600309e
        protected TAbility Ability { get; private set; }

        // 0600309f: the result of the type test is ignored; a failed test publishes null.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            TryGetAbility(out TAbility ability);
            Ability = ability;
        }

        // 060030a0
        protected UIContainerCharacterAbilityStamina() { }
    }
}
