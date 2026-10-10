// Complete original Game.Runtime 02000324. Opposed unsubscribe callbacks are retained.
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterDebugImmobilise
    {
        private const float ActivateButtonTime = 1f;
        private const float MovementSpeed = 10f;
        private bool m_active;
        private float m_activeTime;
        private bool m_activeForward;
        private bool m_activeBackward;
        private Vector3 m_position;
        private Vector3 m_forward;
        private float m_direction;
        private StackableDataHandle m_modifierHandle;
        private readonly Character m_character;

        public CharacterDebugImmobilise(Character character) // 060013a1
        {
            m_character = character;
            ControlMapping.Subscribe(GameInput.CharacterSelectNext, OnDebugMoveForwards, InputTrigger.Held, -1);
            ControlMapping.Subscribe(GameInput.CharacterSelectPrev, OnDebugMoveBackwards, InputTrigger.Held, -1);
        }
        public void OnFixedUpdate(float deltaTime) // 060013a2: test prior accumulated time before incrementing it.
        {
            if (m_activeForward && m_activeBackward && m_activeTime > ActivateButtonTime) ToggleActivation();
            m_activeForward = false;
            m_activeBackward = false;
            m_activeTime += deltaTime;
            if (m_active)
            {
                m_position += m_forward * (deltaTime * MovementSpeed * m_direction);
                m_character.SetWorldPosition(m_position);
                m_character.SetWorldVelocity(Vector3.zero);
                m_direction = 0f;
            }
        }
        public void Close() // 060013a3: shipping associates each key with the opposite callback here.
        {
            ControlMapping.Unsubscribe(GameInput.CharacterSelectNext, OnDebugMoveBackwards);
            ControlMapping.Unsubscribe(GameInput.CharacterSelectPrev, OnDebugMoveForwards);
        }
        private void ToggleActivation() // 060013a4
        {
            m_active = !m_active;
            m_activeTime = 0f;
            if (m_active)
            {
                m_modifierHandle = m_character.AddModifierOverride((int)GameplayModifierType.ApplyGravity, false);
                m_position = m_character.WorldPosition;
                Vector3 forward = m_character.ForwardDirection;
                if (Mathf.Abs(Vector3.Dot(forward, Vector3.forward)) > Mathf.Abs(Vector3.Dot(forward, Vector3.right)))
                    m_forward = Vector3.forward * Mathf.Sign(Vector3.Dot(forward, Vector3.forward));
                else m_forward = Vector3.right * Mathf.Sign(Vector3.Dot(forward, Vector3.right));
            }
            else
            {
                m_character.RemoveModifierOverrides(m_modifierHandle);
                m_modifierHandle = null;
            }
        }
        private void OnDebugMoveForwards(float amount) { m_direction += amount; m_activeForward = true; } // 060013a5
        private void OnDebugMoveBackwards(float amount) { m_activeBackward = true; m_direction -= amount; } // 060013a6
    }
}
