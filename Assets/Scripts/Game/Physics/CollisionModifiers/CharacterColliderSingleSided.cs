using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000741. A one-way collider: it stays solid only while the
    // character moves with the collider's up direction. The native constructor inlines the
    // base constructors that set up the CharacterManager reference.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    public class CharacterColliderSingleSided : CharacterColliderModifier
    {
        // 060028d9; a character that is practically stationary always passes, otherwise it passes
        // when its heading does not point along its up direction.
        public override bool DoEvaluateConditions(Character character)
        {
            if (character.WorldVelocityMagnitude < 0.0001f)
                return true;
            return Vector3.Dot(character.UpDirection, character.WorldVelocityNormalised) <= 0f;
        }

        // 060028da
        public CharacterColliderSingleSided() { }
    }
}
