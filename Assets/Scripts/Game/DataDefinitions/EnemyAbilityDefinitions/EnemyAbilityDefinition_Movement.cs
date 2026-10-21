using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200053f; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EnemyAbilityDefinition_Movement : AbilityDefinition
    {
        [Tooltip("Turning speed, degrees per second.")]
        public float TurnSpeed = 120f;
        [Tooltip("Forward speed when moving, units per second.")]
        public float ForwardSpeed = 3f;
        [Tooltip("Curve affecting turn speed.")]
        public AnimationCurve TurnSameDirectionCurve;
        [Tooltip("Curve affecting acceleration.")]
        public AnimationCurve AccelerationCurve;

        // 06001c8e (1.10.1 RVA 0x54BCF0)
        protected EnemyAbilityDefinition_Movement() { }
    }
}
