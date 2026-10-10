using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterTrackingCollider : CharacterTracking //020003bf, zero own fields.
    {
        protected CharacterTrackingCollider(Character character) : base(character) { } //060016d7
        public override bool IsOnGround() { return m_character.Collider.GroundHit.Hit; } //d8
        public override bool WasOnGround() { return m_character.Collider.GroundHitLastFrame.Hit; } //d9
        public override Vector3 GetContactNormal() { return m_character.Collider.GetCollisionContactNormal(); } //da

        //060016db: TrackMask is evaluated before the collider receiver is reread.
        public override bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime)
        {
            LayerMask trackMask = m_character.TrackMask;
            return m_character.Collider.GetProjectedColliderCollision(trackMask, maximumDistance, out hitRotation, out hitTime);
        }

        public override bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit) //060016dc
        {
            LayerMask trackMask = m_character.TrackMask;
            return Physics.Raycast(ray, out raycastHit, distance, trackMask);
        }

        public override bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit) //060016dd
        {
            LayerMask collisionMask = m_character.CollisionMask;
            return Physics.Raycast(ray, out raycastHit, distance, collisionMask, QueryTriggerInteraction.Ignore);
        }

        public override void OrientateToGround() //060016de
        {
            Vector3 normal = m_character.Collider.GetCollisionContactNormal();
            m_character.OrientateToPlane(normal);
        }
    }
}
