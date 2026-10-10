using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterAbilityDefinition_MovementTracker : CharacterAbilityDefinition_Movement
    {

        protected CharacterAbilityDefinition_MovementTracker() { }
    }
}
