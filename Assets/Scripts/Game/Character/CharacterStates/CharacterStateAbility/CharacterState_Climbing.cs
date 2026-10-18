using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000365; bodies translated from the 1.10.1 x86-64 binary (shared generic
    // bodies at 0xE6C5F0..0xE6F680). Wall climbing: the character is pinned to a climbable surface,
    // moves along it in its local frame and is re-snapped every update.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterState_Climbing<TAbility, TAbilityDef> : CharacterState_Ability<TAbility>
        where TAbility : CharacterAbility_Climbing<TAbilityDef>
        where TAbilityDef : CharacterAbilityDefinition_Climbing
    {
        private const float SnapDistanceOnUpdate = 4f;
        private const float MinSnapDistanceOnUpdate = 0.02f;
        private const float SnapDistanceOnEnter = 1f;
        private const float SnapGroundCollisionBuffer = 0.0001f;
        private const float MinCheckDistanceOnMovingSurface = 1f;
        private readonly RaycastHit[] m_raycastHits = new RaycastHit[4];

        // 06001547
        protected CharacterState_Climbing(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 06001548. The non-mountable edge normals are emptied (or created); failing to snap onto the
        // surface still orients and snapshots, after flagging the edge as passed.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Character character = user.GetAs<Character>();
            TAbility ability = GetAbility(character);
            if (character.Storage.TryGetValue(ActorFSMKeys.ClimbingNonMountableEdgeNormal, out ICollection<Vector3> nonMountableEdgeNormals))
                nonMountableEdgeNormals.Clear();
            else
                character.Storage.SetValue(ActorFSMKeys.ClimbingNonMountableEdgeNormal, new List<Vector3>());
            ability.UpdateJumpAngleExtra(character.Storage);
            character.Storage.TryGetValue(ActorFSMKeys.ClimbingEnterCollisionNormal, out Vector3 enterCollisionNormal);
            if (!SnapPositionToSurface(character, ability, SnapDistanceOnEnter, enterCollisionNormal, out Vector3 surfaceNormal))
            {
                SnapshotPosition(character);
                FinishClimbing(character);
            }
            OrientateToSurface(character, surfaceNormal);
            SnapshotPosition(character);
            SetInitialVelocity(character);
        }

        // 06001549. The entry velocity is either the current one (when the flag, consumed here, asks to
        // maintain it) or the one recorded at terrain entry, as local velocity without its vertical part.
        private static void SetInitialVelocity(Character character)
        {
            if (character.Storage.TryGetValue(ActorFSMKeys.ClimbingMaintainCurrentVelocityOnEnter, out bool maintainVelocity))
                character.Storage.RemoveValue<bool>(ActorFSMKeys.ClimbingMaintainCurrentVelocityOnEnter);
            Vector3 worldVelocity = maintainVelocity
                ? character.WorldVelocity
                : character.Storage.GetValue(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter, default(Vector3), true);
            Vector3 localVelocity = character.WorldToLocalRotation * worldVelocity;
            localVelocity.y = 0f;
            character.SetLocalVelocity(localVelocity);
        }

        // 0600154a. Every climbing record is removed and the time of leaving is stored.
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            IGraphStorage storage = user.Storage;
            storage.RemoveValue<bool>(ActorFSMKeys.ClimbingEdgePassed);
            storage.RemoveValue<Vector3>(ActorFSMKeys.ClimbingLastKnownPosition);
            storage.RemoveValue<Quaternion>(ActorFSMKeys.ClimbingLastKnownRotation);
            storage.RemoveValue<Collider>(ActorFSMKeys.ClimbingLastCollider);
            storage.RemoveValue<Vector3>(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter);
            storage.RemoveValue<bool>(ActorFSMKeys.ClimbingInvalid);
            storage.GetValue<ICollection<Vector3>>(ActorFSMKeys.ClimbingNonMountableEdgeNormal, null, true).Clear();
            Character character = user.GetAs<Character>();
            TAbility ability = GetAbility(character);
            if (ability != null)
                ability.UpdateJumpAngleExtra(character.Storage, true);
            TimeManager timeManager = ProcessManager.GetSystem<TimeManager>(null, true);
            storage.SetValue(ActorFSMKeys.ClimbingLastTimestamp, timeManager.GetTotalTime(TimeCategory.PlayerMovement));
        }

        // 0600154b. A passed edge first restores the last known pose. When the character cannot be
        // snapped to a surface the climb is invalidated if an edge was passed, otherwise finished.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Character character = user.GetAs<Character>();
            TAbility ability = GetAbility(character);
            IGraphStorage storage = character.Storage;
            UpdateRemainTimer(character, updateContext.DeltaTime);
            bool edgePassed = storage.GetValue(ActorFSMKeys.ClimbingEdgePassed, false, true);
            if (edgePassed)
            {
                RestorePosition(character);
                storage.SetValue(ActorFSMKeys.ClimbingEdgePassed, false);
            }
            float deltaTime = updateContext.DeltaTime;
            float snapDistance = GetSnapDistanceOnUpdate(deltaTime, character.WorldVelocityMagnitude);
            if (SnapPositionToSurface(character, ability, snapDistance, character.UpDirection, out Vector3 surfaceNormal, deltaTime, true))
            {
                SnapshotPosition(character);
                UpdateVelocity(character, ability, deltaTime);
                UpdateAnimation(character, ability.Definition);
            }
            else if (edgePassed)
            {
                storage.SetValue(ActorFSMKeys.ClimbingInvalid, true);
            }
            else
            {
                FinishClimbing(character);
            }
        }

        // 0600154c. Lateral and vertical speeds follow the input. The velocity is the one whose step is
        // still on a climbable surface: the full step, else only the vertical one, else only the lateral
        // one, else none. Velocity into the average non-mountable edge normal is removed first.
        private void UpdateVelocity(Character character, TAbility ability, float deltaTime)
        {
            Vector3 localVelocity = character.LocalVelocity;
            ICollection<Vector3> edgeNormals = character.Storage.GetValue<ICollection<Vector3>>(ActorFSMKeys.ClimbingNonMountableEdgeNormal, null, true);
            if (edgeNormals.Count > 0)
            {
                Vector3 edgeNormal = Vector3.zero;
                foreach (Vector3 normal in edgeNormals)
                    edgeNormal += normal;
                edgeNormal /= edgeNormals.Count;
                edgeNormal = edgeNormal.normalized;
                edgeNormal = Vector3.ProjectOnPlane(edgeNormal, character.UpDirection).normalized;
                edgeNormal = character.WorldToLocalRotation * edgeNormal;
                localVelocity = Vector3.ProjectOnPlane(localVelocity, edgeNormal);
                edgeNormals.Clear();
            }
            float lateralSpeed = localVelocity.x;
            ability.UpdateLateralSpeed(ref lateralSpeed, deltaTime);
            float forwardSpeed = localVelocity.z;
            ability.UpdateVerticalSpeedWithLateralBias(ref forwardSpeed, lateralSpeed, deltaTime);
            Vector3 step = new Vector3(lateralSpeed, 0f, forwardSpeed) * deltaTime;
            // A moving surface checks at least one unit sideways, in the direction of the step.
            if (character.Collider.ConnectionTracker.HasMovement)
                step.x = step.x > 0f
                    ? Mathf.Max(step.x, MinCheckDistanceOnMovingSurface)
                    : Mathf.Min(step.x, -MinCheckDistanceOnMovingSurface);
            float speed = Mathf.Sqrt(lateralSpeed * lateralSpeed + forwardSpeed * forwardSpeed);
            float snapDistance = GetSnapDistanceOnUpdate(deltaTime, speed);
            Vector3 velocity = new Vector3(lateralSpeed, 0f, forwardSpeed);
            if (!IsPositionClimbable(character, character.UpDirection, step, snapDistance, ability, out RaycastHit hit, true))
            {
                if (IsPositionClimbable(character, character.UpDirection, new Vector3(0f, 0f, step.z), snapDistance, ability, out hit, true))
                {
                    velocity.x = 0f;
                }
                else
                {
                    velocity.z = 0f;
                    if (!IsPositionClimbable(character, character.UpDirection, new Vector3(step.x, 0f, 0f), snapDistance, ability, out hit, true))
                        velocity.x = 0f;
                }
            }
            character.SetLocalVelocity(velocity);
        }

        // 0600154d. The rotation towards the surface normal is limited to maxRotationDegrees; the character
        // looks away from gravity.
        private void OrientateToSurface(Character character, Vector3 surfaceNormal, float maxRotationDegrees = 180f)
        {
            Vector3 gravity = character.Gravity;
            float angle = Vector3.Angle(character.UpDirection, surfaceNormal);
            if (angle > maxRotationDegrees)
                surfaceNormal = Vector3.Slerp(character.UpDirection, surfaceNormal, maxRotationDegrees / angle);
            character.OrientateToPlaneWithLookDirection(surfaceNormal, -gravity);
        }

        // 0600154e. Succeeds when a climbable position lies within snapDistance. Moving onto a new collider
        // needs that collider to be climbable; the character is then placed SnapGroundCollisionBuffer behind the hit point along the surface
        // normal (just into the surface) and turned
        // to it unless the normal is unchanged.
        private bool SnapPositionToSurface(Character character, TAbility ability, float snapDistance, Vector3 currentNormal, out Vector3 surfaceNormal, float deltaTime = 0f, bool limitRotation = false)
        {
            CharacterCollisionData collisionData = null;
            if (IsPositionClimbable(character, currentNormal, Vector3.zero, snapDistance, ability, out RaycastHit hit, limitRotation))
            {
                if (character.Storage.GetValue<Collider>(ActorFSMKeys.ClimbingLastCollider, null, true) != hit.collider)
                {
                    if (!character.TrackManager.TryGetCharacterCollisionData(hit.collider, out collisionData) || !collisionData.IsClimbable)
                    {
                        surfaceNormal = Vector3.zero;
                        return false;
                    }
                    character.Storage.SetValue(ActorFSMKeys.ClimbingLastCollider, hit.collider);
                }
                surfaceNormal = hit.normal;
                character.SetWorldPosition(hit.point - surfaceNormal * SnapGroundCollisionBuffer);
                if (hit.normal == currentNormal)
                    return true;
                float maxRotationDegrees = limitRotation ? deltaTime * ability.Definition.RotationSpeed : 180f;
                OrientateToSurface(character, surfaceNormal, maxRotationDegrees);
                return true;
            }
            surfaceNormal = Vector3.zero;
            return false;
        }

        // 0600154f. Casts from the character position lifted snapDistance along the contact normal (moved by
        // the offset, which is local to the character) back through it over twice that distance
        // against the surface and takes the first hit that is within the climbing angles, within the
        // allowed angle change when rotation is limited, and on a climbable collider. Without such a hit
        // the output is the first raycast slot.
        private bool IsPositionClimbable(Character character, Vector3 currentContactNormal, Vector3 characterLocalOffset, float snapDistance, TAbility ability, out RaycastHit hit, bool limitRotation)
        {
            CharacterCollisionData collisionData = null;
            Vector3 origin = character.WorldPosition + currentContactNormal * snapDistance;
            if (characterLocalOffset != Vector3.zero)
                origin += character.WorldRotation * characterLocalOffset;
            int hitCount = Physics.RaycastNonAlloc(origin, -currentContactNormal, m_raycastHits, snapDistance * 2f, ability.Definition.TrackMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                hit = m_raycastHits[i];
                if (!IsClimbingAngleValid(ability, character, hit.normal))
                    continue;
                if (limitRotation && ability.Definition.MaxAngleChangeCosine > Vector3.Dot(hit.normal, currentContactNormal))
                    continue;
                if (!character.TrackManager.TryGetCharacterCollisionData(hit.collider, out collisionData) || !collisionData.IsClimbable)
                    continue;
                return true;
            }
            hit = m_raycastHits[0];
            return false;
        }

        // 06001550
        private void FinishClimbing(Character character)
        {
            character.Storage.SetValue(ActorFSMKeys.ClimbingEdgePassed, true);
        }

        // 06001551
        private bool IsClimbingAngleValid(TAbility ability, Character character, Vector3 surfaceNormal)
        {
            return ability.IsClimbingAngleValid(surfaceNormal, character.GravityNormalised);
        }

        // 06001552
        private void SnapshotPosition(Character character)
        {
            character.Storage.SetValue(ActorFSMKeys.ClimbingLastKnownPosition, character.WorldPosition);
            character.Storage.SetValue(ActorFSMKeys.ClimbingLastKnownRotation, character.WorldRotation);
        }

        // 06001553. A missing snapshot restores the zero position and the all-zero rotation.
        private void RestorePosition(Character character)
        {
            character.SetWorldPosition(character.Storage.GetValue(ActorFSMKeys.ClimbingLastKnownPosition, default(Vector3), true));
            character.SetWorldRotation(character.Storage.GetValue(ActorFSMKeys.ClimbingLastKnownRotation, default(Quaternion), true));
        }

        // 06001554
        private float GetSnapDistanceOnUpdate(float deltaTime, float velocity)
        {
            return Mathf.Max(deltaTime * SnapDistanceOnUpdate * velocity, MinSnapDistanceOnUpdate);
        }

        // 06001555. The climb speeds are the local speeds as fractions of the definition's maxima (a
        // negative vertical speed, or an unordered one, uses the downward maximum).
        private void UpdateAnimation(Character character, TAbilityDef definition)
        {
            ActorAnimator animator = character.Animator;
            float lateralMax = definition.LateralMotion.SpeedMax;
            animator.TrySetFromAnimationType(m_stateAnimationDefinition, ActorAnimationType.ClimbSpeedX,
                Mathf.Clamp(character.LocalVelocity.x, -lateralMax, lateralMax) / lateralMax);
            float verticalSpeed = character.LocalVelocity.z;
            float verticalMax = verticalSpeed >= 0f ? definition.VerticalMotion.SpeedMaxUpward : definition.VerticalMotion.SpeedMaxDownward;
            animator.TrySetFromAnimationType(m_stateAnimationDefinition, ActorAnimationType.ClimbSpeedY, verticalSpeed / verticalMax);
        }
    }
}
