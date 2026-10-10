using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterBuff_VelocityRestore : ActorBuff
    {
        private readonly VelocityRestoreParameters m_restoreParameters;
        private readonly Character m_character;
        private readonly float m_targetSpeed;
        private readonly float m_enteredSpeed;

        // Original 0600046a: subscribe before either vector split and magnitude.
        public CharacterBuff_VelocityRestore(Character character, float durationSeconds,
            Vector3 targetComponents, Vector3 enteredLocalVelocity, VelocityRestoreParameters restoreParameters)
            : base(character, durationSeconds)
        {
            m_restoreParameters = restoreParameters;
            m_character = character;
            m_character.Collider.OnAnyCollisionStay += OnAnyCollision;
            SplitVectorForBlend(targetComponents, out Vector3 targetBlend, out Vector3 targetRemaining);
            m_targetSpeed = targetBlend.magnitude;
            SplitVectorForBlend(enteredLocalVelocity, out Vector3 enteredBlend, out Vector3 enteredRemaining);
            m_enteredSpeed = enteredBlend.magnitude;
        }

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            if (Expired) return;
            if (ActorMovementUtilities.GetSlopeAngle(m_actor) >= m_restoreParameters.MaximumSlopeAngle || HasAnyDirectionModifier())
                Expire();
            else
                UpdateLocalVelocity(m_currentTimeSeconds / m_durationSeconds);
        }

        // Original 0600046c: interpolate speed only; use the current blend direction.
        private void UpdateLocalVelocity(float normalisedTime)
        {
            if (HasAnyDirectionModifier()) return;
            Vector3 localVelocity = m_character.LocalVelocity;
            float speed = Mathf.Lerp(m_enteredSpeed, m_targetSpeed, normalisedTime);
            SplitVectorForBlend(localVelocity, out Vector3 blendComponent, out Vector3 remainingComponent);
            m_character.SetLocalVelocity(remainingComponent + blendComponent.normalized * speed);
        }
        private bool HasAnyDirectionModifier()
        {
            return m_character.HasAnyDirectionModifier() ||
                m_character.Storage.GetValueOnly(ActorFSMKeys.HoverHeadingDirection,
                    (CharacterState_AirHover.HeadingDirection)0) != (CharacterState_AirHover.HeadingDirection)0;
        }
        private void SplitVectorForBlend(Vector3 originalVector, out Vector3 blendComponent, out Vector3 remainingComponent)
        {
            blendComponent = Vector3.zero;
            remainingComponent = Vector3.zero;
            if (m_restoreParameters.BlendX) blendComponent.x = originalVector.x;
            else remainingComponent.x = originalVector.x;
            if (m_restoreParameters.BlendY) blendComponent.y = originalVector.y;
            else remainingComponent.y = originalVector.y;
            if (m_restoreParameters.BlendZ) blendComponent.z = originalVector.z;
            else remainingComponent.z = originalVector.z;
        }

        // Original 0600046f: expire on dot strictly below the threshold.
        // The tooltip's opposite wording does not change the shipping comparison.
        private void OnAnyCollision(Collision collision)
        {
            if (Expired) return;
            LayerMask expireOnContact = m_restoreParameters.ExpireOnContact;
            if (!expireOnContact.IncludesLayer(collision.gameObject.layer)) return;
            ContactPoint[] contacts = collision.contacts;
            foreach (ContactPoint contact in contacts)
            {
                if (Vector3.Dot(contact.normal, m_actor.ForwardDirection) < m_restoreParameters.ExpireOnContactDotThreshold)
                {
                    Expire();
                    return;
                }
            }
        }
        public override void Expire()
        {
            if (Expired) return;
            base.Expire();
            m_character.Collider.OnAnyCollisionStay -= OnAnyCollision;
            UpdateLocalVelocity(1f);
        }
    }
}
