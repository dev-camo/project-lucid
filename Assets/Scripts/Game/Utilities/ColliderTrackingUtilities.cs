using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime020009ee, complete own20/natural comparer2 candidate.
    // Private, unaccepted; Character's genuine dependency graph does not yet bind.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class ColliderTrackingUtilities
    {
        private const float RaycastHitSphereRadius = 0.1f;
        private const float RaycastLineWidth = 3f;
        private const int RaycastHitCountMax = 16;
        private static readonly RaycastHit[] m_raycastHitResults = new RaycastHit[RaycastHitCountMax];
        private const float RaycastColliderUpStepDistance = 0.5f;
        private const float RaycastColliderEdgeStepDistance = 1f;
        private const float RaycastProjectForwardDistance = 6f;
        private const float RaycastProjectBackwardDistance = 3f;
        private const float RaycastMinAngleDeviation = 95f;
        private const float RaycastMaxAngleDeviation = 40f;
        //0600392a folds these four values into one exact native binary32 table,
        // on both architectures. Field initializers retain BeforeFieldInit.
        private static readonly float RaycastMinCosAngleDeviation = -0.0871557667851448f;
        private static readonly float RaycastMaxCosAngleDeviation = 0.7660444378852844f;
        private const float RaycastProjectHeightThreshold = 35f;
        private const float RaycastProjectDistance = 2f;
        private const int RaycastAirMaxSteps = 32;
        private const int RaycastAirFrameInterval = 5;
        private const float RaycastUpStepDistance = 0.5f;
        private const int RaycastDownMaxSteps = 3;
        private const float NormalToGravityUpAngleThreshold = 45f;
        private static readonly float NormalToGravityUpAngleCosineThreshold = 0.7071067690849304f;
        private const float NormalToGravityEdgeAngleThreshold = 60f;
        private static readonly float NormalToGravityEdgeAngleCosineThreshold = 0.4999999701976776f;
        private static Character m_character;
        private static int m_projectLocationIndex;

        private class RaycastHitComparer : IComparer<RaycastHit>
        {
            //0600392b: genuine Single.CompareTo, including its NaN ordering.
            public int Compare(RaycastHit hit0, RaycastHit hit1) => hit0.distance.CompareTo(hit1.distance);
            public RaycastHitComparer() { } //0600392c: Object base only.
        }

        // Original public sequential struct020009f0 has four fields and no methods.
        public struct RaycastTransform
        {
            public int LayerMask;
            public Vector3 Position;
            public Vector3 Direction;
            public Vector3 Up;
        }

        // Original020009f1 natural delegate4; generated native wrappers captured.
        public delegate Vector3 CalculateGravityCallback(Vector3 velocity);

        //06003917: physics, new comparer, sort, output publication, in that order.
        public static int RaycastSort(out RaycastHit[] raycastHitResults, Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, m_raycastHitResults, maxDistance, layerMask, queryTriggerInteraction);
            Array.Sort(m_raycastHitResults, 0, count, new RaycastHitComparer());
            raycastHitResults = m_raycastHitResults;
            return count;
        }

        //06003918: a miss preserves every caller field. The successful point read
        // before the release-elided gizmo site is retained, before normal/point writes.
        public static bool RaycastToCollider(ref RaycastTransform raycastTransform)
        {
            RaycastHit hit;
            bool found = Physics.Raycast(raycastTransform.Position - raycastTransform.Direction * RaycastProjectBackwardDistance,
                raycastTransform.Direction, out hit, RaycastProjectForwardDistance, raycastTransform.LayerMask);
            if (found)
            {
                _ = hit.point;
                raycastTransform.Direction = -hit.normal;
                raycastTransform.Position = hit.point;
            }
            return found;
        }

        //06003919: original traversal has no count cap, and its final return is true.
        // Local heading/world-up mixing and the half-unit authored Up step are retained.
        public static bool TrackColliderToTop(ref RaycastTransform raycastTransform, ref Vector3 direction, Vector3 worldUp, bool renderGizmos)
        {
            Vector3 localDirection = Quaternion.Inverse(Quaternion.LookRotation(raycastTransform.Direction, worldUp)) * direction;
            Vector3 localDirectionXZ = localDirection.XZ();
            int layerMask = raycastTransform.LayerMask;
            Vector3 collisionPosition = Vector3.zero;
            Vector3 collisionNormal = Vector3.zero;
            Vector3 rayPosition = raycastTransform.Position - raycastTransform.Direction * RaycastProjectBackwardDistance;
            Vector3 rayOffset = raycastTransform.Direction * RaycastProjectForwardDistance;
            while (ProjectRaycastLocation(rayPosition, rayOffset, false, worldUp, layerMask,
                ref collisionPosition, ref collisionNormal, NormalToGravityUpAngleCosineThreshold, false))
            {
                if (Mathf.Abs(Vector3.Dot(worldUp, collisionNormal)) > RaycastMaxCosAngleDeviation)
                    break;
                raycastTransform.Position = collisionPosition;
                raycastTransform.Direction = -collisionNormal;
                direction = CalculateDirectionOnPlane(localDirectionXZ, collisionNormal, worldUp);
                rayPosition = raycastTransform.Position + direction + raycastTransform.Up * RaycastColliderUpStepDistance
                    - raycastTransform.Direction * RaycastProjectBackwardDistance;
                rayOffset = raycastTransform.Direction * RaycastProjectForwardDistance;
                rayOffset -= worldUp * Vector3.Dot(worldUp, rayOffset);
            }
            Vector3 recombinedDirection = localDirectionXZ + worldUp * Vector3.Dot(localDirection, worldUp);
            direction = CalculateDirectionOnPlane(recombinedDirection, -raycastTransform.Direction, worldUp);
            direction.Normalize();
            return true;
        }

        //0600391a: the two searches use opposite tangent directions. Each restores
        // the original local position/direction after its unbounded search ends.
        public static void TrackColliderEdge(RaycastTransform raycastTransform, Vector3 worldUp, bool renderGizmos)
        {
            Vector3 originalPosition = raycastTransform.Position;
            Vector3 originalDirection = raycastTransform.Direction;
            int layerMask = raycastTransform.LayerMask;
            for (int side = 0; side < 2; ++side)
            {
                float sign = side * 2 - 1;
                Vector3 collisionPosition = Vector3.zero;
                Vector3 collisionNormal = Vector3.zero;
                Vector3 rayPosition = raycastTransform.Position - raycastTransform.Direction * RaycastProjectBackwardDistance;
                Vector3 rayOffset = raycastTransform.Direction * RaycastProjectForwardDistance;
                while (ProjectRaycastLocation(rayPosition, rayOffset, true, worldUp, layerMask,
                    ref collisionPosition, ref collisionNormal, NormalToGravityEdgeAngleCosineThreshold, false))
                {
                    Vector3 tangent = Vector3.Cross(collisionNormal, raycastTransform.Up).normalized * sign;
                    raycastTransform.Position = collisionPosition;
                    raycastTransform.Direction = (side > 0 ? Vector3.Cross(tangent, raycastTransform.Up)
                        : Vector3.Cross(raycastTransform.Up, tangent)) * RaycastProjectForwardDistance;
                    rayPosition = raycastTransform.Position + tangent * RaycastColliderEdgeStepDistance
                        - raycastTransform.Direction * RaycastProjectBackwardDistance;
                    rayOffset = raycastTransform.Direction;
                }
                raycastTransform.Position = originalPosition;
                raycastTransform.Direction = originalDirection;
            }
        }

        //0600391b: the original departure/recontact search uses five real gravity
        // callbacks per query. Its initial and rejected-hit outputs are observable.
        // The final inverse cosine guard deliberately permits unordered comparisons.
        public static bool ProjectToNeighbourSlope(RaycastTransform raycastTransform, Vector3 direction, Vector3 worldUp,
            float speed, CalculateGravityCallback gravity, bool renderGizmos, float deltaTime,
            out float hitTime, out Vector3 hitPosition, out Vector3 hitNormal)
        {
            int layerMask = raycastTransform.LayerMask;
            Vector3 velocity = direction * speed;
            Vector3 planeVelocity = velocity - worldUp * Vector3.Dot(worldUp, velocity);
            Vector3 tangent = Vector3.Cross(worldUp, raycastTransform.Direction).normalized;
            float sign = Vector3.Dot(planeVelocity, tangent) < 0f ? 1f : -1f;
            float planeSpeed = planeVelocity.magnitude;
            Vector3 surfaceNormal = (-raycastTransform.Direction).normalized;
            float surfacePlaneDistance = -Vector3.Dot(raycastTransform.Position, surfaceNormal);
            Vector3 position = raycastTransform.Position;
            Vector3 originalRayDirection = raycastTransform.Direction;
            float minimumHeight = Vector3.Dot(position, worldUp) - RaycastProjectHeightThreshold;
            hitPosition = raycastTransform.Position;
            hitNormal = raycastTransform.Direction;
            hitTime = 0f;
            float queryInterval = deltaTime * RaycastAirFrameInterval;
            Vector3 surfaceMovement = planeVelocity;
            Vector3 planeProjection = Vector3.zero;
            bool hasBeenAirborne = false;
            int hitCount = 0;
            for (int stepIndex = 0; stepIndex < RaycastAirMaxSteps; ++stepIndex)
            {
                if (Vector3.Dot(position, worldUp) < minimumHeight)
                    return false;
                Vector3 collisionPosition = Vector3.zero;
                Vector3 collisionNormal = Vector3.zero;
                if (ProjectRaycastLocation(raycastTransform.Position - raycastTransform.Direction * RaycastProjectBackwardDistance,
                    raycastTransform.Direction * RaycastProjectForwardDistance, true, worldUp, layerMask,
                    ref collisionPosition, ref collisionNormal, NormalToGravityEdgeAngleCosineThreshold, false))
                {
                    surfaceNormal = collisionNormal.normalized;
                    velocity = Vector3.ProjectOnPlane(velocity, collisionNormal);
                    tangent = Vector3.Cross(worldUp, collisionNormal).normalized;
                    surfacePlaneDistance = -Vector3.Dot(collisionPosition, surfaceNormal);
                    planeProjection = surfaceNormal * (Vector3.Dot(position, surfaceNormal) + surfacePlaneDistance);
                    surfaceMovement = tangent * sign * planeSpeed;
                    raycastTransform.Position = collisionPosition + surfaceMovement * queryInterval;
                    raycastTransform.Direction = -collisionNormal;
                }
                else
                {
                    velocity = Vector3.ProjectOnPlane(velocity, surfaceNormal);
                    planeProjection = surfaceNormal * (Vector3.Dot(position, surfaceNormal) + surfacePlaneDistance);
                    raycastTransform.Position += surfaceMovement * queryInterval;
                }
                for (int frameIndex = 0; frameIndex < RaycastAirFrameInterval; ++frameIndex)
                {
                    velocity += gravity(velocity) * deltaTime;
                    position += velocity * deltaTime;
                }
                hitTime += queryInterval;
                hitCount = Raycast(position - planeProjection + surfaceNormal * RaycastProjectBackwardDistance,
                    -surfaceNormal, m_raycastHitResults, RaycastProjectForwardDistance, layerMask);
                if (hitCount != 0 && hasBeenAirborne)
                    break;
                hasBeenAirborne |= hitCount == 0;
                if (stepIndex == RaycastAirMaxSteps - 1)
                    return false;
            }
            if (hitCount < 1)
                return false;
            for (int hitIndex = 0; hitIndex < hitCount; ++hitIndex)
            {
                RaycastHit hit = m_raycastHitResults[hitIndex];
                hitPosition = hit.point;
                hitNormal = hit.normal;
                if (Mathf.Abs(Vector3.Dot(originalRayDirection, hitNormal)) < NormalToGravityEdgeAngleCosineThreshold)
                    return false;
                Vector3 projectedHitNormal = Vector3.ProjectOnPlane(hitNormal, worldUp).normalized;
                Vector3 projectedSurfaceNormal = Vector3.ProjectOnPlane(-raycastTransform.Direction, worldUp).normalized;
                if (Mathf.Abs(Vector3.Dot(projectedHitNormal, projectedSurfaceNormal)) < RaycastMaxCosAngleDeviation)
                    continue;
                RaycastTransform hitTransform = new RaycastTransform
                {
                    LayerMask = raycastTransform.LayerMask,
                    Position = hitPosition,
                    Direction = -hitNormal,
                    Up = worldUp
                };
                Vector3 hitDirection = Vector3.zero;
                TrackColliderToTop(ref hitTransform, ref hitDirection, worldUp, renderGizmos);
                if (Vector3.Dot(worldUp, hitNormal) < -0.0001f)
                    hitNormal = -hitNormal;
                return true;
            }
            return false;
        }

        //0600391c: ordered dot comparisons; no normalisation or Angle conversion.
        public static bool ContactIsEdge(Vector3 normal, Vector3 up, out bool isCeiling)
        {
            float dot = Vector3.Dot(normal, up);
            isCeiling = dot < RaycastMinCosAngleDeviation;
            return dot >= RaycastMinCosAngleDeviation && dot <= 1f - RaycastMaxCosAngleDeviation;
        }

        //0600391d: subtract the unit-normal projection directly; no denominator.
        private static Vector3 CalculateDirectionOnPlane(Vector3 direction, Vector3 normal, Vector3 worldUp)
        {
            Vector3 result = Quaternion.LookRotation(-normal, worldUp) * direction;
            return result - normal * Vector3.Dot(normal, result);
        }

        private static int Raycast(Vector3 position, Vector3 direction, RaycastHit[] raycastHitResults, float distance, int layerMask) //0600391e
            => Physics.RaycastNonAlloc(position, direction, raycastHitResults, distance, layerMask);

        //0600391f: the supplied gizmo flag is genuinely not forwarded by the body.
        public static bool ProjectEdgeRaycastLocation(Vector3 position, Vector3 offset, Vector3 worldUp, LayerMask layerMask, ref Vector3 collisionPosition, ref Vector3 collisionNormal, bool renderGizmos)
            => ProjectRaycastLocation(position, offset, true, worldUp, layerMask, ref collisionPosition, ref collisionNormal,
                NormalToGravityEdgeAngleCosineThreshold, false);

        //06003920: a successful initial query can step upwards without a count cap.
        // A failed initial query only searches the three lower half-unit positions
        // when trackUp is true. Caller outputs survive failure.
        private static bool ProjectRaycastLocation(Vector3 position, Vector3 offset, bool trackUp, Vector3 up, LayerMask layerMask,
            ref Vector3 collisionPosition, ref Vector3 collisionNormal, float normalToGravityAngleCosineThreshold, bool renderGizmos)
        {
            Vector3 step = up * RaycastUpStepDistance;
            Vector3 candidatePosition = Vector3.zero;
            Vector3 candidateNormal = Vector3.zero;
            bool found = ColliderExtensions.ProjectRaycastToLocationClosest(position, offset, layerMask, m_raycastHitResults,
                ref candidatePosition, ref candidateNormal, null) && Vector3.Dot(up, candidateNormal) < normalToGravityAngleCosineThreshold;
            if (found)
            {
                collisionPosition = candidatePosition;
                collisionNormal = candidateNormal;
                if (trackUp)
                {
                    Vector3 nextPosition = position;
                    while (true)
                    {
                        nextPosition += step;
                        if (!ColliderExtensions.ProjectRaycastToLocationClosest(nextPosition, offset, layerMask, m_raycastHitResults,
                            ref candidatePosition, ref candidateNormal, null) || !(Vector3.Dot(up, candidateNormal) < normalToGravityAngleCosineThreshold))
                            break;
                        collisionPosition = candidatePosition;
                        collisionNormal = candidateNormal;
                    }
                }
            }
            else if (trackUp)
            {
                Vector3 nextPosition = position;
                for (int stepIndex = 0; stepIndex < RaycastDownMaxSteps; ++stepIndex)
                {
                    nextPosition -= step;
                    if (ColliderExtensions.ProjectRaycastToLocationClosest(nextPosition, offset, layerMask, m_raycastHitResults,
                        ref candidatePosition, ref candidateNormal, null) && Vector3.Dot(up, candidateNormal) < normalToGravityAngleCosineThreshold)
                    {
                        collisionPosition = candidatePosition;
                        collisionNormal = candidateNormal;
                        found = true;
                        break;
                    }
                }
            }
            if (!found)
                return false;
            AlignNormalToWorldUp(ref collisionNormal, up);
            return true;
        }

        private static void AlignNormalToWorldUp(ref Vector3 normal, Vector3 up) //06003921
        {
            normal -= up * Vector3.Dot(up, normal);
            normal.Normalize();
        }

        //06003922: Gravity and both WorldVelocity reads use the real virtual APIs.
        public static void CalculateCharacterSpeedAlongPlane(Character character, Vector3 colliderNormal, out Vector3 colliderForward, out float colliderSpeedForward)
        {
            colliderForward = Vector3.Cross(colliderNormal, character.Gravity).normalized;
            colliderSpeedForward = Vector3.Project(character.WorldVelocity, colliderForward).magnitude;
            if (Vector3.Dot(colliderForward, character.WorldVelocity) < 0f)
                colliderSpeedForward = -colliderSpeedForward;
        }

        public static void ProjectCharacterToCollider(Character character, Vector3 colliderForward, out Vector3 colliderPosition) //06003923
        {
            colliderPosition = character.Collider.GetCollisionContactPosition();
            colliderPosition -= Vector3.Project(colliderPosition, colliderForward);
            colliderPosition += Vector3.Project(character.WorldPosition, colliderForward);
            colliderPosition += character.GravityNormalised;
        }

        //06003924: property order and the caller's incoming normal are significant.
        public static bool ProjectColliderRaycastLocation(Character character, float dStep, ref Vector3 colliderPosition, ref Vector3 colliderNormal)
        {
            LayerMask layerMask = character.TrackMask;
            Vector3 worldUp = character.WorldUp;
            Vector3 colliderForward = Vector3.Cross(colliderNormal, character.Gravity).normalized;
            return ProjectEdgeRaycastLocation(colliderPosition + colliderForward * dStep + colliderNormal,
                -colliderNormal * RaycastProjectDistance, worldUp, layerMask, ref colliderPosition, ref colliderNormal, false);
        }

        public static void ProcessHalfPipeTrajectory(Character character, HalfPipeTrajectoryDefinition halfPipeTrajectoryAbility) //06003925
        {
            CharacterCollisionData collisionData = character.Collider.LinkedCollisionData;
            if (collisionData != null && collisionData.HasHalfPipeTrajectoryDefinition)
                halfPipeTrajectoryAbility = collisionData.HalfPipeTrajectoryDefinition;
            if (halfPipeTrajectoryAbility == null)
                return;
            float speed = character.WorldVelocityMagnitude;
            if (speed < halfPipeTrajectoryAbility.SpeedMin)
                return;
            Vector3 worldUp = character.WorldUp;
            Vector3 upDirection = character.UpDirection;
            float angle = Vector3.SignedAngle(worldUp, character.ForwardDirection, upDirection);
            if (angle < 0f)
                angle += 360f;
            float trajectory = halfPipeTrajectoryAbility.TrajectoryFromAngle(angle);
            Vector3 forward = Quaternion.AngleAxis(trajectory, upDirection) * worldUp;
            character.OrientateToPlaneWithLookDirection(upDirection, forward);
            character.SetWorldVelocity(character.ForwardDirection * speed);
        }

        //06003926 is genuinely RET in the original shipped release; defaults are
        // retained from metadata, including sphereMultiplier zero.
        private static void RenderGizmoRaycast(Vector3 rayFrom, Vector3 rayTo, Color colour, bool renderGizmos = true,
            float lineWidthMultiplier = 1f, float sphereMultiplier = 0f) { }

        public static void DebugBeginLocationInstantiation(Character character) //06003927
        {
            m_projectLocationIndex = 0;
            m_character = character;
        }

        private static void DebugAddLocationInstantiation(Vector3 position, Quaternion rotation) //06003928
        {
            if (m_character == null)
                return;
            ++m_projectLocationIndex;
        }

        public static void DebugEndLocationInstantiation() => m_character = null; //06003929
    }
}
