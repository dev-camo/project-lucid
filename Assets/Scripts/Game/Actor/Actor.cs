// Complete native-derived original Actor source candidate. PRIVATE, UNCOMPILED, UNACCEPTED.
// Full genuine dependencies remain required; no substitute controller/type shells.
using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class Actor : MonoBehaviour, ITimeScaled, IGraphUser, IHasVisualProxy
    { // Private original Actor members. Complete genuine effect/audio/PFX dependencies
        // Private and incomplete: not a compilable substitute class or type shell.
        // Intended complete class identity: public abstract Actor : MonoBehaviour,
        // ITimeScaled, IGraphUser, IHasVisualProxy. Original option order: NullChecks=false,
        // ArrayBoundsChecks=false. Full schema/36fields/152methods remains authoritative.
        // 06000316 is a BeforeFieldInit initializer: original AngleAxis(180, up),
        // not Euler and not an explicit static constructor that clears BeforeFieldInit.
        public static readonly Quaternion RotationReverseLocal = Quaternion.AngleAxis(180f, Vector3.up);
        public IGraphStorage Storage { get; private set; } // 0600027f/280

        [UnityEngine.Serialization.FormerlySerializedAs("m_collider")]
        [SerializeField]
        protected Collider m_colliderCollision;
        public SmoothedTransformProxy VisualProxy { get; protected set; } // 281/282

        public ActorAudio Audio { get; private set; } // 283/284

        public ActorAnimator Animator { get; protected set; } // 285/286

        public Collider ColliderCollision => m_colliderCollision; // 287
        public bool Initialised => m_initialised; // 288
        public Vector3 WorldPosition { get; private set; } // 289/28a

        public Quaternion WorldRotation { get; private set; } // 28b/28c

        public virtual Vector3 WorldVelocity { get; private set; } // 28d/28e

        public Vector3 LocalVelocity { get; private set; } // 28f/290

        public virtual Vector3 Gravity { get; protected set; } = Vector3.down; // 291/292
        public virtual Vector3 GravityNormalised { get; protected set; } = Vector3.down; // 293/294
        public Vector3 WorldUp => -GravityNormalised; // 295, virtual gravity getter first
        public bool IsVelocityPaused { get; private set; } // 296/297

        public abstract float BrainMovementMagnitude { get; } // genuine 298 abstract contract

        public Vector3 UpDirection { get; private set; } // 299/29a

        public Vector3 ForwardDirection { get; private set; } // 29b/29c

        public Vector3 RightDirection { get; private set; } // 29d/29e

        public Quaternion WorldToLocalRotation { get; private set; } // 29f/2a0

        public float WorldVelocityMagnitudeSqr { get; private set; } // 2a1/2a2

        public float WorldVelocityMagnitude { get; private set; } // 2a3/2a4

        public Vector3 WorldVelocityNormalised { get; private set; } // 2a5/2a6

        public ActorBuffHandler BuffHandler { get; private set; } // 2a7/2a8

        public string ActorName { get; protected set; } // 2a9/2aa

        public List<FormTraits> Forms => m_traits.Forms; // 2ab
        public bool FormRenderersOn { get; private set; } = true; // 2ac/2ad
        private ActorFormHandler m_formHandler;
        public ActorFormType FormType => m_formHandler.CurrentFormType; // 2ae
        protected bool FixedUpdateActive { get; private set; } // 2af/2b0

        public bool IsPaused { get; set; } // 2b1/2b2, genuine implicit ITimeScaled implementation

        private float m_totalFixedTime;
        protected FiniteStateMachine m_fsm { get; set; } // 2b3/2b4

        protected SystemRef<TimeManager> m_timeManagerRef;
        protected bool m_initialised { get; private set; } // 2b5/2b6

        // 06000315 uses the default Dictionary constructor, no generated comparer.
        protected readonly Dictionary<ActorAbilityType, ActorAbility> m_abilities = new Dictionary<ActorAbilityType, ActorAbility>();
        protected ActorPFXController m_pfxController { get; private set; } // 2b7/2b8

        private FullscreenShaderManager m_fullscreenShaderManager;
        private ActorTraits m_traits;
        protected ActorDebug m_debug;
        public ActorAttachPoints ActorAttachPoints { get; private set; } // 2b9/2ba

        public ActorPFXController PFXController => m_pfxController; // 2bb
        // Private original Actor member fragments, not a complete class or compilable source.
        // Exact original class schema and all 152 declarations are adjacent. No missing
        // dependency type or method body is supplied by a placeholder. This file must not
        // be promoted until the complete genuine Actor and its dependency graph closes.
        // Game.Runtime 060002bc, ARM4eae1c: genuine FSMStorage(0), no other Awake work.
        protected virtual void Awake() => Storage = new FSMStorage(0);
        // Private original Actor member fragments. Full Actor and genuine dependencies
        // remain incomplete/uncompiled/unaccepted; this is not a substitute controller.
        // 060002bd. Initialisation order is authored: lookup ownership and animator,
        // optional audio, PFX, cached world pose, graph dictionaries, FSM user, scheduler,
        // buffs, fullscreen manager, then the final initialised flag. No rollback/finally.
        protected void Initialise(ActorDefinition definition, ActorComponentLookup actorComponentLookup)
        {
            actorComponentLookup.Initialise(this);
            CreateAnimatorHandler(actorComponentLookup.Animator);
            Audio = actorComponentLookup.ActorAudio;
            if (Audio != null)
                Audio.Initialise(definition.ActorAudioLookup);
            m_pfxController = actorComponentLookup.ActorPFXController;
            ActorAttachPoints = actorComponentLookup.ActorAttachPoints;
            m_pfxController.Initialise(this, ActorAttachPoints, definition.PFXDefinitionsDictionary);
            SetWorldVelocity(Vector3.zero);
            SetWorldPosition(transform.position);
            SetWorldRotation(transform.rotation);
            Storage.SetValue(ActorFSMKeys.FullscreenEffectDictionary, new Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>());
            Storage.SetValue(ActorFSMKeys.ActiveAnimationLookup, new Dictionary<string, ActorAnimationDefinition>());
            m_fsm = definition.FSMMovement.FSM;
            m_fsm.InitialiseUser(this);
            AttemptToSubscribeToTimeManager();
            BuffHandler = new ActorBuffHandler(this);
            m_fullscreenShaderManager = ProcessManager.GetSystem<FullscreenShaderManager>();
            m_initialised = true;
        }

        // 060002be, ARM4eb4d8: the original gate is m_fsm, not m_initialised or enabled.
        protected virtual void OnEnable()
        {
            if (m_fsm != null)
                AttemptToSubscribeToTimeManager();
        }

        // 060002bf, ARM4eb4e8: preserve two Get calls across unsubscribe callbacks.
        protected virtual void OnDisable()
        {
            if (m_timeManagerRef == null || m_timeManagerRef.IsNull())
                return;
            TimeCategory category = GetTimeCategory();
            m_timeManagerRef.Get().Unsubscribe(this, category, UpdateOn.FixedUpdate);
            m_timeManagerRef.Get().Unsubscribe(this, category, UpdateOn.Update);
            MovementStop(false);
        }

        // 060002c0. The override path reads Type twice before TryGetValue. Missing
        // override reads Tiers[0] directly; the no-override route uses genuine GetBase.
        // CreateAbility occurs before reloading the actor's dictionary receiver for Add.
        protected void InitialiseAbilities(IReadOnlyDictionary<ActorAbilityType, AbilityDefinition> abilityOverrides = null)
        {
            foreach (AbilityTiersDefinition tiersDefinition in m_traits.AbilityTierDefinitions)
            {
                ActorAbilityType type = tiersDefinition.Type;
                AbilityDefinition definition;
                if (abilityOverrides != null)
                {
                    if (!abilityOverrides.TryGetValue(tiersDefinition.Type, out definition))
                        definition = tiersDefinition.AbilityTiers[0];
                }
                else
                {
                    definition = tiersDefinition.GetBase();
                }

                if (definition == null)
                    continue;
                ActorAbility ability = definition.CreateAbility(this);
                m_abilities.Add(type, ability);
            }
        }

        // 060002c1. Assignment follows the genuine constructor and GameObject activation.
        protected virtual void CreateAnimatorHandler(Animator animator) => Animator = new ActorAnimator(animator);
        // 060002c2. The delegate targets the original method directly; no new closure.
        private void AttemptToSubscribeToTimeManager()
        {
            m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();
            m_timeManagerRef.InvokeOnValid(SubscribeToTimeManager);
        }

        // 060002c3, ARM4eba38: one category query, FixedUpdate before Update; no LateUpdate.
        // Resolved original literal at3292cb0 is empty string.
        private void SubscribeToTimeManager(TimeManager timeManager)
        {
            TimeCategory category = GetTimeCategory();
            timeManager.Subscribe(this, category, UpdateOn.FixedUpdate, "");
            timeManager.Subscribe(this, category, UpdateOn.Update, "");
        }

        // 060002c4, ARM4ebadc: distinct Unity-null guard on the captured safe manager.
        private void UnsubscribeFromTimeManager()
        {
            TimeManager timeManager = m_timeManagerRef?.GetSafe();
            if (timeManager == null)
                return;
            TimeCategory category = GetTimeCategory();
            timeManager.Unsubscribe(this, category, UpdateOn.FixedUpdate);
            timeManager.Unsubscribe(this, category, UpdateOn.Update);
        }

        // 060002c5. There is no guard for a null SystemRef itself in the original.
        public float GetTimeScale()
        {
            if (m_timeManagerRef.IsNull())
                return 1f;
            TimeManager timeManager = m_timeManagerRef.Get();
            return timeManager.GetTimescale(GetTimeCategory());
        }

        // 060002c6. Zero is the cache sentinel, including negative zero. NaN/nonzero
        // caches return immediately. A valid manager is obtained before querying category.
        public float GetTotalFixedTime()
        {
            if (m_totalFixedTime == 0f && m_timeManagerRef.TryGet(out TimeManager timeManager))
                m_totalFixedTime = timeManager.GetTotalFixedTime(GetTimeCategory());
            return m_totalFixedTime;
        }

        // 060002c7, ARM4ebd48: genuine IGraphStorage slot1 Clear dispatch.
        public void DestroyUser() => Storage.Clear();
        // 060002c8/2c9. GetValue uses default null/storeDefault true; no factory fallback.
        public Dictionary<string, ActorAnimationDefinition> GetActiveAnimationLookup() => Storage.GetValue<Dictionary<string, ActorAnimationDefinition>>(ActorFSMKeys.ActiveAnimationLookup);
        private Dictionary<int, List<FullscreenShaderManager.ParametersHandle>> GetFullscreenEffectDictionary() => Storage.GetValue<Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>>(ActorFSMKeys.FullscreenEffectDictionary);
        // 060002ca. Context dependency is genuine already-maintained TryGetOrNew.
        public List<FullscreenShaderManager.ParametersHandle> GetFullscreenEffectHandleInstances(int stateId) => GetFullscreenEffectDictionary().TryGetOrNew(stateId);
        // remain necessary; no replacement manager, contract, renderer or callback stub.
        // 060002cb. The exact normal-return cleanup order is retained. A callback may
        // throw at any stage; remaining state/reference writes are not placed in finally.
        public virtual void Close(bool isBeingDestroyed = false)
        {
            m_fsm?.ClearUser(this);
            m_fsm = null;
            foreach (KeyValuePair<int, List<FullscreenShaderManager.ParametersHandle>> pair in GetFullscreenEffectDictionary())
                ClearFullscreenEffects(true, pair.Value);
            UnsubscribeFromTimeManager();
            BuffHandler = null;
            DoClose(isBeingDestroyed);
            if (m_pfxController != null)
                m_pfxController.Deinitialise();
            m_pfxController = null;
            Animator?.Close();
            Animator = null;
            Audio = null;
            Storage.Clear();
            m_initialised = false;
        }

        // 060002cc. The original virtual Close(true) dispatch is retained.
        protected virtual void OnDestroy() => Close(true);
        // 060002cd, ARM4ec984: callback/update order is observable. The original has no
        // finally cleanup; an exceptional body/FSM/post callback leaves FixedUpdateActive.
        public void OnFixedUpdate(float deltaTime)
        {
            m_totalFixedTime = 0f;
            if (!ShouldUpdate())
                return;
            FixedUpdateActive = true;
            UpdateBodyPosition(deltaTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity);
            SetWorldPosition(position);
            SetWorldRotation(rotation);
            SetWorldVelocity(velocity);
            DoOnFixedUpdatePreFSM(deltaTime);
            m_fsm.Update(this, new FSMUpdateContext(deltaTime, FSMUpdateType.FixedUpdate));
            DoOnFixedUpdatePostFSM(deltaTime);
            FixedUpdateActive = false;
        }

        // 060002ce, ARM4ecac4: engine enabled is queried only after m_initialised.
        protected virtual bool ShouldUpdate() => m_initialised && enabled && !IsPaused;
        // 060002cf/2d0: genuine original pre/post virtual hooks, not substituted flow.
        protected virtual void DoOnFixedUpdatePreFSM(float deltaTime) => BuffHandler.OnUpdate(deltaTime);
        public virtual void DoOnFixedUpdatePostFSM(float deltaTime) => Animator.OnUpdate(this);
        // 060002d1, ARM4ecb30: separate ShouldUpdate query, tail virtual DoOnUpdate dispatch.
        public void OnUpdate(float deltaTime)
        {
            if (ShouldUpdate())
                DoOnUpdate(deltaTime);
        }

        // Seven original abstract contracts. No abstract/default substitute is invented.
        protected abstract void DoOnUpdate(float deltaTime); // 060002d2
        public abstract TimeCategory GetTimeCategory(); // 2d3
        protected abstract void UpdateBodyPosition(float deltaTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity); // 2d4
        public abstract void MovementStop(bool detectCollisions = false); // 2d5
        public abstract void MovementResume(Vector3 position, Vector3 velocity); // 2d6
        protected abstract void DoClose(bool isBeingDestroyed); // 2d7
        // 060002d8, ARM4ecb90: genuine retail RET hook, not missing behavior.
        public void OnLateUpdate(float deltaTime)
        {
        }

        // 060002d9, ARM4ecb94: cache only; it does not write the engine Transform.
        public virtual void SetWorldPosition(Vector3 worldPosition) => WorldPosition = worldPosition;
        // 060002da, ARM4ecba0: exact typed Quaternion.Equals, not approximate == or
        // orientation equivalence. It uses Single.Equals, including equal NaN components.
        public virtual void SetWorldRotation(Quaternion worldRotation)
        {
            if (worldRotation.Equals(WorldRotation))
                return;
            WorldRotation = worldRotation;
            WorldToLocalRotation = Quaternion.Inverse(worldRotation);
            UpDirection = WorldRotation * Vector3.up;
            ForwardDirection = WorldRotation * Vector3.forward;
            RightDirection = WorldRotation * Vector3.right;
            RefreshLocalVelocity();
        }

        // 060002db, ARM4ece24: during fixed update only, Unity's Vector3 == comparison
        // can skip a near-identical write. After storing, virtual WorldVelocity is reread
        // for magnitude, copied normalized vector and local velocity (not the argument).
        public virtual void SetWorldVelocity(Vector3 worldVelocity)
        {
            if (FixedUpdateActive && worldVelocity == WorldVelocity)
                return;
            WorldVelocity = worldVelocity;
            WorldVelocityMagnitudeSqr = WorldVelocity.sqrMagnitude;
            WorldVelocityMagnitude = Mathf.Sqrt(WorldVelocityMagnitudeSqr);
            WorldVelocityNormalised = WorldVelocity;
            if (WorldVelocityMagnitude > 0f)
                WorldVelocityNormalised /= WorldVelocityMagnitude;
            RefreshLocalVelocity();
        }

        // 060002dc, ARM4ecdb4: capture WorldToLocalRotation before virtual velocity query.
        private void RefreshLocalVelocity() => LocalVelocity = WorldToLocalRotation * WorldVelocity;
        // Private original Actor fragments. No type shells/placeholder controller or
        // substituted authored flow. Complete original class/dependencies remain blocked.
        // 060002dd. Preserve two virtual velocity reads and the intervening cached up
        // vector. Tangential speed is its magnitude, not signed local z or a clamp.
        public void OrientateVelocityToForward()
        {
            Vector3 velocityForDot = WorldVelocity;
            Vector3 up = UpDirection;
            Vector3 velocityForPlane = WorldVelocity;
            float verticalSpeed = Vector3.Dot(velocityForDot, up);
            Vector3 verticalVelocity = up * verticalSpeed;
            float tangentialSpeed = (velocityForPlane - verticalVelocity).magnitude;
            SetWorldVelocity(verticalVelocity + ForwardDirection * tangentialSpeed);
        }

        // 060002de. Input plane normals are assumed normalized: no normalization or
        // denominator is introduced. Original second projection follows the fallback.
        public void OrientateToPlane(Vector3 planeNormal, bool orientateToVelocity)
        {
            Vector3 lookDirection = orientateToVelocity ? WorldVelocity : ForwardDirection;
            lookDirection -= planeNormal * Vector3.Dot(planeNormal, lookDirection);
            if (lookDirection.sqrMagnitude < 0.0001f)
                lookDirection = -UpDirection;
            lookDirection -= planeNormal * Vector3.Dot(planeNormal, lookDirection);
            SetWorldRotation(Quaternion.LookRotation(lookDirection, planeNormal));
            if (!orientateToVelocity && Storage.GetValue<float>(ActorFSMKeys.GroundGripValue) != 0f)
            {
                float normalSpeed = Vector3.Dot(planeNormal, WorldVelocity);
                float redirectedSpeed = WorldVelocity.magnitude - Mathf.Abs(normalSpeed);
                Vector3 forward = ForwardDirection;
                SetWorldVelocity(planeNormal * normalSpeed + forward * redirectedSpeed);
            }
        }

        // 060002df. No near-zero fallback or vector normalization in this route.
        public void OrientateToPlaneWithLookDirection(Vector3 planeNormal, Vector3 lookDirection)
        {
            lookDirection -= planeNormal * Vector3.Dot(planeNormal, lookDirection);
            SetWorldRotation(Quaternion.LookRotation(lookDirection, planeNormal));
        }

        // 060002e0/2e1/2e2. A present null/wrong-subtype record returns false/null;
        // this does not claim that its dictionary key is absent.
        public bool TryGetAbilityFromType(ActorAbilityType abilityType, out ActorAbility ability) => m_abilities.TryGetValue(abilityType, out ability);
        public bool TryGetAbility<T>(ActorAbilityType abilityType, out T ability)
            where T : ActorAbility
        {
            if (TryGetAbilityFromType(abilityType, out ActorAbility actorAbility))
            {
                ability = actorAbility as T;
                return ability != null;
            }

            ability = null;
            return false;
        }

        public T GetAbility<T>(ActorAbilityType abilityType)
            where T : ActorAbility => TryGetAbilityFromType(abilityType, out ActorAbility actorAbility) ? actorAbility as T : null;
        // 060002e3. The original iterates the dictionary itself (not Values), with
        // disposable enumeration and callback only for a non-null typed value.
        public void GetAbilitiesOfType<T>(GetAbilityTypeCallback<T> callback)
            where T : ActorAbility
        {
            foreach (KeyValuePair<ActorAbilityType, ActorAbility> pair in m_abilities)
            {
                T ability = pair.Value as T;
                if (ability != null)
                    callback(ability);
            }
        }

        // 060002e4. Original I has an interface constraint, no class constraint.
        // Preserve the separate is check and cast/read; don't introduce a fake interface.
        public void GetAbilitiesWithInterface<I>(GetAbilityInterfaceCallback<I> callback)
            where I : IAbility
        {
            foreach (KeyValuePair<ActorAbilityType, ActorAbility> pair in m_abilities)
            {
                if (pair.Value is I)
                    callback((I)(object)pair.Value);
            }
        }

        // 060002e5. First matching predicate result returns after enumerator disposal;
        // a null callback is only dereferenced on a matching non-null subtype.
        public T FindAbility<T>(FindAbilityTypeCallback<T> callback)
            where T : ActorAbility
        {
            foreach (KeyValuePair<ActorAbilityType, ActorAbility> pair in m_abilities)
            {
                T ability = pair.Value as T;
                if (ability != null && callback(ability))
                    return ability;
            }

            return null;
        }

        // 060002e6. ContainsKey remains true even for a stored null ability.
        public bool HasAbilityType(ActorAbilityType abilityType) => m_abilities.ContainsKey(abilityType);
        // 060002e7. Return means a typed ability was found, not a trigger outcome.
        protected bool TryTriggerAbility<TAbility>(ActorAbilityType abilityType, bool trigger)
            where TAbility : ActorAbility, IAbilityTriggerable
        {
            bool found = TryGetAbility<TAbility>(abilityType, out TAbility ability);
            if (found)
            {
                if (trigger)
                    ability.DoTrigger();
                else
                    ability.ClearTrigger();
            }

            return found;
        }

        // 060002e8. Animator trigger, animator bool, audio, PFX, then fullscreen effects.
        // Only the optional effects list is CLR-null guarded; other systems are real
        // required dependencies. UntilStopped is captured before the manager callback.
        public void TriggerAnimationEnter(ActorAnimationDefinition animationDefinition, List<FullscreenShaderManager.ParametersHandle> effectHandleInstances = null)
        {
            if (!m_initialised || animationDefinition == null)
                return;
            Animator.TrySetFromAnimationTypeTrigger(animationDefinition, ActorAnimationType.TriggerEnter);
            Animator.TrySetFromAnimationType(animationDefinition, ActorAnimationType.BoolEnter, true);
            Audio.ProcessOnEnterActions(animationDefinition);
            m_pfxController.PlayActorPFX(animationDefinition.PFXParameters);
            if (effectHandleInstances == null)
                return;
            foreach (ActorAnimationDefinition.FullscreenEffectParameter parameter in animationDefinition.FullscreenEffectParameters)
            {
                if (parameter.TriggerOnExit)
                    continue;
                float duration = parameter.Duration;
                bool untilStopped = parameter.UntilStopped;
                bool applyCurveOverDuration = parameter.ApplyCurveOverDuration;
                FullscreenShaderManager manager = m_fullscreenShaderManager;
                FullscreenShaderParametersType type = parameter.FullscreenShaderParametersType;
                bool started = manager.StartEffectOverride(type, out FullscreenShaderManager.ParametersHandle handle, duration, applyCurveOverDuration, untilStopped);
                if (started && untilStopped)
                    effectHandleInstances.Add(handle);
            }
        }

        // 060002e9. effectHandleInstances is genuinely unused on update.
        public void TriggerAnimationUpdate(ActorAnimationDefinition animationDefinition, List<FullscreenShaderManager.ParametersHandle> effectHandleInstances = null)
        {
            if (!m_initialised || animationDefinition == null)
                return;
            m_pfxController.UpdateActorPFX(animationDefinition.PFXParameters);
        }

        // 060002ea. Delayed audio is cleared before leave actions. Existing effects clear
        // before exit effects start; exit effects never return stored handles and always
        // use false for both ApplyCurveOverDuration and UntilStopped, preserving quirks.
        public void TriggerAnimationLeave(ActorAnimationDefinition animationDefinition, List<FullscreenShaderManager.ParametersHandle> effectHandleInstances = null)
        {
            if (!m_initialised || animationDefinition == null)
                return;
            Animator.TrySetFromAnimationTypeTrigger(animationDefinition, ActorAnimationType.TriggerExit);
            Animator.TrySetFromAnimationType(animationDefinition, ActorAnimationType.BoolExit, false);
            Audio.ClearDelayedAudio();
            Audio.ProcessOnLeaveActions(animationDefinition);
            m_pfxController.StopPFX(animationDefinition.PFXParameters);
            m_pfxController.PlayActorPFX(animationDefinition.PFXParametersLeaveState);
            ClearFullscreenEffects(false, effectHandleInstances, animationDefinition);
            foreach (ActorAnimationDefinition.FullscreenEffectParameter parameter in animationDefinition.FullscreenEffectParameters)
            {
                if (!parameter.TriggerOnExit)
                    continue;
                float duration = parameter.Duration;
                FullscreenShaderManager manager = m_fullscreenShaderManager;
                FullscreenShaderParametersType type = parameter.FullscreenShaderParametersType;
                manager.StartEffectOverride(type, out FullscreenShaderManager.ParametersHandle handle, duration, false, false);
            }
        }

        // 060002eb. A non-null animation narrows stopping to its first matching effect
        // type. Missing definition or absent match skips the handle; the original still
        // clears the complete caller list after normal enumeration. Delays are !=0,
        // including negative/NaN values. No finally list-clearing or fallback stop.
        private void ClearFullscreenEffects(bool immediate, List<FullscreenShaderManager.ParametersHandle> effectHandleInstances, ActorAnimationDefinition animation = null)
        {
            if (effectHandleInstances == null)
                return;
            foreach (FullscreenShaderManager.ParametersHandle handle in effectHandleInstances)
            {
                if (animation != null)
                {
                    if (handle.ActiveParameters.Definition == null)
                        continue;
                    FullscreenShaderParametersType type = handle.ActiveParameters.Definition.FullscreenShaderParametersType;
                    float delayStopSeconds = 0f;
                    bool found = false;
                    foreach (ActorAnimationDefinition.FullscreenEffectParameter parameter in animation.FullscreenEffectParameters)
                    {
                        if (parameter.FullscreenShaderParametersType != type)
                            continue;
                        delayStopSeconds = parameter.DelayStopSeconds;
                        found = true;
                        break;
                    }

                    if (!found)
                        continue;
                    if (!immediate && delayStopSeconds != 0f)
                    {
                        m_fullscreenShaderManager.StopEffectOverrideWithDelay(handle, delayStopSeconds);
                        continue;
                    }
                }

                m_fullscreenShaderManager.StopEffectOverride(handle);
            }

            effectHandleInstances.Clear();
        }

        // 060002ec. Original PFX stop does not guard the controller.
        public void StopAllActorPFX() => m_pfxController.StopAllPFX();
        // 060002ed. Genuine original retail RET hook, not unresolved collision behavior.
        public virtual void SetColliderCollisionSize(float radius, float height)
        {
        }

        // 060002ee. Local input is rotated using the cached world rotation, then sent
        // through the virtual world setter so the original derived update paths remain.
        public void SetLocalVelocity(Vector3 localVelocity) => SetWorldVelocity(WorldRotation * localVelocity);
        public void SwitchToForm(ActorFormType formType) => m_formHandler.SwitchToForm(formType); // 060002ef
        // 060002f0. Missing Unity component leaves FormRenderersOn unchanged.
        public void ToggleFormRenderers(bool on)
        {
            if (m_formHandler == null)
                return;
            FormRenderersOn = on;
            m_formHandler.ToggleFormRenderers(on);
        }

        // 060002f1. Base Actor's original false return is overridden in Character.
        public virtual bool IsFormLocked() => false;
        // 060002f2. Store traits, store form handler, Initialise, then renderer toggle.
        // The handler Initialise is not null guarded, even though the later toggle is.
        protected void InitialiseTraits(ActorTraits traits, ActorComponentLookup componentLookup)
        {
            m_traits = traits;
            m_formHandler = componentLookup.ActorFormHandler;
            m_formHandler.Initialise(this);
            ToggleFormRenderers(true);
        }

        // 060002f3. Close is unconditional after the Unity guarded renderer toggle.
        // An exception leaves the real handler reference in place; no finally cleanup.
        protected void CloseTraits()
        {
            ToggleFormRenderers(false);
            m_formHandler.Close();
            m_formHandler = null;
        }

        // 060002f4/2f5. These update velocity-paused and audio. They do not set IsPaused;
        // that separately stored ITimeScaled property is managed by the real scheduler.
        public virtual void OnPause()
        {
            IsVelocityPaused = true;
            SetAudioActive(false);
        }

        public virtual void OnResume()
        {
            IsVelocityPaused = false;
            SetAudioActive(true);
        }

        // 060002f6/2f7. Preserve Unity fake-null checks, then reload Audio for callback.
        private void SetAudioActive(bool active)
        {
            if (Audio != null)
                Audio.SetActive(active);
        }

        public void ActivateAudioFilter(bool enable, bool isAir)
        {
            if (Audio != null)
                Audio.ActivateFilter(enable, isAir);
        }

        // 060002f8. No missing-proxy guard in the original.
        public void SetVisualProxyActive(bool active) => VisualProxy.gameObject.SetActive(active);
        // 060002f9. Original boolean storage probe treats present false as false too.
        public bool TryGetValue(GraphStorageKey fsmKey) => Storage.TryGetValue(fsmKey, out bool value) && value;
        // Private native-derived Actor diagnostic members. Genuine retail RET bodies
        // are established individually by original ARM and x86 ranges, not guessed stubs.
        // The full real ActorDebug/CharacterDebug_Metadata dependencies remain required.
        // 060002fa. Genuine generic ActorDebug subtype component, then virtual Init;
        // the optional toggle follows initialisation and rereads the stored debug field.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        protected void DebugInit<T>(bool enableDebug = false)
            where T : ActorDebug
        {
            m_debug = gameObject.GetOrAddComponent<T>();
            m_debug.Init(this);
            if (enableDebug)
                m_debug.ToggleDebugInfo();
        }

        public bool DebugIsEnabled() => m_debug != null && m_debug.IsEnabled; // 060002fb
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugToggleInfo() => m_debug.ToggleDebugInfo(); // 2fc
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        protected void DebugClose()
        {
        } // 2fd genuine RET

        public GameObject DebugGetLocationParent() => DebugIsEnabled() ? m_debug.GetLocationParent() : null; // 2fe
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        protected void DebugAddLocationTimestamp(Rigidbody body, FiniteStateMachine fsm)
        {
        } // 2ff

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugOnStateEnter(FSMState state, FSMStateChangeAction action)
        {
        } // 300

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugOnStateUpdate(FSMState state, float deltaTime)
        {
        } // 301

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugBeginLocationInstantiation(bool forceUpdate = false)
        {
        } // 302

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugAddLocationInstantiation(Vector3 position, Quaternion rotation, string namePostfix, Vector3? scale = null, GameObject parent = null, float lifetime = 5f, CharacterDebug_Metadata.Metadata data = null, bool unique = false)
        {
        } // 303

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugAddLocationLine(Vector3 start, Vector3 end, string namePostfix, GameObject parent = null, float lifetime = 5f, CharacterDebug_Metadata.Metadata data = null, bool unique = false)
        {
        } // 304

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugAddLocationCube(Vector3 centre, Vector3 size, string namePostfix, CharacterDebug_Metadata.Metadata data = null)
        {
        } // 305

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugEndLocationInstantiation(bool forceUpdate = false)
        {
        } // 306

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugRemoveLocation(GameObject gameObject, GameObject parent)
        {
        } // 307

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugRemoveLocationParent(GameObject locationParent)
        {
        } // 308

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugDrawRay(Vector3 position, Vector3 direction, Color colour)
        {
        } // 309

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugSetThrottleDebugInfo(float minSpeed, float maxSpeed, float acceleration, float deceleration)
        {
        } // 30a

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugSetMovementDebugInfo(float targetSpeed, float scaledSpeed, float accelerationMultiplier, float effectiveInputMagnitude)
        {
        } // 30b

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugSetTurningInfo(float intendedTurnDelta, float byVelocity, float byAngle, float rateOfTurn)
        {
        } // 30c

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public virtual void DebugGetActorInfo(StringBuilder stringInfoBuilder)
        {
        } // 30d

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public virtual void DebugGetStateInfo(StringBuilder stringInfoBuilder)
        {
        } // 30e

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugGetStateInfo(FiniteStateMachine fsm, StringBuilder stringInfoBuilder, IGraphUser user)
        {
        } // 30f

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public virtual void DebugGetFsmInfo(StringBuilder stringInfoBuilder)
        {
        } // 310

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public virtual void DebugClearFsmInfo()
        {
        } // 311

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public virtual void DebugGetCollisionInfo(StringBuilder stringInfoBuilder)
        {
        } // 312

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugGetDisplayVelocity(ref Vector3 velocity) => velocity = m_debug.GetDisplayVelocity(); // 313
        // 06000314. Animator is guarded by CLR null; active dump, blank line, then a
        // reloaded Animator for inactive dump. No engine fake-null or callback catches.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugGetAnimatorInfo(StringBuilder stringInfoBuilder)
        {
            if (Animator == null)
                return;
            Animator.GetDebugInfo(stringInfoBuilder, true);
            stringInfoBuilder.AppendLine();
            Animator.GetDebugInfo(stringInfoBuilder, false);
        }

        // 06000315 field initializers precede the genuine MonoBehaviour base constructor.
        // Remaining cached pose/axes/rotation/velocity fields retain their zero defaults.
        protected Actor()
        {
        }

        // Three genuine nested delegates in original declaration order, contravariant
        // parameter attributes=2. Runtime-created delegate methods add no own body credit.
        public delegate void GetAbilityInterfaceCallback<in I>(I ability)
            where I : IAbility;
        public delegate void GetAbilityTypeCallback<in T>(T ability)
            where T : ActorAbility;
        public delegate bool FindAbilityTypeCallback<in T>(T ability)
            where T : ActorAbility;
    }
}
