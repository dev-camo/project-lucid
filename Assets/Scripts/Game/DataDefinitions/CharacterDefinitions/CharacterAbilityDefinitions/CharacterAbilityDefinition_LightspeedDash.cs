using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_LightspeedDash", menuName = "HardlightProject/DefinitionData/AbilityDef/LightspeedDash", order = 1)]
    public class CharacterAbilityDefinition_LightspeedDash : CharacterAbilityDefinition_MovementTracker
    {
        // Original 0x04001174; offset 0xe0.
        [Tooltip("When target ring is reached, scan for closest lightspeed dash spline within this distance.")]
        public float ProximityDistance = 10f;

        public override Type ScriptType => typeof(CharacterAbility_LightspeedDash);
        public CharacterAbilityDefinition_LightspeedDash() { }
    }
}
