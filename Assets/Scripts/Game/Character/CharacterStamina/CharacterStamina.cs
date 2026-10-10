using System;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original owner candidate. Character/App/CollectableManager and
    // ActorAbilityUtilities are genuine, still-open source dependencies.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterStamina
    {
        // Complete original own-field order, including generated backing fields.
        public bool IsTriggered { get; private set; }
        public bool ActivationQueued { get; set; }
        public float SuspendTimer { get; set; }
        public bool Active { get; private set; }
        public float Stamina { get; protected set; } = 1f;
        public bool ImmediateResetApplied { get; private set; }
        public bool StaminaRechargeFromCollectable { get; private set; }
        // 060014bf. The radius getter intentionally has no guard.
        public float ColliderRadius => m_definition.ColliderRadius;
        public float ColliderRadiusSqr { get; private set; }
        public Func<CollectableType, float> OnCollectableOverride;
        // 060014c2 / ARM74b06c.
        public float ImpactDeactivationCosAngle => m_definition != null ? m_definition.ImpactDeactivationCosAngle : 1f;
        public bool UpdatedFromAbility { get; private set; }
        private bool m_overrideImpulse;
        private float m_staminaActivationDelayTime;
        private float m_staminaRechargeDelayTimer;
        private float m_staminaRechargeTimeSeconds;
        private float m_staminaRechargeMin;
        private bool m_isRecharging;
        // This empty default is present in the original constructor; replacing
        // the public field with null would alter its direct invocation contract.
        public Action OnStaminaRecharge = () => { };
        private float m_timeSinceTrigger;
        private float m_cooldownTime;
        private float m_elapsedActivationTime;
        protected ActorAbilityType m_abilityType;
        protected Definition m_definition;
        protected readonly Character m_character;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();
        private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();

        // 060014c5 / ARM8c8d64 shared enum body. Every successful read consumes
        // that key, with Storage reloaded after the typed lookup callback.
        protected bool TryGetValue<T>(GraphStorageKey varName, out T value)
        {
            bool result = m_character.Storage.TryGetValue(varName, out value);
            if (result) m_character.Storage.RemoveValue<T>(varName);
            return result;
        }

        // 060014c6 / ARM74a4fc. Initializers precede Object's constructor;
        // registration then captures the real current manager's delegate field.
        protected CharacterStamina(Character character)
        {
            m_character = character;
            m_collectableManagerRef.Get().OnCollectableStateUpdate += OnCollectableStateUpdate;
        }

        // 060014c7 / ARM74b094. GetSafe and both nullable reference guards are
        // genuine. Close only removes the subscription; it clears no state.
        public void Close()
        {
            CollectableManager manager = m_collectableManagerRef?.GetSafe();
            if (manager != null) manager.OnCollectableStateUpdate -= OnCollectableStateUpdate;
        }

        // 060014c8 has no native body: original abstract collectable contract.
        public abstract bool IsCollectableType(CollectableType collectableType);

        // 060014c9 / ARM74b1c0. The state and change metadata are unused by the
        // original handler. A nonzero override (including NaN) bypasses the type
        // predicate. Capture App once across its two ordered checks.
        private void OnCollectableStateUpdate(CollectableType collectableType,
            CollectableState collectableState, CollectableChangeMetadata metadata)
        {
            Func<CollectableType, float> collectableOverride = OnCollectableOverride;
            float multiplier = collectableOverride != null ? collectableOverride(collectableType) : 0f;
            if (multiplier == 0f)
            {
                if (!IsCollectableType(collectableType)) return;
                multiplier = 1f;
            }
            App app = m_appRef.Get();
            if (!app.HasGameplayControl() || app.RestartInProgress()) return;
            if (m_definition != null) multiplier *= m_definition.StaminaFromCollectable;
            AddStamina(multiplier);
            StaminaRechargeFromCollectable = true;
            m_staminaRechargeMin = 0f;
            OnStaminaRecharge();
        }

        // 060014ca / ARM74b2e4. There is only an upper cap; negative energy is
        // retained, and this clears the recharge latch without firing callbacks.
        public void AddStamina(float deltaStamina)
        {
            Stamina = Mathf.Min(Stamina + deltaStamina, 1f);
            m_isRecharging = false;
        }

        // 060014cb / ARM74ad30. Updating the selected definition happens first.
        // Reporting uses the accumulated milliseconds even when they are zero.
        // The activation's orientation flag is read from the supplied definition,
        // whereas cost/delay/radius use the current retained definition.
        public void Activate(ActorAbilityType abilityType, Definition definition,
            bool overrideImpulse = false, bool overrideActivation = false)
        {
            UpdateFromAbility(abilityType, definition, overrideImpulse);
            SuspendTimer = 0f;
            if (!Active)
            {
                m_character.ReportBoostingTime(Mathf.RoundToInt(m_elapsedActivationTime * 1000f));
                m_elapsedActivationTime = 0f;
            }
            // Native GT rejects positive cooldowns and allows unordered values.
            if (!overrideActivation && (Active || m_cooldownTime > 0f)) return;
            Active = true;
            ConsumeInitialStaminaCost(m_definition);
            m_staminaActivationDelayTime = m_definition.StaminaActivationDelay;
            SetRechargeTime(m_definition);
            float colliderRadius = m_definition.ColliderRadius;
            if (colliderRadius > 0f) m_character.EnableColliderTrigger(colliderRadius);
            m_character.ResetGrip();
            if (definition.OrientateToInputForward) m_character.OrientateToInputForward();
            m_character.EndImpulses();
        }

        // 060014cc / ARM74b3b0. No timer, definition or trigger reset is added.
        public void Reset(bool applyImmediate = false)
        {
            Stamina = 1f;
            ImmediateResetApplied = applyImmediate;
        }

        // 060014cd / ARM74b300. Null definitions still replace both fields before
        // returning. An already overridden impulse blocks all of these writes.
        public void UpdateFromAbility(ActorAbilityType abilityType, Definition definition,
            bool overrideImpulse = false)
        {
            if (m_overrideImpulse) return;
            m_definition = definition;
            m_abilityType = abilityType;
            if (m_definition == null) return;
            UpdatedFromAbility = true;
            m_overrideImpulse = overrideImpulse;
            float radius = m_definition.ColliderRadius;
            ColliderRadiusSqr = radius * radius;
        }

        // 060014ce / ARM74b3c0.
        public void ContinueFromAbility()
        {
            if (Active) UpdatedFromAbility = true;
        }

        // 060014cf / ARM74b3d4. The genuine shared base reads Boost, including
        // when the concrete owner is CharacterChaosStamina.
        private bool ImpulseActivated(CharacterBrain brain) =>
            m_overrideImpulse || (brain.Boost && m_cooldownTime == 0f);

        // 060014d0 / ARM74b424. Clearing override is inside the entry-time Active
        // branch. Collider callback failure interrupts the final override clear.
        public void UpdateDeactivation(bool impulseActivated)
        {
            ImmediateResetApplied = false;
            StaminaRechargeFromCollectable = false;
            bool wasUpdated = UpdatedFromAbility;
            UpdatedFromAbility = false;
            if (Active)
            {
                if (!wasUpdated || !impulseActivated)
                {
                    if (!m_isRecharging) SetRechargeDelay(m_definition);
                    Active = false;
                    m_isRecharging = false;
                    IsTriggered = false;
                    ActivationQueued = false;
                    m_character.DisableColliderTrigger();
                }
                m_overrideImpulse = false;
            }
        }

        // 060014d1 / ARM74b4a8. Preserve original impulseOverriden spelling.
        private void TriggerActivation(bool brainActive, bool impulseOverriden)
        {
            bool previous = IsTriggered;
            if (brainActive && !impulseOverriden) brainActive = CanTrigger();
            IsTriggered = brainActive;
            if (previous != IsTriggered) m_timeSinceTrigger = 0f;
        }

        // 060014d2 / ARM74b5e0. The delay uses the pre-Update captured duration.
        public void TriggerDeactivation(CharacterBrain brain)
        {
            float deltaTime = (1f - Stamina) * m_definition.StaminaActiveTime;
            Update(brain, deltaTime);
            m_staminaRechargeDelayTimer = deltaTime;
        }

        // 060014d3 / ARM74b4f4. Ability grace checks precede controller movement;
        // ValidStamina reloads the definition after that real Character call.
        public bool CanTrigger()
        {
            if (m_definition == null || m_cooldownTime > 0f) return false;
            if (!m_character.IsActive()) return false;
            if (ActorAbilityUtilities.AnyAbilityInUse(m_character, m_definition.BlockingAbilities, m_definition.BlockingPeriod)) return false;
            bool movement = !m_definition.RequiresControlStickInput || m_character.HasControllerMovement;
            return ValidStamina() && movement && !m_isRecharging;
        }

        // 060014d4 / ARM74ba10. Ordered comparisons keep NaN invalid.
        public bool ValidStamina()
        {
            if (m_definition != null && m_definition.StaminaDrainsToEmpty && Stamina < 1f) return false;
            return Stamina > 0f && Stamina >= m_staminaRechargeMin;
        }

        // 060014d5 / ARM74ba54.
        public float RemainingTime() => Active ? Stamina * m_definition.StaminaActiveTime : 0f;

        // 060014d6 / ARM74b628. Update order and the retained impulse snapshot are
        // important: deactivation clears the field but later queuing uses its old
        // value. Recharge increments precede the inactive delay decrement.
        public void Update(CharacterBrain brain, float deltaTime)
        {
            if (m_definition == null) return;
            bool impulseActivated = ImpulseActivated(brain);
            bool impulseOverriden = m_overrideImpulse;
            if (IsTriggered) m_timeSinceTrigger += deltaTime;
            else m_timeSinceTrigger = 0f;
            UpdateCooldown(deltaTime);
            UpdateDeactivation(impulseActivated);
            if (Active && IsTriggered && m_definition.StaminaActiveTime > 0f)
            {
                m_staminaRechargeMin = 0f;
                if (Stamina > 0f && m_staminaActivationDelayTime > 0f)
                {
                    m_staminaActivationDelayTime = Mathf.Max(m_staminaActivationDelayTime - deltaTime, 0f);
                    return;
                }
                if (SuspendTimer > 0f) return;
                m_elapsedActivationTime += deltaTime;
                Stamina = Mathf.Max(Stamina - deltaTime / m_definition.StaminaActiveTime, 0f);
                if (!(Stamina > 0f))
                {
                    IsTriggered = false;
                    m_isRecharging = true;
                    m_staminaRechargeMin = m_definition.StaminaActivationFromEmpty;
                    SetRechargeDelay(m_definition);
                }
                return;
            }
            if (Stamina < 1f && m_staminaRechargeTimeSeconds > 0f)
                Stamina = Mathf.Min(Stamina + deltaTime / m_staminaRechargeTimeSeconds, 1f);
            else if (m_isRecharging) m_isRecharging = false;

            if (ActivationQueued && !IsTriggered)
            {
                TriggerActivation(true, impulseOverriden);
                ActivationQueued = false;
            }
            else
            {
                float stateRemainTimer = m_character.Storage.GetValue<float>(ActorFSMKeys.StateRemainTimer, 0f, true);
                if (!IsTriggered || stateRemainTimer == 0f)
                    TriggerActivation(impulseActivated, impulseOverriden);
            }
            if (!IsTriggered && m_staminaRechargeDelayTimer > 0f)
                m_staminaRechargeDelayTimer = Mathf.Max(m_staminaRechargeDelayTimer - deltaTime, 0f);
        }

        // 060014d7 / ARM74b378.
        private void ConsumeInitialStaminaCost(Definition definition)
        {
            Stamina = Mathf.Max(Stamina - definition.StaminaActivationCost, 0f);
            m_staminaRechargeMin = Stamina == 0f ? definition.StaminaActivationFromEmpty : 0f;
        }

        // 060014d8 and 060014d9 / ARM74b48c,74b3a4.
        private void SetRechargeDelay(Definition definition) => m_staminaRechargeDelayTimer = definition.StaminaRechargeDelay;
        private void SetRechargeTime(Definition definition) => m_staminaRechargeTimeSeconds = definition.StaminaRechargeTime;

        // 060014da / ARM74ba9c, with the cost before both recharge timer writes.
        public void ExternalConsumeStaminaCost(Definition definition)
        {
            ConsumeInitialStaminaCost(definition);
            SetRechargeDelay(definition);
            SetRechargeTime(definition);
        }

        // 060014db and 060014dc / ARM74bad0,74bad8.
        public void ActivateCooldown(float cooldownTime) => m_cooldownTime = cooldownTime;
        public void ActivateCooldownFromImpact()
        {
            if (m_definition != null) m_cooldownTime = m_definition.ImpactCooldownTime;
        }

        // 060014dd / ARM74ba78. Equality is tested before subtraction.
        private void UpdateCooldown(float deltaTime)
        {
            if (m_cooldownTime != 0f) m_cooldownTime = Mathf.Max(m_cooldownTime - deltaTime, 0f);
        }

        // 060014de / ARM74b498. Clear does not set Active or reset the stamina.
        public void Clear()
        {
            IsTriggered = false;
            ActivationQueued = false;
            m_character.DisableColliderTrigger();
        }

        // 060014df / ARM74baec. Preserve current-culture formatting/newline.
        public bool GetUIText(StringBuilder stringInfoBuilder)
        {
            stringInfoBuilder.AppendLine(string.Format("Stamina = {0:F2}", Stamina));
            return true;
        }

        // 060014e0 / ARM74bb98. There is a trailing space and no newline.
        public void GetDebugExtraInfo(StringBuilder extraInfo) =>
            extraInfo.Append(string.Format("IsTriggered = {0}, Active = {1}, Stamina = {2} ", IsTriggered, Active, Stamina));

        [Serializable]
        [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)2, false)]
        [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)1, false)]
        public class Definition
        {
            [Tooltip("Character collision radius for the duration of being active.")]
            [Min(0f)]
            public float ColliderRadius;
            [Tooltip("Delay to depleting stamina, nb. this still triggers activation immediately.")]
            [Min(0f)]
            public float StaminaActivationDelay = 0.05f;
            [Min(0f)]
            [Tooltip("Stamina cost of activating.")]
            public float StaminaActivationCost = 0.3f;
            [Min(0f)]
            [Tooltip("Maximum duration of being active.")]
            public float StaminaActiveTime = 2.5f;
            [Tooltip("Whether the stamina drains to empty on activation, nb. this prevents any further activations until recharged.")]
            public bool StaminaDrainsToEmpty;
            [Tooltip("Time after activation before starting recharge.")]
            [Min(0f)]
            public float StaminaRechargeDelay = 0.5f;
            [Tooltip("Maximum duration of stamina recharge.")]
            [Min(0f)]
            public float StaminaRechargeTime = 0.5f;
            [Min(0f)]
            [Tooltip("The minimum stamina value allowed to activate after using all stamina.")]
            public float StaminaActivationFromEmpty = 1f;
            [Min(0f)]
            [Tooltip("The maximum time to suspend activation before deactivating.")]
            public float SuspendTime;
            [Tooltip("Character must have control stick input for activation to trigger.")]
            public bool RequiresControlStickInput;
            [Tooltip("Character will orientate to input forward on activation.")]
            public bool OrientateToInputForward = true;
            [Min(0f)]
            [Tooltip("The additional stamina awarded from an energy collectable.")]
            public float StaminaFromCollectable = 0.05f;
            [Tooltip("Any angle of impact of the character's velocity up to this value will deactivate.")]
            public float ImpactDeactivationAngle = 45f;
            [Tooltip("The cooldown time for an activation after an impact.")]
            public float ImpactCooldownTime = 0.5f;
            [Tooltip("Abilities that delay the activation if recently triggered.")]
            public System.Collections.Generic.List<ActorAbilityType> BlockingAbilities = new System.Collections.Generic.List<ActorAbilityType>();
            [Tooltip("Period of time abilities will delay activation.")]
            public float BlockingPeriod = 0.5f;
            [Tooltip("Ratio multiplier when switching characters.")]
            public float SwitchRatio = 1f;
            public float ImpactDeactivationCosAngle { get; private set; }
            public void CalculateCachedValues() => ImpactDeactivationCosAngle =
                UnityEngine.Mathf.Cos(ImpactDeactivationAngle * UnityEngine.Mathf.Deg2Rad);
            public Definition() { }
        }
    }
}
