using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterAbility_MovementGround<TAbilityDefType> : CharacterAbility_Movement<TAbilityDefType>
        where TAbilityDefType : CharacterAbilityDefinition_MovementGround
    {
        protected CharacterAbility_MovementGround() { }
    }
}
