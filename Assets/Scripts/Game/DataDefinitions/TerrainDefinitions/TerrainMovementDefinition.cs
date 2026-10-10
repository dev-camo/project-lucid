using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005ce; complete own fields and method API.
    [CreateAssetMenu(fileName = "TerrainMovementDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TerrainMovementDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TerrainMovementDefinition : ScriptableObject
    {
        // 0x040014b8; original instance offset 0x18.
        public CharacterTraits.TurnTraits TurnTraits;
        // 0x040014b9; original instance offset 0x20.
        public CharacterAbilityDefinition_Movement.SlopeMotion Motion;
        // 0x040014ba; original instance offset 0x28.
        public ActorAbilityType AbilityOverride;

        // 0x06001f69: both native architectures tail-call the genuine base constructor.
        // No own field initialization, callback, allocation, null test or catch is present.
        public TerrainMovementDefinition() { }
    }
}
