using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Complete genuine Game.Runtime type0200040d, nine fields/twenty-one methods.
    // Source closure remains unavailable until the real CharacterCollisionData and its
    // Actor/Character/App/terrain/spline dependencies compile together.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterCollisionTracker
    {
        // 060017c6; genuine Unity object liveness, including destroyed transforms.
        public bool HasCollision => Transform != null;
        public bool Active { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Normal { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Transform Transform { get; private set; }
        public bool HasMovement { get; private set; }
        private Vector3 m_worldPositionTracked;
        private Vector3 m_localPositionTracked;

        // 060017d5; ARM76f0ac. First contact resets activity but can inherit explicit
        // authored movement; repeated contact derives velocity at the tracked point.
        // The moving latch is sticky until contact reset or the maximum-speed reset.
        public void Update(Transform collisionTransform, Vector3 collisionPosition, Vector3 collisionNormal,
            Vector3 worldPositionTracked, float deltaTime, float speedSqrToleranceMin, float speedSqrToleranceMax)
        {
            if (collisionTransform == null)
            {
                SetInactive();
                Transform = null;
                return;
            }
            Transform previousTransform = Transform;
            Transform = collisionTransform;
            if (Transform != previousTransform)
            {
                SetInactive();
                CharacterCollisionData collisionData = Transform.GetComponent<CharacterCollisionData>();
                if (collisionData != null)
                {
                    HasMovement = collisionData.HasMovement;
                    if (HasMovement) Velocity = collisionData.MovementVelocity;
                }
                else HasMovement = false;
            }
            else
            {
                Vector3 movement = Transform.TransformPoint(m_localPositionTracked) - m_worldPositionTracked;
                Active = true;
                Velocity = movement.ClampToZero(0.0001f) / deltaTime;
                float speedSqr = Velocity.sqrMagnitude;
                HasMovement = HasMovement || !(speedSqr <= speedSqrToleranceMin);
                // Unordered comparisons follow the native reset route. Keep Transform
                // and tracked positions; common pose writes below replace reset poses.
                if (!(speedSqr <= speedSqrToleranceMax)) SetInactive();
            }
            Position = collisionPosition;
            Normal = collisionNormal;
            Rotation = Transform.rotation;
            m_worldPositionTracked = worldPositionTracked;
            m_localPositionTracked = Transform.InverseTransformPoint(worldPositionTracked);
        }

        // 060017d6; ARM76f57c. The collision reference and tracked-point caches are
        // deliberately retained; null-contact handling clears the reference separately.
        private void SetInactive()
        {
            Active = false;
            Position = Vector3.zero;
            Normal = Vector3.zero;
            Velocity = Vector3.zero;
            Rotation = Quaternion.identity;
            HasMovement = false;
        }

        // 060017d7; ARM76f638. This returns the signed directional projection and
        // multiplies the original velocity by it, rather than projecting a unit vector.
        // A below-threshold speed returns zero while leaving velocity untouched.
        public float SetTransferVelocity()
        {
            float magnitude = Velocity.magnitude;
            if (magnitude < 0.0001f) return 0f;
            float transfer = Vector3.Dot(Velocity / magnitude, Normal);
            Velocity *= transfer;
            return transfer;
        }

        // 060017d8; ARM76f728. Only contact plus movement enables alignment. Either
        // a nonpositive signed distance or relative speed projects both body values.
        // NaN in both comparisons does not grant alignment.
        public void AlignBodyToMovement(Rigidbody body)
        {
            if (!HasCollision || !HasMovement) return;
            float distance = Vector3.Dot(body.position - Position, Normal);
            float normalSpeed = Vector3.Dot(body.velocity - Velocity, Normal);
            if (distance <= 0f || normalSpeed <= 0f)
            {
                body.position -= Normal * distance;
                body.velocity -= Normal * normalSpeed;
            }
        }

        // 060017d9; literal32b9258, after transform liveness/name and velocity boxing.
        public void GetDebugInfo(string label, StringBuilder stringInfoBuilder)
        {
            if (Transform == null) return;
            stringInfoBuilder.AppendLine(string.Format("{0}: {1}: {2:F2}", label, Transform.name, Velocity));
        }
        // 060017da; original base-only constructor, no explicit field initialization.
        public CharacterCollisionTracker() { }
    }
}
