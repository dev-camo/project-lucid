using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original 0x0200051a, one field and one public constructor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class CharacterTraits : ActorTraits
    {
        // 0x04001223; original instance offset 0x20.
        [Tooltip("The mass of the character applied to gravity to change its momentum.")]
        public float Mass;

        // 0x06001be5: base only; Mass retains original zero.
        public CharacterTraits() { }

        // Complete original nested 0x0200051b, seven fields and one constructor.
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class TurnTraits
        {
            // 0x04001224; original instance offset 0x10.
            [Tooltip("Determines acceleration/deceleration based on difference between current and target speed.")]
            public UnityEngine.AnimationCurve AccelerationMultiplier;
            // 0x04001225; original instance offset 0x18.
            [Tooltip("Default 180. Rate of turn in degrees per second.")]
            public float BaseRateOfTurn;
            // 0x04001226; original instance offset 0x20.
            [Tooltip("Determines how much speed should multiply the base rate of turn.")]
            public UnityEngine.AnimationCurve RateOfTurnByVelocity;
            // 0x04001227; original instance offset 0x28.
            [Tooltip("Determines how much angle should multiply the base rate of turn.")]
            public UnityEngine.AnimationCurve RateOfTurnByAngle;
            // 0x04001228; original instance offset 0x30.
            [Tooltip("Default 0. Raise to reduce the impact of turning sharply on the effective input magnitude.")]
            public float InputDamping;
            // 0x04001229; original instance offset 0x34.
            [Tooltip("Default 0. Minimum clamp value for effective input multiplier at the most extreme turn angles.")]
            public float MinimumInputMagnitude;
            // 0x0400122a; original instance offset 0x38.
            [Tooltip("Minimum clamp value for effective turn magnitude at the most extreme turn angles.")]
            public float MinimumTurnMagnitude = 0.01f;

            // 0x06001be6: only MinimumTurnMagnitude initializes to 0.01.
            // Tooltip default 180 does not cause a BaseRateOfTurn initializer in native code.
            public TurnTraits() { }
        }
    }
}
