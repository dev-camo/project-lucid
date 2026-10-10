using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_MovementAir<T> : CharacterAbility_Movement<T> where T : CharacterAbilityDefinition_Movement
    {
        protected override void UpdateForwardSpeed(ref float forwardSpeed, ref Vector3 forwardDirection,
            float deltaTime, CharacterAbilityDefinition.Motion motion, float intendedTurnDelta = 1f,
            float effectiveInputMagnitude = 1f, AnimationCurve accelerationCurve = null, bool applyDeceleration = true)
        {
            float speedMin = motion.SpeedMin;
            float speedMax = motion.SpeedMax;
            float acceleration = motion.Acceleration;
            float deceleration = motion.EvaluateDeceleration(forwardSpeed);
            m_modifierSpeed?.Invoke(ref acceleration, ref speedMin, ref speedMax, ref deceleration);
            // Both shipping CPU branches put unordered input in the zero-input route.
            if (!(effectiveInputMagnitude > 0f || effectiveInputMagnitude < 0f))
            {
                if (applyDeceleration)
                {
                    if (forwardSpeed > 0f) forwardSpeed = Mathf.Max(forwardSpeed - deceleration * deltaTime, 0f);
                }
                else forwardSpeed = Mathf.Min(forwardSpeed, speedMax);
            }
            else
            {
                float multiplier = accelerationCurve != null
                    ? accelerationCurve.Evaluate(Mathf.Min(forwardSpeed, speedMax) / speedMax) : 1f;
                acceleration *= multiplier;
                forwardSpeed = Mathf.Max(forwardSpeed - deceleration * deltaTime, 0f);
                Vector3 velocity = forwardDirection * forwardSpeed;
                Quaternion turn = Quaternion.AngleAxis(intendedTurnDelta, Vector3.up);
                velocity += turn * (forwardDirection * (acceleration * deltaTime));
                float magnitude = Mathf.Sqrt(velocity.sqrMagnitude);
                if (magnitude > 0f) forwardDirection = velocity / magnitude;
                if (forwardSpeed > speedMin)
                {
                    if (magnitude < forwardSpeed) forwardSpeed = magnitude;
                }
                else forwardSpeed = Mathf.Min(magnitude, speedMin);
            }
            if (motion.ClampToSpeedMin) forwardSpeed = Mathf.Max(forwardSpeed, speedMin);
            forwardSpeed = motion.ClampSpeed(forwardSpeed);
        }

        public CharacterAbility_MovementAir() { }
    }
}
