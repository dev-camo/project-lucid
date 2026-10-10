using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000523; complete own fields and method API.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterStickyControlsDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CharacterStickyControlsDefinition")]
    public class CharacterStickyControlsDefinition : ScriptableObject
    {
        // 0x04001254; original instance offset 0x18.
        public CharacterStickyControls StickyControls;

        // 0x06001c0a: both native architectures tail-call the genuine base constructor.
        // No own field initialization, callback, allocation, null test or catch is present.
        public CharacterStickyControlsDefinition() { }
    }
}
