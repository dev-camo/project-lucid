using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200076d; complete16 owner + LockRoutineInfo1 + iterator6.
    // The generic native captures identify Boolean instantiations; generic source binding remains unverified.
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    public class RigidbodyForceTrigger : MonoBehaviour
    {
        // Original 0x04001b8e.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Time category for modifier override durations.")]
        private TimeCategory m_timeCategory = TimeCategory.PlayerPhysics;
        // Original 0x04001b8f.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Entering collider's tag will be checked against this. Set to empty to allow all.")]
        private string m_colliderTagToCheck = "Player";
        // Original 0x04001b90.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Multiplier for the resultant force after direction, force mode, and force type are applied.")]
        protected float m_power;
        // Original 0x04001b91.
        [Hardlight.DisableIfAttribute("m_positionSnapToPlane", null)]
        [UnityEngine.SerializeField()]
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("m_moveToThisOrigin")]
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("m_moveToSpringOrigin")]
        [UnityEngine.TooltipAttribute("Entering Rigidbody will be moved to transform's origin.")]
        private bool m_positionSnapToOrigin;
        // Original 0x04001b92.
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("m_moveToThisPlane")]
        [UnityEngine.TooltipAttribute("Entering Rigidbody will be moved to transform's plane.")]
        [Hardlight.DisableIfAttribute("m_positionSnapToOrigin", null)]
        [UnityEngine.SerializeField()]
        private bool m_positionSnapToPlane;
        // Original 0x04001b93.
        [Hardlight.EnableIfAttribute((Hardlight.InspectorConditionalAttribute.ComparisonType)1, true, new string[] { "m_positionSnapToOrigin", "m_positionSnapToPlane" })]
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("If a rail falls within the collision trigger's bounds, the closest location will be used as the transform.")]
        private bool m_positionSnapToRail;
        // Original 0x04001b94.
        [UnityEngine.SerializeField()]
        [Hardlight.EnableIfAttribute((Hardlight.InspectorConditionalAttribute.ComparisonType)1, true, new string[] { "m_positionSnapToOrigin", "m_positionSnapToPlane" })]
        [UnityEngine.TooltipAttribute("Transform override for snap to location.")]
        private Transform m_positionSnapTransform;
        // Original 0x04001b95.
        [UnityEngine.TooltipAttribute("The local offset from the snap position.")]
        [UnityEngine.SerializeField()]
        [Hardlight.EnableIfAttribute((Hardlight.InspectorConditionalAttribute.ComparisonType)1, true, new string[] { "m_positionSnapToOrigin", "m_positionSnapToPlane" })]
        private Vector3 m_positionSnapToOffsetLocal;
        // Original 0x04001b96.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Additive - Add force to current velocity.\nDirect - Override current velocity with force.\nReflect - Reflect current velocity and then add force.")]
        private ForceType m_forceType;
        // Original 0x04001b97.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Triggered when a valid rigidbody enters the trigger volume")]
        private UnityEvent m_onTriggerEvent;
        // Original 0x04001b98.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Force mode only applies for Additive and Reflect types.")]
        private ForceMode m_forceMode = ForceMode.Impulse;
        // Original 0x04001b99.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Direction the force will be applied in.")]
        private DirectionType m_forceDirectionType;
        // Original 0x04001b9a.
        [UnityEngine.SerializeField()]
        private bool m_manualTrigger;
        // Original 0x04001b9b.
        [UnityEngine.SerializeField()]
        private float m_resultantForceMultiplier = 1f;
        // Original 0x04001b9c.
        [UnityEngine.SerializeField()]
        [UnityEngine.TooltipAttribute("Direction will be reversed if incoming body is coming from the opposite direction.")]
        private bool m_biDirectional;
        // Original 0x04001b9d.
        [Hardlight.ShowIfAttribute("m_forceType", (HardlightProject.RigidbodyForceTrigger.ForceType)1)]
        [UnityEngine.SerializeField()]
        private bool m_maintainXWithDirect;
        // Original 0x04001b9e.
        [UnityEngine.SerializeField()]
        [Hardlight.ShowIfAttribute("m_forceType", (HardlightProject.RigidbodyForceTrigger.ForceType)1)]
        private bool m_maintainYWithDirect;
        // Original 0x04001b9f.
        [UnityEngine.SerializeField()]
        [Hardlight.ShowIfAttribute("m_forceType", (HardlightProject.RigidbodyForceTrigger.ForceType)1)]
        private bool m_maintainZWithDirect;
        // Original 0x04001ba0.
        [UnityEngine.SerializeField()]
        [Hardlight.ShowIfAttribute("m_forceDirectionType", (HardlightProject.RigidbodyForceTrigger.DirectionType)8)]
        private Vector3 m_worldSerialisedDirection;
        // Original 0x04001ba1.
        [UnityEngine.Serialization.FormerlySerializedAsAttribute("m_speedLockTime")]
        [UnityEngine.TooltipAttribute("If rigidbody has valid IGameplayModifier component then tell it to override the velocity for this amount of seconds.\nIgnored if set to 0")]
        [UnityEngine.HeaderAttribute("Gameplay modifiers")]
        [UnityEngine.MinAttribute(0f)]
        [UnityEngine.SerializeField()]
        private float m_velocityOverrideTime;
        // Original 0x04001ba2.
        [UnityEngine.TooltipAttribute("If rigidbody has valid IGameplayModifier component then tell it to override the speed for this amount of seconds.\nIgnored if set to 0")]
        [UnityEngine.MinAttribute(0f)]
        [UnityEngine.SerializeField()]
        private float m_speedOverrideTime;
        // Original 0x04001ba3.
        [UnityEngine.TooltipAttribute("If rigidbody has valid IGameplayModifier component then tell it to lock the controls for this amount of seconds.\nIgnored if set to 0")]
        [UnityEngine.MinAttribute(0f)]
        [UnityEngine.SerializeField()]
        private float m_controlLockTime;
        // Original 0x04001ba4.
        [UnityEngine.TooltipAttribute("If rigidbody has valid IGameplayModifier component then tell it to reset the current body's grip for the duration of the override.")]
        [UnityEngine.SerializeField()]
        private bool m_gripReset = true;
        // Original 0x04001ba5.
        [UnityEngine.TooltipAttribute("If rigidbody is attached to the character, reset sticky controls if they are currently active.")]
        [UnityEngine.SerializeField()]
        private bool m_stickyControlsReset;
        // Original 0x04001ba6.
        [UnityEngine.TooltipAttribute("If set, character's targeting is disabled for the specified time in seconds.  Ignored if set to 0.")]
        [UnityEngine.MinAttribute(0f)]
        [UnityEngine.SerializeField()]
        private float m_targetingDisableTime;
        // Original 0x04001ba7.
        private readonly List<LockRoutineInfo> m_lockRoutines = new List<LockRoutineInfo>();
        // Original 0x04001ba8.
        private Collider m_colliderTrigger;
        // Original 0x04001ba9.
        private float m_colliderProximityDistance;
        // Original 0x04001baa.
        private static SurfaceLocation s_railLocation;
        // Original 0x04001bab.
        public static Action OnCharacterPositionSnap;

        // Original nested enums0200076e/0200076f; exact serialized Int32 values.
        private enum ForceType { Additive = 0, Direct = 1, Reflect = 2 }
        private enum DirectionType
        {
            SelfUp = 0, SelfDown = 1, SelfForward = 2, SelfRight = 3,
            SelfBack = 4, SelfLeft = 5, AbsoluteWorldUp = 6, AbsoluteWorldDown = 7,
            AbsoluteWorldSerialised = 8, IncomingBodyForward = 9, AwayFromCentre = 10
        }
        // Original02000770/060029d4: three fields, ordinary Object constructor then assignment.
        private class LockRoutineInfo
        {
            public readonly IGameplayModifiable Modifiable;
            public StackableDataHandle Handle;
            public Coroutine Coroutine;
            public LockRoutineInfo(IGameplayModifiable modifiableComponent) { Modifiable = modifiableComponent; }
        }

        // Original060029c4: failed collider lookup still reaches the bounds getter.
        private void Awake()
        {
            if (!m_positionSnapToRail) return;
            if (TryGetComponent(out m_colliderTrigger)) _ = m_colliderTrigger.isTrigger;
            Vector3 size = m_colliderTrigger.bounds.size;
            m_colliderProximityDistance = Mathf.Max(Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)), Mathf.Abs(size.z));
        }
        // Original060029c5: enabled getter precedes the manual-trigger test.
        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || m_manualTrigger) return;
            TryTrigger(other);
        }
        // Original060029c6 uses the actual Actor ColliderCollision field/getter.
        public void TryTrigger(Character character) { TryTrigger(character.ColliderCollision); }
        // Original060029c7: attachedRigidbody is read again after its Unity-null check.
        public void TryTrigger(Collider other)
        {
            if (!string.IsNullOrEmpty(m_colliderTagToCheck) && !other.CompareTag(m_colliderTagToCheck)) return;
            if (other.attachedRigidbody == null) return;
            ApplyToRigidbody(other.attachedRigidbody);
            m_onTriggerEvent.Invoke();
        }
        // Original060029c8: real Rigidbody/Character graph, sequential getters and modifiers preserved.
        private void ApplyToRigidbody(Rigidbody body)
        {
            EndActiveRoutines();
            bool isCharacter = body.TryGetComponent(out Character character);
            ApplyPositionSnap(body, isCharacter, character);
            float timeScale = isCharacter ? character.GetTimeScale() : 1f;
            Vector3 direction = GetForceDirection(body.transform.forward, body.velocity, body.position);
            Vector3 force = direction * (timeScale * m_power);
            Vector3 predictedVelocity;
            switch (m_forceType)
            {
                case ForceType.Additive:
                    predictedVelocity = body.velocity + force;
                    body.AddForce(force, m_forceMode);
                    break;
                case ForceType.Reflect:
                    Vector3 reflectedVelocity = Vector3.Reflect(body.velocity, direction);
                    body.velocity = reflectedVelocity;
                    predictedVelocity = reflectedVelocity + force;
                    body.AddForce(force, m_forceMode);
                    break;
                case ForceType.Direct:
                    Vector3 up = transform.up;
                    Vector3 horizontal = Vector3.ProjectOnPlane(body.velocity, up);
                    if (horizontal.sqrMagnitude < 0.0001f) horizontal = body.transform.forward;
                    Quaternion rotation = Quaternion.LookRotation(horizontal, up);
                    Quaternion inverse = Quaternion.Inverse(rotation);
                    Vector3 localForce = inverse * force;
                    Vector3 localVelocity = inverse * body.velocity;
                    if (m_maintainXWithDirect) localForce.x = localVelocity.x;
                    if (m_maintainYWithDirect) localForce.y = localVelocity.y;
                    if (m_maintainZWithDirect) localForce.z = localVelocity.z;
                    predictedVelocity = rotation * localForce;
                    body.velocity = predictedVelocity;
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
            body.velocity = body.velocity * m_resultantForceMultiplier;
            IGameplayModifiable modifiable = body.GetComponent<IGameplayModifiable>();
            if (modifiable == null) return;
            Vector3 velocityOverride = predictedVelocity * (m_resultantForceMultiplier / timeScale);
            AddModifierOverride(modifiable, unchecked((int)0xc22df46d), false, m_controlLockTime);
            // These two authentic modifier integers have no shipped GameplayModifierType member.
            AddModifierOverride(modifiable, unchecked((int)0xbbcf6556), velocityOverride, m_velocityOverrideTime);
            AddModifierOverride(modifiable, 0x078dc3b9, velocityOverride.magnitude, m_speedOverrideTime);
            if (m_gripReset) AddModifierOverride(modifiable, 0x08f0c399, true, GetOverrideTime());
            if (!isCharacter) return;
            if (m_stickyControlsReset)
            {
                if (character.Storage.GetValueOnly(ActorFSMKeys.TurnCameraActive, false))
                    character.Storage.GetValueOnly<CharacterStickyControls>(ActorFSMKeys.TurnCameraStickyControls, null)?.DeactivateStickyControls(character);
                AddModifierOverride(modifiable, unchecked((int)0x8a23d40a), false, GetOverrideTime());
            }
            if (m_targetingDisableTime > 0f)
            {
                character.HomingPool.ClearTarget(true);
                AddModifierOverride(modifiable, unchecked((int)0x9d7e35f5), false, m_targetingDisableTime);
            }
        }
        // Original060029c9: notify even when snapping is disabled or movement will be blocked.
        private void ApplyPositionSnap(Rigidbody body, bool isCharacter, Character character)
        {
            if (isCharacter) OnCharacterPositionSnap?.Invoke();
            if (!m_positionSnapToOrigin && !m_positionSnapToPlane) return;
            if (isCharacter && !CanMovePosition(character)) return;
            Vector3 position;
            Vector3 up;
            if (isCharacter && m_positionSnapToRail && TryGetRailHit(body.position, character.TrackManager))
            {
                position = s_railLocation.m_worldPosition + s_railLocation.m_worldRotation * m_positionSnapToOffsetLocal;
                up = s_railLocation.m_worldRotation * Vector3.up;
            }
            else
            {
                Transform snapTransform = m_positionSnapTransform != null ? m_positionSnapTransform : transform;
                position = snapTransform.position + snapTransform.rotation * m_positionSnapToOffsetLocal;
                up = snapTransform.up;
            }
            if (!m_positionSnapToOrigin)
            {
                float distance = Vector3.Dot(up, body.position - position);
                position = body.position - up * distance;
            }
            body.position = position;
        }
        // Original060029ca: real ref SurfaceLocation API, a second collider bounds getter, then edge exclusion.
        private bool TryGetRailHit(Vector3 bodyPosition, TrackManager trackManager)
        {
            return trackManager.TryGetRailHit(bodyPosition, m_colliderTrigger.bounds, m_colliderProximityDistance, ref s_railLocation)
                && m_colliderTrigger.bounds.Contains(s_railLocation.m_worldPosition)
                && !s_railLocation.m_positionBoundsInfo.AtEndEdge;
        }
        // Original060029cb: ordered two-argument maxima retain native second-operand NaN selection.
        private float GetOverrideTime() { return Mathf.Max(Mathf.Max(m_controlLockTime, m_velocityOverrideTime), m_speedOverrideTime); }
        // Original060029cc: the RailActive storage query intentionally stores its default.
        private bool CanMovePosition(Character character)
        {
            if (m_targetingDisableTime > 0f) return true;
            return !character.Storage.GetValue(ActorFSMKeys.RailActive, false, true);
        }
        // Original060029cd: initial count captured once, live list indexing during reverse removal.
        private void EndActiveRoutines()
        {
            for (int i = m_lockRoutines.Count - 1; i >= 0; i--) EndLockRoutine(m_lockRoutines[i]);
        }
        // Original060029ce: zero alone is skipped; negative and NaN waits start. Start precedes list append.
        private void AddModifierOverride<T>(IGameplayModifiable modifiableComponent, int modifierType, T value, float waitSeconds)
        {
            if (waitSeconds == 0f) return;
            LockRoutineInfo routineInfo = new LockRoutineInfo(modifiableComponent);
            routineInfo.Coroutine = CoroutineUtils.RunCoroutine(AddModifierOverrideCoroutine(routineInfo, modifierType, value, waitSeconds));
            m_lockRoutines.Add(routineInfo);
        }
        // Original060029cf plus iterator060029d5..29da: one yield, terminal state before EndLockRoutine, no finally.
        private IEnumerator AddModifierOverrideCoroutine<T>(LockRoutineInfo routineInfo, int modifierType, T value, float waitSeconds)
        {
            routineInfo.Handle = routineInfo.Modifiable.AddModifierOverride(modifierType, value);
            yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(waitSeconds, m_timeCategory, routineInfo.Modifiable.AreModifiersBlocked);
            EndLockRoutine(routineInfo);
        }
        // Original060029d0: remove tracking before provider callback and host coroutine stop.
        private void EndLockRoutine(LockRoutineInfo lockRoutineInfo)
        {
            m_lockRoutines.Remove(lockRoutineInfo);
            lockRoutineInfo.Modifiable.RemoveModifierOverrides(lockRoutineInfo.Handle);
            CoroutineUtils.StopUtilCoroutine(ref lockRoutineInfo.Coroutine);
        }
        // Original060029d1: eleven native jump-table cases; input-forward and away directions bypass bidirectional flip.
        private Vector3 GetForceDirection(Vector3 bodyForward, Vector3 bodyVelocity, Vector3 bodyPosition)
        {
            Vector3 direction;
            switch (m_forceDirectionType)
            {
                case DirectionType.SelfUp: direction = transform.up; break;
                case DirectionType.SelfDown: direction = -transform.up; break;
                case DirectionType.SelfForward: direction = transform.forward; break;
                case DirectionType.SelfRight: direction = transform.right; break;
                case DirectionType.SelfBack: direction = -transform.forward; break;
                case DirectionType.SelfLeft: direction = -transform.right; break;
                case DirectionType.AbsoluteWorldUp: direction = Vector3.up; break;
                case DirectionType.AbsoluteWorldDown: direction = Vector3.down; break;
                case DirectionType.AbsoluteWorldSerialised: direction = m_worldSerialisedDirection; break;
                case DirectionType.IncomingBodyForward: direction = bodyForward; break;
                case DirectionType.AwayFromCentre: direction = (bodyPosition - transform.position).normalized; break;
                default: throw new ArgumentOutOfRangeException();
            }
            if (m_biDirectional && m_forceDirectionType != DirectionType.IncomingBodyForward && m_forceDirectionType != DirectionType.AwayFromCentre)
                if (Vector3.Dot(direction, bodyVelocity.normalized) < 0f) direction = -direction;
            return direction;
        }
        // Original060029d2; implicit public060029d3 constructor initializes authored defaults before MonoBehaviour.
        public void SetAsManualTrigger() { m_manualTrigger = true; }

    }
}
