using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime type 02000329. Scene-authored events for the current character entering,
    // staying in and leaving the volume, plus the actions those events call back on the character.
    // The compiler-generated names follow from the 23 fields (19 serialized, 4 runtime) declared before the first method.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterEvents : CharacterTrigger
    {
        [SerializeField]
        [Tooltip("Events triggered on component enable.")]
        private UnityEvent m_onTriggerEnableEvents;
        [Tooltip("Events triggered on component disable.")]
        [SerializeField]
        private UnityEvent m_onTriggerDisableEvents;
        [SerializeField]
        [Tooltip("Events triggered on entering a collision.")]
        private UnityEvent<Character> m_onTriggerEnterEvents;
        [Tooltip("Events triggered when staying in a collision.")]
        [SerializeField]
        private UnityEvent<Character> m_onTriggerStayEvents;
        [Tooltip("Delay to triggering stay events.")]
        [SerializeField]
        private float m_onTriggerStayDelayTime;
        [SerializeField]
        [Tooltip("Events triggered when staying in a collision but delayed the set time.")]
        private UnityEvent<Character> m_onTriggerStayDelayedEvents;
        [SerializeField]
        [Tooltip("Events triggered on exiting a collision.")]
        private UnityEvent<Character> m_onTriggerExitEvents;
        [SerializeField]
        [Tooltip("Events triggered on taking damage.")]
        private UnityEvent m_onTriggerTakeDamageEvents;
        [Tooltip("Events triggered on level activation.")]
        [SerializeField]
        private UnityEvent m_onTriggerLevelActivatedEvents;
        [SerializeField]
        [Tooltip("Converts character's current speed to a normalised value sent through in the events.")]
        private AnimationCurve m_speedCurve = AnimationCurve.Linear(0f, 0f, 100f, 100f);
        [SerializeField]
        [Tooltip("Events to send through character speed normalised values.")]
        private UnityEvent<float> m_onTriggerSpeedEvents;
        [Tooltip("Optional transform to target.  If not set target will be this game object's transform.")]
        [SerializeField]
        private Transform m_targetTransform;
        [Tooltip("Whether to target using the camera or character current forward direction.")]
        [SerializeField]
        private bool m_targetUseCameraForward;
        [SerializeField]
        [Tooltip("Sets target location to closest point on .")]
        private bool m_targetUseClosestPoint;
        [Tooltip("Distance to raycast to find target hit location.")]
        [SerializeField]
        private float m_targetRaycastDistance = 100f;
        [SerializeField]
        [Tooltip("Angle from forward direction to raycast to find target hit location.")]
        private float m_targetRaycastAngle = 45f;
        [Tooltip("Orientates target to the nearest ribbon location.")]
        [SerializeField]
        private bool m_targetUseRibbonOrientation;
        [SerializeField]
        [Tooltip("The abilities to use for events called on the character.")]
        private List<ActorAbilityType> m_abilityTypes = new List<ActorAbilityType>();
        [SerializeField]
        [Tooltip("Scriptable object for the abilities to use for events called on the character.")]
        private CharacterAbilityTypeGroup m_abilityTypeGroup;
        private SystemRef<LevelManager> m_levelManagerRef;
        private StackableDataHandle m_controlsHandle;
        private StackableDataHandle m_targetingHandle;
        private Coroutine m_onTriggerStayDelayCoroutine;

        // 060013ae; the level and damage events are wired only when they have persistent listeners.
        protected override void Awake()
        {
            base.Awake();
            if (m_onTriggerLevelActivatedEvents.GetPersistentEventCount() > 0)
            {
                m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
                m_levelManagerRef.InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated, true));
            }
            if (m_onTriggerTakeDamageEvents.GetPersistentEventCount() > 0)
                m_characterManagerRef.InvokeOnValid(characterManager => characterManager.OnCharacterTakeDamage += OnCharacterTakeDamage);
        }

        // 060013af
        private void OnDestroy()
        {
            if (m_onTriggerLevelActivatedEvents.GetPersistentEventCount() > 0 && m_levelManagerRef.IsValid())
                m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
            if (m_onTriggerTakeDamageEvents.GetPersistentEventCount() > 0 && m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().OnCharacterTakeDamage -= OnCharacterTakeDamage;
        }

        // 060013b0
        private void OnEnable()
        {
            m_onTriggerEnableEvents?.Invoke();
        }

        // 060013b1
        private void OnDisable()
        {
            m_onTriggerDisableEvents?.Invoke();
        }

        // 060013b2
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            m_onTriggerEnterEvents?.Invoke(character);
        }

        // 060013b3; the delayed stay events are scheduled once per visit and only with a delay time.
        protected override void DoTriggerStay(Character character)
        {
            base.DoTriggerStay(character);
            m_onTriggerStayEvents?.Invoke(character);
            if (m_onTriggerStayDelayTime == 0f || m_onTriggerStayDelayCoroutine != null)
                return;
            m_onTriggerStayDelayCoroutine = CoroutineUtils.Delay(() => DoTriggerStayDelayed(character), m_onTriggerStayDelayTime);
        }

        // 060013b4
        private void DoTriggerStayDelayed(Character character)
        {
            m_onTriggerStayDelayedEvents?.Invoke(character);
        }

        // 060013b5; leaving only forgets the pending delayed coroutine, it is not stopped.
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            m_onTriggerExitEvents?.Invoke(character);
            m_onTriggerStayDelayCoroutine = null;
        }

        // 060013b6
        private void OnLevelActivated(LevelManagerLevel level)
        {
            m_onTriggerLevelActivatedEvents?.Invoke();
        }

        // 060013b7
        private void OnCharacterTakeDamage()
        {
            m_onTriggerTakeDamageEvents?.Invoke();
        }

        // 060013b8
        public void AirAbilitiesExit()
        {
            Character.EndAirActiveImpulse();
        }

        // 060013b9
        public void AirAbilitiesExit(Character character)
        {
            character.EndAirActiveImpulse();
        }

        // 060013ba
        public void AirAbilitiesReset()
        {
            AirAbilitiesReset(Character);
        }

        // 060013bb; every air ability leaves, every homing ability resets and the spin dash charge is
        // dropped before the reset is published to the character's storage.
        public void AirAbilitiesReset(Character character)
        {
            if (character == null)
                return;
            character.GetAbilitiesOfType<CharacterAbility_Air>(ability => ability.Leave());
            character.GetAbilitiesWithInterface<IHomingAbility>(ability => ability.Reset());
            character.SpinDashRoll.DisableCharge();
            character.Storage.SetValue<bool>(ActorFSMKeys.AirAbilitiesReset, true);
        }

        // 060013bc
        public void AirAbilitiesRecharge(bool isExit)
        {
            AirAbilitiesRecharge(Character, isExit);
        }

        // 060013bd
        public void AirAbilitiesRecharge(Character character, bool isExit)
        {
            character.GetAbilitiesOfType<CharacterAbility_Air>(ability => ability.Recharge(isExit));
        }

        // 060013be
        public void ResetSpinDashCharge()
        {
            Character.SpinDashRoll.DisableCharge();
        }

        // 060013bf
        public void ResetSpinDashCharge(Character character)
        {
            character.SpinDashRoll.DisableCharge();
        }

        // 060013c0
        public void ResetCharacterForm()
        {
            Character.SwitchToForm(ActorFormType.Default, false, true);
        }

        // 060013c1
        public void TryResetCharacterForm()
        {
            Character.SwitchToForm(ActorFormType.Default, false, false);
        }

        // 060013c2; disabling holds a ControlsEnabled=false override and enabling releases it. Enabling
        // without a held override reaches the release with a null handle.
        public void SetControlsEnabled(bool enable)
        {
            Character character = Character;
            if (enable)
            {
                character.RemoveModifierOverrides(m_controlsHandle);
                m_controlsHandle = null;
            }
            else
            {
                m_controlsHandle = character.AddModifierOverride((int)GameplayModifierType.ControlsEnabled, false);
            }
        }

        // 060013c3; the same shape for the TargetingAvailable override, but a repeated call changes nothing.
        public void SetTargetingEnabled(bool enable)
        {
            if (enable)
            {
                if (m_targetingHandle != null)
                {
                    Character.RemoveModifierOverrides(m_targetingHandle);
                    m_targetingHandle = null;
                }
            }
            else if (m_targetingHandle == null)
            {
                m_targetingHandle = Character.AddModifierOverride((int)GameplayModifierType.TargetingAvailable, false);
            }
        }

        // 060013c4
        public void SetTargetingInnerVolumeOnly(bool value)
        {
            IGraphStorage storage = Character.Storage;
            if (value)
                storage.SetValue<bool>(ActorFSMKeys.HomingTargetInnerVolumeOnly, true);
            else
                storage.RemoveValue<bool>(ActorFSMKeys.HomingTargetInnerVolumeOnly);
        }

        // 060013c5; the definition's cached values are recalculated before it is published.
        public void SetAbilityDefinitionOverride(CharacterAbilityDefinition abilityDefinitionOverride)
        {
            if (abilityDefinitionOverride == null)
                return;
            abilityDefinitionOverride.CalculateCachedValues();
            Character.Storage.SetValue<CharacterAbilityDefinition>(ActorFSMKeys.AbilityDefinitionOverride, abilityDefinitionOverride);
        }

        // 060013c6
        public void LevelExit()
        {
            Character.SetWorldVelocity(Vector3.zero);
            ProcessManager.GetSystem<LevelManager>(null, true).ExitLevel(false, false);
        }

        // 060013c7
        public void MissionPauseTimers(bool paused)
        {
            ProcessManager.GetSystem<MissionManager>(null, true).SetTimersPaused(paused);
        }

        // 060013c8
        public void TriggerCharacterAnimationEnter(CharacterAnimationTrigger trigger)
        {
            trigger.Action_OnEnter(Character);
        }

        // 060013c9
        public void TriggerCharacterAnimationExit(CharacterAnimationTrigger trigger)
        {
            trigger.Action_OnLeave(Character);
        }

        // 060013ca
        public void TriggerCharacterSpeedEvents()
        {
            m_onTriggerSpeedEvents.Invoke(m_speedCurve.Evaluate(Character.WorldVelocityMagnitude));
        }

        // 060013cb
        public void ResetTransform()
        {
            Transform target = m_targetTransform != null ? m_targetTransform : transform;
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
        }

        // 060013cc; the target forward is the camera's or the character's forward flattened onto the
        // character's ground plane.
        private void CalculateTarget(Character character, out Vector3 worldForward, out Vector3 targetForward, out Transform targetTransform)
        {
            targetTransform = m_targetTransform != null ? m_targetTransform : transform;
            worldForward = m_targetUseCameraForward ? character.CameraForward : character.ForwardDirection;
            targetForward = Vector3.ProjectOnPlane(worldForward, character.WorldUp).normalized;
        }

        // 060013cd; the target moves to the collider's closest point to the character, or to where a ray
        // from the character, swung by the raycast angle about its right axis, hits the collider.
        public void SetTransformPosition(Collider otherCollider)
        {
            if (!TryGetCharacter(out Character character))
                return;
            CalculateTarget(character, out Vector3 worldForward, out Vector3 targetForward, out Transform targetTransform);
            Vector3 position;
            if (m_targetUseClosestPoint)
            {
                position = otherCollider.ClosestPoint(character.WorldPosition);
            }
            else
            {
                Quaternion swing = Quaternion.AngleAxis(-m_targetRaycastAngle, character.RightDirection);
                var ray = new Ray(character.WorldPosition, swing * targetForward);
                position = otherCollider.Raycast(ray, out RaycastHit hit, m_targetRaycastDistance)
                    ? hit.point
                    : otherCollider.ClosestPoint(character.WorldPosition);
            }
            targetTransform.position = position;
        }

        // 060013ce; with ribbon orientation the up axis comes from the collider's ribbon at the nearest
        // location, and the character's forward is flattened onto that.
        public void SetTransformRotation(Collider otherCollider)
        {
            if (!TryGetCharacter(out Character character))
                return;
            CalculateTarget(character, out Vector3 worldForward, out Vector3 targetForward, out Transform targetTransform);
            if (m_targetUseRibbonOrientation && otherCollider.TryGetComponent(out Ribbon ribbon))
            {
                RibbonLocation location = ribbon.GetRibbon().FindNearestRibbonLocation(targetTransform.position, RibbonAlignment.Center);
                Quaternion rotation = otherCollider.transform.rotation * location.GetLocalTransform(false).Orientation;
                Vector3 up = rotation * Vector3.up;
                Vector3 forward = Vector3.ProjectOnPlane(worldForward, up);
                targetTransform.rotation = Quaternion.LookRotation(forward, up);
            }
            else
            {
                targetTransform.rotation = Quaternion.LookRotation(targetForward, character.WorldUp);
            }
        }

        // 060013cf
        public void SetTransform(Collider otherCollider)
        {
            SetTransformPosition(otherCollider);
            SetTransformRotation(otherCollider);
        }

        // 060013d0
        public void SetTransformRotationOnPlane(Transform planeTransform)
        {
            Transform target = m_targetTransform != null ? m_targetTransform : transform;
            Vector3 up = planeTransform.up;
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, up);
            target.rotation = Quaternion.LookRotation(forward, up);
        }

        // 060013d1; switches the update of the listed abilities and of the group's abilities.
        public void OverrideEnabledAbilities(bool enable)
        {
            if (!TryGetCharacter(out Character character))
                return;
            foreach (ActorAbilityType abilityType in m_abilityTypes)
                SetAbilityUpdateEnabled(character, abilityType, enable);
            if (m_abilityTypeGroup == null)
                return;
            foreach (CharacterAbilityTypeGroup.AbilityTypeDefinition definition in m_abilityTypeGroup.Abilities)
                SetAbilityUpdateEnabled(character, definition.Type, enable);
        }

        // 060013d2
        private static void SetAbilityUpdateEnabled(Character character, ActorAbilityType abilityType, bool enable)
        {
            if (character.TryGetAbilityFromType(abilityType, out ActorAbility ability))
                ability.UpdateEnabled = enable;
        }

        // 060013d3
        public void EndImpulses()
        {
            if (TryGetCharacter(out Character character))
                character.EndImpulses();
        }

        // 060013d4
        public CharacterEvents() { }
    }
}
