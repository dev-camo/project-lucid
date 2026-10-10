using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAnimator : ActorAnimator
    {
        private readonly ActorAnimationSettingsDefinition m_animationSettings;
        private readonly List<float> m_dampenedXZVelocityBuffer;

        // Original 060012d0: the real base activates the Animator GameObject
        // before the definition is stored and its sample capacity is read.
        public CharacterAnimator(Animator animator, ActorAnimationSettingsDefinition animationSettings)
            : base(animator)
        {
            m_animationSettings = animationSettings;
            m_dampenedXZVelocityBuffer = new List<float>(animationSettings.DampenedXZLocalVelocitySampleSize);
        }

        // Original 060012d1: failed/null casts are passed through to the
        // private routine, whose first Character field read remains unguarded.
        public override void OnUpdate(Actor actor)
        {
            base.OnUpdate(actor);
            UpdateVelocityAnimationParameters(actor as Character);
        }

        // Original 060012d2: authored wrappers are visited in this exact order.
        // The dampened parameter is the maximum of a bounded history. Both
        // native backends skip samples <= maximum; unordered samples replace
        // it. A zero capacity attempts RemoveAt(0) before adding a sample.
        private void UpdateVelocityAnimationParameters(Character character)
        {
            Vector3 localVelocity = character.LocalVelocity;
            TrySetAnimation(m_animationSettings.RawXLocalVelocity, localVelocity.x);
            TrySetAnimation(m_animationSettings.RawYLocalVelocity, localVelocity.y);
            TrySetAnimation(m_animationSettings.RawZLocalVelocity, localVelocity.z);
            float xzSpeed = localVelocity.xz().magnitude;
            TrySetAnimation(m_animationSettings.RawXZLocalVelocity, xzSpeed);
            Vector2 controllerMovement = character.ControllerMovement;
            TrySetAnimation(m_animationSettings.RawXControlInput, controllerMovement.x);
            TrySetAnimation(m_animationSettings.RawYControlInput, controllerMovement.y);

            if (m_animationSettings.DampenedXZLocalVelocity.TryGetAnimationHash(out int hash))
            {
                if (m_dampenedXZVelocityBuffer.Count == m_dampenedXZVelocityBuffer.Capacity)
                    m_dampenedXZVelocityBuffer.RemoveAt(0);
                m_dampenedXZVelocityBuffer.Add(xzSpeed);

                float maximum = 0f;
                foreach (float sample in m_dampenedXZVelocityBuffer)
                {
                    if (sample <= maximum)
                        continue;
                    maximum = sample;
                }
                SetAnimation(hash, maximum);
            }
        }

        // Original 060012d3 does not guard the wrapper reference.
        private void TrySetAnimation(AnimationParameterWrapper animationParameterWrapper, float value)
        {
            if (animationParameterWrapper.TryGetAnimationHash(out int hash))
                SetAnimation(hash, value);
        }
    }
}
