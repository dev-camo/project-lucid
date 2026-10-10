// Original Game.Runtime 0x020003bc: complete 44-method CharacterTracker candidate.
// Natural C# deferred iterator preserves six captured generated method behaviors;
// original generated names, metadata tokens, and native layout require separate validation.
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterTracker
    {
        // 0x04000d16 native offset 0x10
        private readonly Character m_character;
        // 0x04000d17 native offset 0x0
        private const float TrackerMaxStepDistance = 0.05f;
        // 0x04000d18 native offset 0x0
        private const float ColliderTrackerTeleportThreshold = 2.5f;
        // 0x04000d19 native offset 0x0
        private const float TrackerWorldToleranceMultiplier = 0.0001f;
        // 0x04000d1a native offset 0x0
        private const float ClampRaycastDistanceCheck = 4f;
        // 0x04000d1b native offset 0x0
        private const float JumpTrackerDistanceCheck = 6f;
        // 0x04000d1c native offset 0x0
        private const float JumpTrackerDistanceCheckSqr = 36f;
        // 0x04000d1d native offset 0x0
        private const float HeightOffsetMin = -0.01f;
        // 0x04000d1e native offset 0x0
        private const float HeightLookAheadMax = 0.05f;
        // 0x04000d1f native offset 0x0
        private const float TrackerAngleDeltaMax = 45f;
        // 0x04000d20 native offset 0x0
        private const float ProjectedMaxStepDistance = 1f;
        // 0x04000d21 native offset 0x0
        private const float ProjectedMaxStepDistanceSqr = 1f;
        // 0x04000d22 native offset 0x18
        private readonly Vector3 m_surfaceBoundsExpandAmount = new Vector3(0.01f, 0.2f, 0.01f);
        // 0x04000d23 native offset 0x28
        private readonly Tracker2D m_tracker;
        // 0x04000d24 native offset 0x30
        private Coroutine m_waitForTeleport;
        // 0x04000d25 native offset 0x38
        private bool m_lookAheadWasOnTracker;
        // 0x04000d26 native offset 0x39
        private bool m_lookAheadIsOnTracker;
        // 0x04000d27 native offset 0x3a
        private bool m_currentSurfaceReversed;
        // 0x04000d28 native offset 0x3c
        private Quaternion m_trackerWorldToLocal;
        // 0x04000d29 native offset 0x4c
        private Quaternion m_trackerLocalToWorld;
        // 0x04000d2a native offset 0x5c
        private float m_trackerLength;
        // 0x04000d2b native offset 0x60
        public Action<Character, SurfaceLocation, SurfaceLocation> OnSurfaceChanged;

        // Original 0x06001695 Hardlight.SurfaceLocation HardlightProject.CharacterTracker::get_TrackerLocation()
        public SurfaceLocation TrackerLocation => m_tracker.Location;

        // Original 0x06001696 UnityEngine.Vector3 HardlightProject.CharacterTracker::get_TrackerPosition()
        public Vector3 TrackerPosition => m_tracker.Location.m_worldPosition;

        // Original 0x06001697 UnityEngine.Vector3 HardlightProject.CharacterTracker::get_TrackerVelocityLocal()
        public Vector3 TrackerVelocityLocal => m_trackerWorldToLocal * m_character.WorldVelocity;

        // Original 0x06001698 System.Single HardlightProject.CharacterTracker::get_TrackerDistance()
        public float TrackerDistance => m_tracker.Location.m_localPosition.z;

        // Original 0x06001699 System.Single HardlightProject.CharacterTracker::get_TrackerLength()
        public float TrackerLength => m_trackerLength;

        // Original 0x0600169a System.Single HardlightProject.CharacterTracker::get_TrackerDistanceFromEnd()
        public float TrackerDistanceFromEnd => m_currentSurfaceReversed ? m_tracker.Location.m_localPosition.z : m_trackerLength - m_tracker.Location.m_localPosition.z;

        // Original 0x0600169b UnityEngine.Quaternion HardlightProject.CharacterTracker::get_TrackerRotation()
        public Quaternion TrackerRotation
        {
            get
            {
                Quaternion rotation = m_tracker.Location.m_worldRotation;
                return m_currentSurfaceReversed ? rotation * Actor.RotationReverseLocal : rotation;
            }
        }

        // Original 0x0600169c System.Boolean HardlightProject.CharacterTracker::get_SurfaceReversed()
        public bool SurfaceReversed => m_currentSurfaceReversed;

        // Original 0x0600169d System.Collections.Generic.IReadOnlyList`1<Hardlight.ISurface> HardlightProject.CharacterTracker::get_Surfaces()
        public IReadOnlyList<ISurface> Surfaces => m_tracker.Surfaces;

        // Original 0x0600169e System.Void HardlightProject.CharacterTracker::.ctor(HardlightProject.Character character, HardlightProject.TrackManager trackManager, HardlightProject.CustomGravity customGravity)
        public CharacterTracker(Character character, TrackManager trackManager, CustomGravity customGravity)
        {
            m_character = character;
            m_tracker = new Tracker2D(new CollisionResolver2D());
            trackManager.SetSurfaceTracker(m_tracker);
            m_tracker.OnTrackableSurfaceChange += OnTrackableSurfaceChange;
            m_tracker.Teleport(m_character.WorldPosition);
            customGravity.SetTracker(m_tracker);
        }

        // Original 0x0600169f System.Void HardlightProject.CharacterTracker::SetTrackableSurfaces(HardlightProject.TrackManager trackManager)
        public void SetTrackableSurfaces(TrackManager trackManager) { trackManager.SetSurfaceTracker(m_tracker); }

        // Original 0x060016a0 System.Void HardlightProject.CharacterTracker::Close(HardlightProject.TrackManager trackManager, HardlightProject.CustomGravity customGravity)
        public void Close(TrackManager trackManager, CustomGravity customGravity)
        {
            m_tracker.OnTrackableSurfaceChange -= OnTrackableSurfaceChange;
            customGravity.SetTracker(null);
        }

        // Original 0x060016a1 System.Void HardlightProject.CharacterTracker::Update(System.Single deltaTime)
        public void Update(float deltaTime) { } // Genuine native RET in both architectures.

        // Original 0x060016a2 System.Void HardlightProject.CharacterTracker::PostUpdate(System.Single deltaTime)
        public void PostUpdate(float deltaTime) { } // Genuine native RET in both architectures.

        // Original060016a3. Predicted step includes gravity, without publishing
        // that velocity to Character. Teleport search uses the initial position.
        public void UpdateOffTracker(float deltaTime)
        {
            Vector3 position = m_character.WorldPosition;
            Vector3 velocity = m_character.WorldVelocity;
            Vector3 step = (velocity + m_character.Gravity * deltaTime) * deltaTime;
            if (m_tracker.Location.m_surface == null)
            {
                float distance = step.magnitude;
                Ray ray = new Ray(position, step / distance);
                if (!m_tracker.TeleportIfHit(ray, distance))
                    return;
            }
            Vector3 nextPosition = position + step;
            Vector3 trackerStep = nextPosition - m_tracker.Location.m_worldPosition;
            float searchDistance = m_character.GetGravityMaxDistance();
            float previousHeight = DistanceOnNormal(position);
            if (trackerStep.sqrMagnitude > 1f || (searchDistance > 0f && (previousHeight < 0f || previousHeight > 1f)))
            {
                m_tracker.Teleport(position, searchDistance, null);
                SetLookAhead(m_tracker.Location.m_surface != null);
                return;
            }
            MoveTracker(trackerStep, true);
            if (m_tracker.Location.m_surface == null || m_lookAheadIsOnTracker)
                return;
            float nextHeight = DistanceOnNormal(nextPosition);
            if (!(previousHeight >= HeightOffsetMin) || !(nextHeight <= HeightLookAheadMax))
                return;
            Bounds bounds = m_tracker.Location.m_surface.GetBoundingBox(true);
            bounds.extents += m_surfaceBoundsExpandAmount * 0.5f;
            Vector3 projectedPosition = nextPosition - TrackerRotation * (Vector3.up * nextHeight);
            if (bounds.Contains(projectedPosition))
                SetLookAhead(true);
        }

        // Original060016a4. Velocity and current position precede lookahead;
        // CheckLookAhead receives the tracker captured before rotation is read.
        public void UpdateOnTrackerRibbon(float deltaTime)
        {
            Vector3 velocity = m_character.WorldVelocity;
            Vector3 nextPosition = m_character.WorldPosition + velocity * deltaTime;
            Tracker2D tracker = m_tracker;
            Vector3 stepWorld = nextPosition - tracker.Location.m_worldPosition;
            Quaternion rotation = TrackerRotation;
            SetLookAhead(CheckLookAheadIsOnTracker(tracker, stepWorld, rotation));
            MoveTracker(stepWorld, true);
            if (m_character.Settings.Surface.ClampToSurfaceBoundsOnGround)
                velocity = ClampVelocityToLateralBounds(velocity, m_tracker.Location);
            m_character.SetWorldVelocity(velocity);
            Character character = m_character;
            Vector3 trackerPosition = m_tracker.Location.m_worldPosition;
            Vector3 currentVelocity = character.WorldVelocity;
            character.SetWorldPosition(trackerPosition - currentVelocity * deltaTime);
        }

        // Original060016a5. Preserve the signed local displacement after either
        // move and the original division by deltaTime, including zero/NaN inputs.
        public void UpdateOnTrackerSpline(float deltaTime, bool orientateToWorldUp = false)
        {
            Vector3 worldStep = m_character.WorldVelocity * deltaTime;
            float localDistance = worldStep.magnitude;
            if (m_currentSurfaceReversed)
                localDistance = -localDistance;
            m_tracker.MoveLocal(new Vector3(0f, 0f, localDistance));
            UpdateCachedValues();
            bool atEndEdge = m_tracker.Location.m_positionBoundsInfo.AtEndEdge;
            SetLookAhead(!atEndEdge);
            if (atEndEdge)
            {
                m_tracker.MoveWorld(worldStep);
                UpdateCachedValues();
                atEndEdge = m_tracker.Location.m_positionBoundsInfo.AtEndEdge;
                SetLookAhead(!atEndEdge);
                if (atEndEdge)
                    return;
            }
            Vector3 trackerStep = m_tracker.Location.m_worldRotation * new Vector3(0f, 0f, localDistance);
            m_character.SetWorldVelocity(trackerStep / deltaTime);
            m_character.SetWorldPosition(m_tracker.Location.m_worldPosition - trackerStep);
            if (orientateToWorldUp)
            {
                Character character = m_character;
                Vector3 worldUp = character.WorldUp;
                character.OrientateToPlaneWithLookDirection(worldUp, m_character.WorldVelocityNormalised);
            }
            else
                m_character.SetWorldRotation(TrackerRotation);
        }

        // Original060016a6. Preserve the original signed distance for the world
        // fallback, even when the initial local movement reverses it.
        public void MoveTrackerByDistance(float distance)
        {
            m_tracker.MoveLocal(new Vector3(0f, 0f, m_currentSurfaceReversed ? -distance : distance));
            UpdateCachedValues();
            bool atEndEdge = m_tracker.Location.m_positionBoundsInfo.AtEndEdge;
            SetLookAhead(!atEndEdge);
            if (!atEndEdge)
                return;
            Vector3 step = m_tracker.Location.m_worldRotation * Vector3.forward * distance;
            m_tracker.MoveWorld(step);
            UpdateCachedValues();
            SetLookAhead(!m_tracker.Location.m_positionBoundsInfo.AtEndEdge);
        }

        // Original060016a7. Preserve the distance-decreasing search, undo,
        // captured plane before reversal, and ref publication/aliasing order.
        public bool UpdateJumpTracker(float step, ref Vector3 position, ref Quaternion rotation,
            ref Vector3 velocity, bool canTurnAround)
        {
            Vector3 trackerStep = position - m_tracker.Location.m_worldPosition;
            float previousDistanceSqr = trackerStep.sqrMagnitude;
            if (previousDistanceSqr > JumpTrackerDistanceCheckSqr)
            {
                MoveTracker(trackerStep, true);
                if (m_tracker.Location.m_positionBoundsInfo.AtEndEdge)
                    return false;
            }
            while (true)
            {
                MoveTrackerByDistance(step);
                if (!m_lookAheadIsOnTracker)
                    return false;
                float distanceSqr = (position - m_tracker.Location.m_worldPosition).sqrMagnitude;
                bool closer = distanceSqr < previousDistanceSqr;
                previousDistanceSqr = distanceSqr;
                if (!closer)
                    break;
            }
            MoveTrackerByDistance(-step);
            Vector3 trackerPosition = m_tracker.Location.m_worldPosition;
            Vector3 worldUp = m_character.WorldUp;
            Vector3 forward = TrackerRotation * Vector3.forward;
            Vector3 planeNormal = Vector3.Cross(worldUp, forward);
            Vector3 normalizedPlaneNormal = planeNormal.normalized;
            float planeDistance = Vector3.Dot(trackerPosition, normalizedPlaneNormal);
            if (canTurnAround)
                DetectSurfaceReversedFromVelocity(velocity);
            position -= normalizedPlaneNormal *
                (Vector3.Dot(position, normalizedPlaneNormal) - planeDistance);
            velocity = Vector3.ProjectOnPlane(velocity, planeNormal);
            Quaternion lookRotation = Quaternion.LookRotation(forward, worldUp);
            float angle = QuaternionExtensions.SignedAngle(lookRotation, TrackerRotation, forward);
            rotation = Quaternion.AngleAxis(-angle, m_character.ForwardDirection) * TrackerRotation;
            return true;
        }

        // Original 0x060016a8 System.Single HardlightProject.CharacterTracker::DistanceOnNormal(UnityEngine.Vector3 position)
        public float DistanceOnNormal(Vector3 position)
        {
            Vector3 trackerPosition = m_tracker.Location.m_worldPosition;
            Quaternion trackerRotation = TrackerRotation;
            return Vector3.Dot(position - trackerPosition, trackerRotation * Vector3.up);
        }

        // Original 0x060016a9 System.Boolean HardlightProject.CharacterTracker::LookAheadIsOnTracker()
        public bool LookAheadIsOnTracker() { return m_lookAheadIsOnTracker; }

        // Original 0x060016aa System.Boolean HardlightProject.CharacterTracker::LookAheadWasOnTracker()
        public bool LookAheadWasOnTracker() { return m_lookAheadWasOnTracker; }

        // Original 0x060016ab System.Void HardlightProject.CharacterTracker::SetLookAhead(System.Boolean lookAheadIsOnTracker)
        private void SetLookAhead(bool lookAheadIsOnTracker)
        {
            m_lookAheadWasOnTracker = m_lookAheadIsOnTracker;
            m_lookAheadIsOnTracker = lookAheadIsOnTracker;
        }

        // Original060016ac. Passed tracker supplies the first location only;
        // height, rotation, bounding box, and the final probe use this tracker.
        private bool CheckLookAheadIsOnTracker(Tracker2D tracker, Vector3 dStepWorld, Quaternion rotationLookAhead)
        {
            if (m_character.Settings.Surface.ClampToSurfaceBoundsOnGround)
            {
                PositionBoundsInfo boundsInfo = tracker.Location.m_positionBoundsInfo;
                if (boundsInfo.OnLateralBounds(m_character.LateralBoundsThreshold) && !boundsInfo.AtEndEdge)
                    return true;
            }
            Vector3 nextPosition = tracker.Location.m_worldPosition + dStepWorld;
            if ((tracker.Location.m_worldPosition - nextPosition).sqrMagnitude <
                dStepWorld.sqrMagnitude * TrackerWorldToleranceMultiplier)
                return false;
            float height = DistanceOnNormal(nextPosition);
            if (height < -HeightLookAheadMax || height > HeightLookAheadMax)
                return false;
            Vector3 axis = rotationLookAhead * Vector3.right;
            float angle = QuaternionExtensions.SignedAngle(rotationLookAhead, TrackerRotation, axis);
            if (Mathf.Abs(angle) > TrackerAngleDeltaMax)
                return false;
            Bounds bounds = m_tracker.Location.m_surface.GetBoundingBox(true);
            bounds.extents += m_surfaceBoundsExpandAmount * 0.5f;
            if (bounds.Contains(nextPosition))
                return true;
            Vector3 center = m_character.Bounds.center;
            Vector3 direction = nextPosition - center;
            Ray ray = new Ray(center, direction);
            return SurfacePhysics.Raycast(ray, direction.magnitude, m_tracker.Surfaces,
                out SurfaceLocation hitLocation, true);
        }

        // Original060016ad. Starting deferred teleport does not return here;
        // StartCoroutine may advance its first state before the handle is stored.
        public void UpdateColliderTracker(Vector3 velocity, AnimationCurve splineAngleInfluence, float deltaTime)
        {
            if (m_waitForTeleport != null)
                return;
            UpdateActiveTracker();
            if (GetPositionToTrackerHeading2D().sqrMagnitude > ColliderTrackerTeleportThreshold)
                m_waitForTeleport = m_character.StartCoroutine(DeferTrackerTeleportRoutine());
            float speed = velocity.magnitude;
            Vector3 heading = Vector3.ClampMagnitude(GetPositionToTrackerHeading2D(), speed);
            Vector3 stepWorld = velocity * deltaTime + heading;
            if (splineAngleInfluence.Evaluate(0f) < 1f)
            {
                MoveTracker(stepWorld, true);
                return;
            }
            float distance = stepWorld.magnitude;
            Vector3 direction = stepWorld / distance;
            Vector3 localDirection = Quaternion.Inverse(TrackerRotation) * direction;
            while (distance > TrackerMaxStepDistance)
            {
                MoveTracker(direction * TrackerMaxStepDistance, true);
                direction = TrackerRotation * localDirection;
                distance += -TrackerMaxStepDistance;
            }
            if (distance > 0f)
                MoveTracker(direction * distance, true);
        }

        // Original060016ae. LinkedCollisionData is a Unity component; the ribbon
        // reference uses a managed null check before its gameObject access.
        private void UpdateActiveTracker()
        {
            if (m_character.Collider.LinkedCollisionData == null)
                return;
            Ribbon ribbon = m_character.Collider.LinkedCollisionData.GetTrackerRibbon();
            GameObject ribbonObject = ribbon?.gameObject;
            GameObject surfaceObject = null;
            if (m_tracker.Location.m_surface is MonoBehaviour surfaceBehaviour)
                surfaceObject = surfaceBehaviour.transform.parent.gameObject;
            if (ribbonObject == null || surfaceObject == null || ribbonObject == surfaceObject)
                return;
            // Original calls Tracker2D directly: cached values and lookahead do
            // not refresh through the CharacterTracker.OnTeleport wrapper.
            m_tracker.Teleport(m_character.WorldPosition);
        }

        // Original 0x060016af UnityEngine.Vector3 HardlightProject.CharacterTracker::GetPositionToTrackerHeading2D()
        private Vector3 GetPositionToTrackerHeading2D()
        {
            Vector3 position = m_character.WorldPosition;
            Vector3 trackerPosition = m_tracker.Location.m_worldPosition;
            Quaternion trackerRotation = m_tracker.Location.m_worldRotation;
            Vector3 delta = position - trackerPosition;
            Vector3 normal = trackerRotation * Vector3.down;
            return delta - normal * Vector3.Dot(delta, normal);
        }

        // Original 0x060016b0 System.Collections.IEnumerator HardlightProject.CharacterTracker::DeferTrackerTeleportRoutine()
        private IEnumerator DeferTrackerTeleportRoutine()
        {
            OnTeleport(m_character.WorldPosition);
            yield return new WaitForFixedUpdate();
            m_waitForTeleport = null;
        }

        // Original 0x060016b1 System.Void HardlightProject.CharacterTracker::UpdateCachedValues()
        private void UpdateCachedValues()
        {
            m_trackerLocalToWorld = TrackerRotation;
            m_trackerWorldToLocal = Quaternion.Inverse(m_trackerLocalToWorld);
        }

        // Original 0x060016b2 System.Void HardlightProject.CharacterTracker::MoveTracker(UnityEngine.Vector3 direction, System.Boolean useWorldSpace)
        private void MoveTracker(Vector3 direction, bool useWorldSpace)
        {
            if (useWorldSpace) m_tracker.MoveWorld(direction);
            else m_tracker.MoveLocal(direction);
            UpdateCachedValues();
            SetLookAhead(!m_tracker.Location.m_positionBoundsInfo.AtEndEdge);
        }

        // Original 0x060016b3 System.Void HardlightProject.CharacterTracker::ApplyVerticalVelocity(System.Single verticalVelocity)
        public void ApplyVerticalVelocity(float verticalVelocity)
        {
            if (m_tracker.Location.m_surface != null)
            {
                Vector3 localVelocity = TrackerVelocityLocal;
                Character character = m_character;
                Quaternion localToWorld = m_trackerLocalToWorld;
                localVelocity.y = verticalVelocity;
                character.SetWorldVelocity(localToWorld * localVelocity);
            }
            if (verticalVelocity != 0f) SetLookAhead(false);
        }

        // Original060016b4. Left bounds take precedence and the threshold getter
        // is read again before testing the right bounds. Probe ignores this surface.
        public Vector3 ClampVelocityToLateralBounds(Vector3 velocity, SurfaceLocation surfaceLocation)
        {
            ISurface surface = surfaceLocation.m_surface;
            if (surface == null)
                return velocity;
            PositionBoundsInfo bounds = surfaceLocation.m_positionBoundsInfo;
            Vector3 boundPosition;
            if (bounds.OnLeftBounds(m_character.LateralBoundsThreshold))
                boundPosition = surface.TransformPoint(bounds.LeftPosition);
            else if (bounds.OnRightBounds(m_character.LateralBoundsThreshold))
                boundPosition = surface.TransformPoint(bounds.RightPosition);
            else
                return velocity;
            Vector3 outward = (boundPosition - surfaceLocation.m_worldPosition).normalized;
            Ray ray = new Ray(boundPosition + outward * 0.5f + Vector3.up, Vector3.down);
            if (!SurfacePhysics.Raycast(ray, ClampRaycastDistanceCheck, m_tracker.Surfaces,
                out RaycastHit hit, out ISurface hitSurface, surface, true))
                velocity -= GetVelocityAlongDirection(outward, velocity);
            return velocity;
        }

        // Original 0x060016b5 UnityEngine.Vector3 HardlightProject.CharacterTracker::GetVelocityAlongDirection(UnityEngine.Vector3 normalizedDirection, UnityEngine.Vector3 velocity)
        private Vector3 GetVelocityAlongDirection(Vector3 normalizedDirection, Vector3 velocity)
        {
            float speed = Vector3.Dot(normalizedDirection, velocity);
            // Ordered >= preserves native negative-zero and selects +0 for NaN.
            return normalizedDirection * (speed >= 0f ? speed : 0f);
        }

        // Original 0x060016b6 System.Void HardlightProject.CharacterTracker::OnMoveAway()
        public void OnMoveAway() { SetLookAhead(false); }

        // Original 0x060016b7 System.Void HardlightProject.CharacterTracker::OnTeleport(UnityEngine.Vector3 worldPosition)
        public void OnTeleport(Vector3 worldPosition)
        {
            m_tracker.Teleport(worldPosition);
            UpdateCachedValues();
            SetLookAhead(true);
        }

        // Original 0x060016b8 System.Void HardlightProject.CharacterTracker::OnTeleport(UnityEngine.Vector3 worldPosition, System.Single searchDistance, Hardlight.MetadataGroupKey key)
        public void OnTeleport(Vector3 worldPosition, float searchDistance, MetadataGroupKey key = null)
        {
            m_tracker.Teleport(worldPosition, searchDistance, key);
            UpdateCachedValues();
            SetLookAhead(true);
        }

        // Original060016b9. Retain the genuine metadata allocation, per-step
        // gravity reads, discarded LookRotation call, and outputs on failure.
        public bool GetProjectedSurfaceCollision(float maximumDistance, out Quaternion hitRotation, out float t)
        {
            CharacterDebug_Metadata.Metadata metadata = new CharacterDebug_Metadata.Metadata();
            Vector3 position = m_character.WorldPosition;
            Vector3 velocity = m_character.WorldVelocity;
            hitRotation = default;
            t = 0f;
            float distance = 0f;
            while (true)
            {
                bool inBounds = m_character.LevelBounds.Contains(position);
                bool withinDistance = distance < maximumDistance;
                if (!(inBounds & withinDistance))
                    return false;
                Vector3 step = Vector3.zero;
                metadata.Time = t;
                while (step.sqrMagnitude < ProjectedMaxStepDistanceSqr)
                {
                    t += 1f / 30f;
                    velocity += m_character.Gravity * (1f / 30f);
                    step += velocity * (1f / 30f);
                }
                Quaternion.LookRotation(step);
                float stepDistance = step.magnitude;
                Ray ray = new Ray(position, step / stepDistance);
                bool hit = SurfacePhysics.Raycast(ray, stepDistance, m_tracker.Surfaces,
                    out SurfaceLocation location, true);
                position += step;
                distance += stepDistance;
                if (hit)
                {
                    hitRotation = location.m_worldRotation;
                    return true;
                }
            }
        }

        // Original060016ba. The ray length is the captured gravity magnitude;
        // SurfacePhysics retains its own null/list/search behavior.
        public void ClampToLateralBoundsViaSurfaceRaycast()
        {
            if (!m_character.Settings.Surface.ClampToSurfaceBoundsInAir)
                return;
            Vector3 gravity = m_character.Gravity;
            Vector3 position = m_character.WorldPosition;
            Ray ray = new Ray(position, gravity);
            if (SurfacePhysics.Raycast(ray, gravity.magnitude, m_tracker.Surfaces, out SurfaceLocation location, true))
            {
                Vector3 velocity = m_character.WorldVelocity;
                velocity = ClampVelocityToLateralBounds(velocity, location);
                m_character.SetWorldVelocity(velocity);
            }
        }

        // Original060016bb. Raw ISurface identity, not Unity.Object equality.
        public void OnTrackingTypeChanged(CharacterTrackingType fromTrackingType, CharacterTrackingType toTrackingType)
        {
            ISurface previousSurface = m_tracker.Location.m_surface;
            if (fromTrackingType == CharacterTrackingType.ColliderFree && toTrackingType != CharacterTrackingType.ColliderFree)
            {
                m_tracker.Teleport(m_character.WorldPosition);
                if (previousSurface == m_tracker.Location.m_surface)
                    OnTrackableSurfaceChangeEvent(null, m_tracker.Location);
            }
            else if (fromTrackingType != CharacterTrackingType.ColliderFree && toTrackingType == CharacterTrackingType.ColliderFree)
                OnTrackableSurfaceChangeEvent(m_tracker.Location, null);
        }

        // Original060016bc. Disabled Character suppresses both notifications.
        // Delegate is read after the event bridge completes; exceptions propagate.
        private void OnTrackableSurfaceChange(SurfaceLocation fromLocation, SurfaceLocation toLocation)
        {
            if (!m_character.enabled)
                return;
            OnTrackableSurfaceChangeEvent(fromLocation, toLocation);
            OnSurfaceChanged?.Invoke(m_character, fromLocation, toLocation);
        }

        // Original060016bd. Exit happens before enter; length updates only for
        // a live Component surface. It remains unchanged when the next surface is
        // absent or is not a Component. SurfaceEvent callbacks may alter Character.
        private void OnTrackableSurfaceChangeEvent(SurfaceLocation? fromLocation, SurfaceLocation? toLocation)
        {
            Component fromComponent = fromLocation?.m_surface as Component;
            if (fromComponent != null)
            {
                SurfaceEvent surfaceEvent = fromComponent.GetComponentInParent<SurfaceEvent>();
                if (surfaceEvent != null)
                    surfaceEvent.OnSurfaceExit();
            }
            Component toComponent = toLocation?.m_surface as Component;
            if (toComponent == null)
                return;
            m_trackerLength = toLocation.Value.m_surface.Length;
            SurfaceEvent enterEvent = toComponent.GetComponentInParent<SurfaceEvent>();
            if (enterEvent != null)
                enterEvent.OnSurfaceEnter();
            Vector3 forward = toLocation.Value.m_worldRotation * Vector3.forward;
            if (Mathf.Abs(Vector3.Dot(forward, m_character.UpDirection)) > m_character.Constants.CharacterToSurfaceParallelCosineAngleThreshold)
            {
                bool wasReversed = m_currentSurfaceReversed;
                bool reversed = CharacterMovementUtilities.AreDirectionsReversed(m_character.WorldVelocityNormalised, forward);
                if (wasReversed != reversed)
                    TurnAround();
            }
            else
            {
                bool wasReversed = m_currentSurfaceReversed;
                Quaternion worldRotation = m_character.WorldRotation;
                bool reversed = CharacterMovementUtilities.AreRotationsReversed(worldRotation, toLocation.Value.m_worldRotation);
                if (wasReversed != reversed)
                    TurnAround();
            }
        }

        // Original 0x060016be System.Void HardlightProject.CharacterTracker::TurnAround()
        public void TurnAround() { m_currentSurfaceReversed = !m_currentSurfaceReversed; }

        // Original060016bf. GravityNormalised is a virtual getter and is called
        // twice; preserve both reads. Reversal uses the raw surface orientation.
        public void DetectSurfaceReversedFromVelocity(Vector3 velocity)
        {
            float vertical = Vector3.Dot(velocity, m_character.GravityNormalised);
            Vector3 direction = (velocity - vertical * m_character.GravityNormalised).normalized;
            if (direction == Vector3.zero)
                direction = m_character.ForwardDirection;
            Vector3 forward = m_tracker.Location.m_worldRotation * Vector3.forward;
            bool reversed = Vector3.Dot(direction, forward) <= -0.0001f;
            if (m_currentSurfaceReversed != reversed)
                TurnAround();
        }

        // Original060016c0. The initial inverted guard lets NaN magnitudes
        // reach the original Unity math. The final ordered comparison does not
        // toggle for an unordered dot or threshold.
        public void DetectSurfaceReversedFromInput(float inputToTrackerThreshold)
        {
            if (m_character.ControllerMovementMagnitude < 0.0001f)
                return;
            float angle = Vector2.SignedAngle(m_character.ControllerMovement, Vector2.up);
            Quaternion inputRotation = Quaternion.AngleAxis(angle, m_character.UpDirection);
            Vector3 direction = inputRotation * m_character.CameraForwardOnCharacterPlane;
            Vector3 trackerForward = TrackerRotation * Vector3.forward;
            if (Vector3.Dot(direction, trackerForward) < inputToTrackerThreshold)
                TurnAround();
        }

    }
}
