using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original0200031f candidate, private and unaccepted. The genuine
    // Character/TrackManager/collision-tracking graph still needs source closure.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterCollider
    {
        private const float GroundPenetrationTolerance = 0.01f;
        private const float SpeedSqrToleranceMin = 0.001f;
        private const float SpeedSqrToleranceMax = 1000f;
        private const float OrientationToContactMaxAngleDeviation = 30f;
        // Exact original ctor binary32 constant 0x3f5db3d7, cos(30 degrees).
        private readonly float m_orientationToContactMaxCosAngleDeviation = 0.8660254f;
        private readonly Character m_character;

        public Ribbon LinkedRibbon => HasLinkedCollisionData ? LinkedCollisionData.GetTrackerRibbon() : null;
        public CharacterCollisionData LinkedCollisionData { get; private set; }
        public CharacterCollisionData LinkedCollisionDataLastFrame { get; private set; }
        public bool HasLinkedCollisionData { get; private set; }
        public TerrainTrackerMetadataKeyLookupDefinition TerrainTrackerMetadataKeyLookup { get; }
        public Action<ContactPoint> ValidateContactCallback;
        public Action<Collision> OnAnyCollisionStay;
        private int m_collisionContactCount;
        private Vector3 m_collisionContactPosition = Vector3.zero;
        private Vector3 m_collisionContactNormal = Vector3.zero;
        private Vector3 m_collisionContactZeroPosition = Vector3.zero;
        private Vector3 m_collisionContactZeroNormal = Vector3.up;
        private int m_nonTrackCollisionContactCount;
        private Vector3 m_nonTrackCollisionContactPosition = Vector3.zero;
        private Vector3 m_nonTrackCollisionContactNormal = Vector3.zero;
        private readonly List<Transform> m_nonTrackCollisionTransforms = new List<Transform>();
        public RaycastColliderHit GroundHit { get; } = new RaycastColliderHit();
        public RaycastColliderHit GroundHitLastFrame { get; } = new RaycastColliderHit();
        public readonly CharacterCollisionTracker ConnectionTracker = new CharacterCollisionTracker();
        public readonly CharacterCollisionTracker ImpactTracker = new CharacterCollisionTracker();
        private CharacterSettings.ColliderSettings Settings => m_character.Settings.Collider;
        private bool ColliderContactIgnored { get; set; }
        // Original06001355 is a BeforeFieldInit initializer, not an explicit cctor.
        private static readonly RaycastHit[] s_raycastHitResults = new RaycastHit[16];

        // Original06001337 initializes owned trackers before the Object base call;
        // the supplied TrackManager argument is genuinely unused in this body.
        public CharacterCollider(Character character, TrackManager trackManager, TerrainTrackerMetadataKeyLookupDefinition terrainTrackerMetadataKeyLookup)
        {
            m_character = character;
            TerrainTrackerMetadataKeyLookup = terrainTrackerMetadataKeyLookup;
            GroundHit.SetDistances(Settings.GroundCheckMinDistance, Settings.GroundCheckMaxDistance);
        }

        // Original06001338 is genuinely RET; it does not unregister the callback.
        public void Close(TrackManager trackManager) { }

        // Original06001339/33a: global contact mode changes before callback mutation.
        public void RegisterTrackManager(TrackManager trackManager)
        {
            trackManager.SetColliderModifiableContacts(true);
            trackManager.OnColliderContactIgnored += OnColliderContactIgnored;
        }

        public void UnregisterTrackManager(TrackManager trackManager)
        {
            trackManager.SetColliderModifiableContacts(false);
            trackManager.OnColliderContactIgnored -= OnColliderContactIgnored;
        }

        // Original0600133b publishes the out contact before any rejection/callback.
        // NaN separation passes this inverted guard; the two cosine tests are ordered.
        private bool ValidateContact(Collision collision, int contactIndex, out ContactPoint contact)
        {
            contact = collision.GetContact(contactIndex);
            if (contact.separation > 0f)
                return false;
            ValidateContactCallback?.Invoke(contact);
            if (Vector3.Dot(m_character.UpDirection, contact.normal) > m_orientationToContactMaxCosAngleDeviation)
                return true;
            return Vector3.Dot(m_character.WorldUp, contact.normal) > m_orientationToContactMaxCosAngleDeviation;
        }

        public void OnCollisionEnter(Collision collision) => CalculateCollisionContact(collision); //0600133c

        // Original0600133d takes the callback after contact accumulation finishes.
        public void OnCollisionStay(Collision collision)
        {
            CalculateCollisionContact(collision);
            OnAnyCollisionStay?.Invoke(collision);
        }

        private void CalculateCollisionContact(Collision collision) //0600133e
        {
            if (m_character.TrackMask.IncludesLayer(collision.gameObject))
            {
                int contactCount = collision.contactCount;
                for (int index = 0; index < contactCount; index++)
                {
                    if (!ValidateContact(collision, index, out var contact))
                        continue;
                    m_collisionContactPosition += contact.point;
                    m_collisionContactNormal += contact.normal;
                    m_collisionContactCount++;
                }
            }
            else
                CalculateNonTrackCollision(collision);
        }

        // Original0600133f preserves the caller's out value if the real lookup faults.
        // A successful lookup publishes its modifier even when HasColliderModifier=false.
        private bool TryGetColliderModifier(Collider collider, out CharacterColliderModifier colliderModifier)
        {
            if (m_character.TrackManager.TryGetCharacterCollisionData(collider, out var collisionData))
            {
                colliderModifier = collisionData.ColliderModifier;
                return collisionData.HasColliderModifier;
            }
            colliderModifier = null;
            return false;
        }

        private void CalculateNonTrackCollision(Collision collision) //06001340
        {
            if (!m_character.CollisionMask.IncludesLayer(collision.gameObject))
                return;
            if (TryGetColliderModifier(collision.collider, out var colliderModifier) && colliderModifier.DoEvaluateConditions(m_character))
                return;
            int contactCount = collision.contactCount;
            for (int index = 0; index < contactCount; index++)
            {
                var contact = collision.GetContact(index);
                m_nonTrackCollisionContactPosition += contact.point;
                m_nonTrackCollisionContactNormal += contact.normal;
                m_nonTrackCollisionContactCount++;
                m_nonTrackCollisionTransforms.AddUnique(collision.transform);
            }
        }

        public void SetLinkedCollisionData(CharacterCollisionData linkedCollisionData) //06001341
        {
            if (linkedCollisionData == null)
                return;
            if (LinkedCollisionData != null && !linkedCollisionData.HasTracker)
                return;
            LinkedCollisionData = linkedCollisionData;
            HasLinkedCollisionData = true;
        }

        public void UpdateCollisionTracking(float deltaTime) //06001342
        {
            if (deltaTime == 0f)
                return;
            var worldPosition = m_character.WorldPosition;
            Transform collisionTransform = GroundHit.Hit ? GroundHit.HitTransform : null;
            Vector3 collisionPosition = GroundHit.Hit ? GroundHit.HitPosition() : Vector3.zero;
            Vector3 collisionNormal = GroundHit.Hit ? GroundHit.HitNormal() : Vector3.zero;
            ConnectionTracker.Update(collisionTransform, collisionPosition, collisionNormal, worldPosition, deltaTime, SpeedSqrToleranceMin, SpeedSqrToleranceMax);
            ImpactTracker.Update(m_nonTrackCollisionTransforms.Count > 0 ? m_nonTrackCollisionTransforms[0] : null,
                m_nonTrackCollisionContactPosition, m_nonTrackCollisionContactNormal, worldPosition, deltaTime, SpeedSqrToleranceMin, SpeedSqrToleranceMax);
        }

        public void Reset() => ResetCollision(); //06001343

        // Original06001344 averages only multiple contacts and reduces each count to1.
        public void Finalise()
        {
            if (m_collisionContactCount > 1)
            {
                m_collisionContactPosition /= m_collisionContactCount;
                m_collisionContactNormal /= m_collisionContactCount;
                m_collisionContactCount = 1;
            }
            if (m_nonTrackCollisionContactCount > 1)
            {
                m_nonTrackCollisionContactPosition /= m_nonTrackCollisionContactCount;
                m_nonTrackCollisionContactNormal /= m_nonTrackCollisionContactCount;
                m_nonTrackCollisionContactCount = 1;
            }
        }

        public void StickToCollider(float deltaTime) //06001345
        {
            float force = 0f;
            if (LinkedCollisionData != null && LinkedCollisionData.HasTracker &&
                LinkedCollisionData.TrackerMetadata.TryGetValue(TerrainTrackerMetadataKeyLookup.StickToColliderForceMetadataKey, out force) && force > 0f)
            {
                var velocity = m_character.WorldVelocity;
                m_character.SetWorldVelocity(velocity - GetCollisionContactNormal() * force);
                return;
            }
            if (ConnectionTracker.Active)
            {
                var up = m_character.UpDirection;
                float speed = Vector3.Dot(up, ConnectionTracker.Velocity);
                if (speed > 0.0001f && speed < Settings.GroundCheckMinDistance)
                {
                    var position = m_character.WorldPosition;
                    position += up * Vector3.Dot(up, ConnectionTracker.Position - position);
                    position += up * (speed * deltaTime);
                    m_character.SetWorldPosition(position);
                    return;
                }
            }
            if (!CharacterPhysicsUtilities.OrientateToLookAheadSlope(m_character, deltaTime))
                CharacterPhysicsUtilities.StickToCollider(m_character, deltaTime);
        }

        public void PerformGroundCheck() //06001346
        {
            if (GroundHit.Valid)
                GroundHitLastFrame.Set(GroundHit);
            var position = m_character.Bounds.center;
            var worldUp = m_character.WorldUp;
            var direction = -m_character.UpDirection;
            float offset = m_character.HalfHeight;
            GroundHit.Raycast(position, direction, offset, worldUp, m_character.TrackMask);
            if (!GroundHit.Hit)
                return;
            if (!GroundHit.HitIsEdge && GroundHitLastFrame.Hit && GroundHitLastFrame.HitIsEdge &&
                Vector3.Dot(GroundHit.HitNormal(), GroundHitLastFrame.HitNormal()) < m_orientationToContactMaxCosAngleDeviation)
            {
                GroundHit.Invalidate();
                GroundHitLastFrame.Invalidate();
                return;
            }
            m_collisionContactPosition += GroundHit.HitPosition();
            m_collisionContactNormal += GroundHit.HitNormal();
            m_collisionContactCount++;
            SetLinkedCollisionData(GroundHit.GetComponent<CharacterCollisionData>());
            // Preserve the native inverse guard: unordered distance enters correction.
            if (!(GroundHit.HitDistance() >= -GroundPenetrationTolerance))
            {
                var velocity = m_character.WorldVelocity;
                float speed = Vector3.Dot(velocity, m_character.UpDirection);
                m_character.SetWorldVelocity(velocity - GroundHit.HitNormal() * speed);
                m_character.SetWorldPosition(GroundHit.HitPosition());
            }
        }

        private void ResetCollision() //06001347
        {
            if (m_collisionContactCount != 0)
            {
                m_collisionContactZeroPosition = m_collisionContactPosition;
                m_collisionContactZeroNormal = m_collisionContactNormal;
            }
            m_collisionContactCount = 0;
            m_collisionContactPosition = Vector3.zero;
            m_collisionContactNormal = Vector3.zero;
            ColliderContactIgnored = false;
            m_nonTrackCollisionContactCount = 0;
            m_nonTrackCollisionContactPosition = Vector3.zero;
            m_nonTrackCollisionContactNormal = Vector3.zero;
            m_nonTrackCollisionTransforms.Clear();
            LinkedCollisionDataLastFrame = LinkedCollisionData;
            LinkedCollisionData = null;
            HasLinkedCollisionData = false;
        }

        private void OnColliderContactIgnored() => ColliderContactIgnored = true; //06001348
        public bool HasTrackContact() => m_collisionContactCount > 0 || ColliderContactIgnored; //06001349

        public void OnMoveAway() //0600134a
        {
            if (GroundHit.HitProjected)
            {
                var velocity = m_character.WorldVelocity;
                var normal = GroundHit.HitNormal();
                m_character.SetWorldVelocity(velocity - normal * Vector3.Dot(velocity, normal));
            }
        }

        public Vector3 GetCollisionContactPosition() => m_collisionContactCount == 0 ? m_collisionContactZeroPosition : m_collisionContactPosition; //0600134b
        public Vector3 GetCollisionContactNormal() => m_collisionContactCount == 0 ? m_collisionContactZeroNormal : m_collisionContactNormal; //0600134c
        public int GetNonTrackCollisionContactCount() => m_nonTrackCollisionContactCount; //0600134d
        public Vector3 GetNonTrackCollisionContactNormal() => m_nonTrackCollisionContactNormal; //0600134e

        // Original0600134f advances past the current contact, then searches for a
        // later contact. The budget sums squared STEP lengths, not path length.
        // Loop/final predicates below preserve the actual dual-architecture NaN
        // branch shape instead of replacing it with a conventional distance guard.
        public bool GetProjectedColliderCollision(LayerMask layerMask, float maximumDistance, out Quaternion hitRotation, out float hitTime)
        {
            hitTime = 0f;
            hitRotation = Quaternion.identity;
            float deltaTime = Time.fixedDeltaTime;
            var position = m_character.WorldPosition;
            var velocity = m_character.WorldVelocity;
            var gravity = m_character.Gravity;
            float maximumDistanceSquared = maximumDistance * maximumDistance;
            var offset = velocity * deltaTime;
            var gravityStep = gravity * deltaTime;
            Vector3 collisionPosition = Vector3.zero;
            Vector3 collisionNormal = Vector3.zero;
            float squaredDistance = 0f;
            while (ColliderExtensions.ProjectRaycastLocation(position, offset, layerMask, s_raycastHitResults, ref collisionPosition, ref collisionNormal, null) &&
                squaredDistance < maximumDistanceSquared)
            {
                // Native inlined DebugAddLocationInstance retains only this call.
                if (!(offset.sqrMagnitude < 0.0001f)) Quaternion.LookRotation(offset);
                position += offset;
                velocity += gravityStep;
                hitTime += deltaTime;
                offset = velocity * deltaTime;
                squaredDistance += offset.sqrMagnitude;
            }
            bool found;
            while (true)
            {
                bool hit = ColliderExtensions.ProjectRaycastLocation(position, offset, layerMask, s_raycastHitResults, ref collisionPosition, ref collisionNormal, null);
                found = hit || !(squaredDistance < maximumDistanceSquared);
                if (found)
                    break;
                if (!(offset.sqrMagnitude < 0.0001f)) Quaternion.LookRotation(offset);
                position += offset;
                velocity += gravityStep;
                hitTime += deltaTime;
                offset = velocity * deltaTime;
                squaredDistance += offset.sqrMagnitude;
                if (!m_character.LevelBounds.Contains(position))
                    break;
            }
            if (squaredDistance >= maximumDistanceSquared)
                found = false;
            if (!(offset.sqrMagnitude < 0.0001f)) Quaternion.LookRotation(offset);
            if (found)
            {
                var forward = Vector3.ProjectOnPlane(m_character.ForwardDirection, collisionNormal);
                hitRotation = Quaternion.LookRotation(forward, collisionNormal);
                if (!(collisionNormal.sqrMagnitude < 0.0001f)) Quaternion.LookRotation(collisionNormal);
            }
            return found;
        }

        public bool ProjectColliderRaycastLocation(Vector3 position, Vector3 offset, LayerMask layerMask, ref Vector3 collisionPosition, ref Vector3 collisionNormal) =>
            ColliderExtensions.ProjectRaycastLocation(position, offset, layerMask, s_raycastHitResults, ref collisionPosition, ref collisionNormal, null); //06001350

        public bool CanTrackToLocation(Vector3 worldPosition) //06001351
        {
            var delta = worldPosition - m_character.WorldPosition;
            delta -= m_character.UpDirection * Vector3.Dot(delta, m_character.UpDirection);
            float distance = delta.magnitude;
            float step = Settings.TrackingStep;
            if (distance < step)
                return true;
            int steps = Mathf.FloorToInt(distance / step);
            var trackMask = m_character.TrackMask;
            if (steps < 1)
                return true;
            var stepOffset = delta * (step / distance);
            var position = m_character.WorldPosition + m_character.UpDirection * Settings.TrackingDistanceUp;
            var direction = -m_character.UpDirection;
            float maxDistance = Settings.TrackingDistanceDown;
            for (int index = 0; index < steps; index++)
            {
                position += stepOffset;
                if (!Physics.Raycast(position, direction, maxDistance, trackMask))
                    return false;
            }
            return true;
        }

        private void DebugAddLocationInstance(Vector3 position, Vector3 normal, float t, string tag) //06001352
        {
            if (normal.sqrMagnitude < 0.0001f)
                return;
            Quaternion.LookRotation(normal);
        }

        public void GetDebugInfo(StringBuilder stringInfoBuilder) //06001353
        {
            ConnectionTracker.GetDebugInfo("Connection", stringInfoBuilder);
            ImpactTracker.GetDebugInfo("Impact", stringInfoBuilder);
        }

        // Original06001354 is genuinely RET in both supplied architectures.
        public void GetDebugInfoImpactHistory(StringBuilder stringInfoBuilder) { }
    }
}
