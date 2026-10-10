using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Enemy : Actor, IPlayerDamageable, IResetCapableGameplayElement
    {
        private const string AttackAnimationBool = "AttackAnimComplete[FSM]";
        private const string WindUpAnimationBool = "WindUpAnimStarted[FSM]";
        private const string AlertAnimationBool = "Alert[Bool]";

        [SerializeField] private EnemyType m_type;
        [SerializeField] private SimplifiedBody m_simplifiedBody;
        [SerializeField] private EnemyNavigation m_navigation;
        [SerializeField] private ActorComponentLookup m_actorComponentLookup;
        [Tooltip("The default effect sequence when enemy receives a hit from the character.")]
        [SerializeField] private EffectSequence m_onReceivedHit;
        [SerializeField]
        [Tooltip("The override effect sequence when enemy receives a hit from the character using a certain ability.")]
        private SerializableDictionary<ActorAbilityType, EffectSequence> m_onReceivedHitCharacterOverrides =
            new SerializableDictionary<ActorAbilityType, EffectSequence>(HardlightEnumComparers.ActorAbilityTypeComparer);
        [SerializeField]
        [Tooltip("The effect sequence when enemy receives a hit from a collider trigger in the level. e.g. out of bounds.")]
        private EffectSequence m_onReceivedHitTrigger;
        [SerializeField] private UnityEvent m_onRespawned;
        [SerializeField] private UnityEvent m_onHitComplete;
        [SerializeField] private float m_activationDistance = 50f;
        [SerializeField] private float m_deactivationDistance = 100f;
        [SerializeField] private Hazard m_attackHazard;
        [Tooltip("On level activated make sure all enemy components are active.")]
        [SerializeField] private UnityEvent m_onLevelActivated;
        [SerializeField] private SmoothedTransformProxy m_smoothedTransformProxy;

        public EnemyType Type => m_type;
        public EnemyDefinition Definition => m_definition;
        public EnemyManager EnemyManager { get; private set; }
        public EnemyNavigation Navigation => m_navigation;
        public ActorComponentLookup ActorComponentLookup => m_actorComponentLookup;
        public EnemyBrain Brain { get; private set; }
        public Vector2 BrainMovement { get; private set; }
        public override float BrainMovementMagnitude => BrainMovement.magnitude;
        public bool BrainTargetLocked { get; private set; }
        public bool AnimatorReadyWindUpAttack { get; private set; }
        public bool BrainWindUpAttack { get; private set; }
        public bool BrainAttackTarget { get; private set; }
        public bool BrainTargetSpotted { get; private set; }
        public bool AnimatorAttackComplete { get; private set; }
        public float AttackWindUpTimer { get; private set; }
        public bool WindUpAttackComplete { get; private set; }
        public bool AlertAnimation { get; private set; }
        public bool IsDying { get; private set; }
        public bool IsDead { get; private set; }
        public bool HitPlayer { get; private set; }
        public override Vector3 Gravity => m_simplifiedBody.Gravity;
        public override Vector3 GravityNormalised => m_simplifiedBody.GravityNormalised;
        public bool CollidingWithGround => m_simplifiedBody.CollidingWithGround;
        public override Vector3 WorldVelocity => m_simplifiedBody.WorldVelocity;

        private float m_activationDistanceSqr;
        private float m_deactivationDistanceSqr;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private readonly SystemRef<EnemyManager> m_enemyManagerRef = ProcessManager.GetSystemRef<EnemyManager>();
        private readonly SystemRef<GameplayIslandManager> m_gameplayIslandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>();
        private Transform m_transform;
        private Vector3 m_startPosition;
        private Quaternion m_startRotation;
        private bool m_isActivated;
        private EnemyDefinition m_definition;

        private void Start()
        {
            m_activationDistanceSqr = m_activationDistance * m_activationDistance;
            m_deactivationDistanceSqr = m_deactivationDistance * m_deactivationDistance;
            m_transform = transform;
            m_startPosition = m_transform.position;
            m_startRotation = m_transform.rotation;
            SetWorldPosition(m_startPosition);
            SetWorldRotation(m_startRotation);
            if (ProcessManager.GetSystem<DataManager>().EnemyDefinitions.TryGetValue(m_type, out m_definition))
            {
                m_enemyManagerRef.InvokeOnValid(RegisterToEnemyManager);
                Validate();
                m_gameplayIslandManagerRef.InvokeOnValid(islandManager => islandManager.RegisterResetCapableBehaviour(this));
            }
        }

        private void OnValidate()
        {
            if (gameObject.CanValidate(false))
            {
                Validate();
                m_activationDistanceSqr = m_activationDistance * m_activationDistance;
            }
        }

        private void Validate()
        {
            _ = m_navigation != null;
        }

        private void RegisterToEnemyManager(EnemyManager enemyManager)
        {
            EnemyManager = enemyManager;
            EnemyManager.Register(this);
        }

        public void Initialise(EnemyDefinition definition)
        {
            InitialiseTraits(definition.Traits, m_actorComponentLookup);
            InitialiseAbilities(null);
            Activate();
            Brain = new EnemyBrain();
            Brain.Initialise(this, m_definition.FSMBrain.FSM);
            VisualProxy = m_smoothedTransformProxy;
            if (definition.Traits.ShouldDetachVisualProxy && VisualProxy != null)
                VisualProxy.Detach(transform.parent);
            m_characterManagerRef.Get().OnCharacterRespawn += OnCharacterRespawn;
            base.Initialise(definition, m_actorComponentLookup);
            SetEnabled(false);
        }

        protected override void DoClose(bool isBeingDestroyed)
        {
            if (!m_characterManagerRef.IsNull())
                m_characterManagerRef.Get().OnCharacterRespawn -= OnCharacterRespawn;
            CloseTraits();
            EnemyManager?.Deregister(this);
            EnemyManager = null;
            Brain?.Close();
            Brain = null;
            m_transform = null;
            m_simplifiedBody.Close();
        }

        protected override bool ShouldUpdate()
        {
            return base.ShouldUpdate() && Brain != null && enabled;
        }

        protected override void DoOnFixedUpdatePreFSM(float deltaTime)
        {
            base.DoOnFixedUpdatePreFSM(deltaTime);
            if (m_simplifiedBody.GravityForceEnabled)
            {
                Vector3 upDirection = UpDirection;
                if (!((upDirection + WorldUp).sqrMagnitude < 9.99999944E-11f))
                {
                    float radians = m_definition.Traits.RotateToGravityRadiansPerSecond * deltaTime;
                    Vector3 direction = Vector3.RotateTowards(-upDirection, WorldUp, radians, 0f);
                    OrientateToPlaneWithLookDirection(-direction, ForwardDirection);
                }
            }
            CharacterSettings.ColliderSettings colliderSettings = m_definition.Settings.Collider;
            if (colliderSettings.StickToColliderMask.value != 0)
            {
                Vector3 direction = m_simplifiedBody.GravityForceEnabled ? WorldUp : -UpDirection;
                if (Physics.Raycast(WorldPosition, direction, out RaycastHit hit,
                    colliderSettings.GroundCheckMinDistance, colliderSettings.StickToColliderMask))
                    SetWorldPosition(hit.point);
            }
            UpdateAbilities(deltaTime);
            UpdateBrain(deltaTime);
        }

        private void UpdateAbilities(float deltaTime)
        {
            foreach (KeyValuePair<ActorAbilityType, ActorAbility> entry in m_abilities)
            {
                entry.Deconstruct(out ActorAbilityType abilityType, out ActorAbility ability);
                ability.Update(deltaTime);
            }
        }

        public override void DoOnFixedUpdatePostFSM(float deltaTime)
        {
            base.DoOnFixedUpdatePostFSM(deltaTime);
            SetWorldPosition(m_navigation.ConstrainPosition(WorldPosition));
            UpdateTransformPosition();
            m_transform.rotation = WorldRotation;
        }

        private void UpdateTransformPosition()
        {
            if (WorldVelocity.sqrMagnitude >= 1E-6f || (m_transform.position - WorldPosition).sqrMagnitude >= 1E-6f)
                m_transform.position = WorldPosition;
            if (VisualProxy != null)
                VisualProxy.transform.position = m_transform.position;
        }

        private void UpdateBrain(float deltaTime)
        {
            Brain.OnFixedUpdate(deltaTime);
            BrainMovement = Brain.GetMovement();
            BrainTargetLocked = Brain.GetTargetLocked();
            BrainAttackTarget = Brain.GetAttackTarget();
            BrainTargetSpotted = Brain.GetSightingReactionTrigger();
            BrainWindUpAttack = Brain.GetWindUpAttack();
            AnimatorReadyWindUpAttack = Animator.GetAnimatorBool(WindUpAnimationBool);
            AlertAnimation = Animator.GetAnimatorBool(AlertAnimationBool);
            Brain.SetAnimatorWindUpReady(AnimatorReadyWindUpAttack);
            AttackWindUpTimer = Brain.GetAttackWindUpTimer();
            WindUpAttackComplete = Brain.GetAttackWindUpAnimationComplete();
            AnimatorAttackComplete = Animator.GetAnimatorBool(AttackAnimationBool);
            if (m_definition.Traits.HasExtraAttackHazard)
                m_attackHazard.enabled = WindUpAttackComplete && !AnimatorAttackComplete;
            Brain.SetAttackAnimationComplete(AnimatorAttackComplete);
        }

        protected override void DoOnUpdate(float deltaTime) { }

        public override TimeCategory GetTimeCategory()
        {
            return TimeCategory.EnemyMovement;
        }

        protected override void UpdateBodyPosition(float deltaTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            SetWorldVelocity(m_simplifiedBody.WorldVelocity);
            position = m_transform.position;
            rotation = m_transform.rotation;
            velocity = WorldVelocity;
        }

        public override void SetWorldPosition(Vector3 worldPosition)
        {
            base.SetWorldPosition(worldPosition);
            UpdateTransformPosition();
        }

        public override void SetWorldVelocity(Vector3 worldVelocity)
        {
            base.SetWorldVelocity(worldVelocity);
            m_simplifiedBody.WorldVelocity = worldVelocity;
        }

        public override void MovementStop(bool detectCollisions = false) { }
        public override void MovementResume(Vector3 position, Vector3 velocity) { }

        public void ReceiveHit(Collider damagedCollider)
        {
            if (IsDying)
                return;
            IsDying = true;
            if (m_onReceivedHit == null)
            {
                HitComplete();
                return;
            }
            if (!TryTriggerOverrideHit(damagedCollider))
                m_onReceivedHit.SetTrigger(this);
            IEffectData effectData = new TargetedEffect { Target = transform };
            MessageExchangeWithCompletion<Component> messages = ProcessManager.GetSystem<MessageManager>().ComponentMessagesWithCompletion;
            Component component = this;
            messages.PublishMessage(in component, in effectData, HitComplete, -1);
        }

        private bool IsCharacterCollider(Collider hitCollider, out Character character)
        {
            return m_characterManagerRef.Get().TryGetCurrentCharacter(out character) && hitCollider == character.ColliderCollision;
        }

        private bool TryTriggerOverrideHit(Collider damagedCollider)
        {
            if (damagedCollider == null || !IsCharacterCollider(damagedCollider, out Character character))
            {
                m_onReceivedHitTrigger.SetTrigger(this);
                return true;
            }
            foreach (KeyValuePair<ActorAbilityType, EffectSequence> entry in m_onReceivedHitCharacterOverrides)
            {
                if (ActorAbilityUtilities.IsAbilityInUse(character, entry.Key, 0f))
                {
                    entry.Value.SetTrigger(this);
                    return true;
                }
            }
            return false;
        }

        private void HitComplete()
        {
            if (this == null)
                return;
            IsDying = false;
            IsDead = true;
            Storage.SetValue<float>(ActorFSMKeys.EnemyDeadTimer, 0f);
            EnemyManager?.ReportEnemyDestroyedByPlayer();
            m_onHitComplete?.Invoke();
        }

        private void OnCharacterRespawn()
        {
            if (IsDying)
            {
                if (!m_definition.Traits.ReviveOnCharacterRespawn)
                    return;
                HitComplete();
                OnRevive();
            }
            else if (IsDead)
            {
                if (m_definition.Traits.ReviveOnCharacterRespawn)
                    OnRevive();
            }
            else
                ResetGameplayElement();
        }

        public void Activate()
        {
            IsDying = false;
            IsDead = false;
            m_isActivated = true;
            if (VisualProxy != null)
                VisualProxy.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            m_isActivated = false;
            enabled = false;
            if (VisualProxy != null)
                VisualProxy.gameObject.SetActive(false);
            Audio.StopAnyAudio();
            HitPlayer = false;
        }

        public void RegisterHitPlayer(DamageAction damageAction, HazardType hazardType)
        {
            HitPlayer = true;
        }

        public void ClearHitPlayer()
        {
            HitPlayer = false;
        }

        public void SetGravityForceEnabled(bool gravityForceEnabled)
        {
            m_simplifiedBody.SetGravityForceEnabled(gravityForceEnabled);
        }

        private void SetEnabled(bool enable)
        {
            if (enabled == enable)
                return;
            m_simplifiedBody.enabled = enable;
            enabled = enable;
            if (enable)
                m_fsm.InitialiseUser(this, null, null);
            else
            {
                Animator.Rebind();
                m_fsm.ClearUser(this, null);
                SetWorldVelocity(Vector3.zero);
                HitPlayer = false;
            }
        }

        public void SetEnabledByProximity(Vector3 playerPosition)
        {
            if (!m_initialised || !m_isActivated)
                return;
            float distanceSqr = (WorldPosition - playerPosition).sqrMagnitude;
            float activationDistanceSqr = m_activationDistanceSqr;
            float deactivationDistanceSqr = m_deactivationDistanceSqr;
            if (enabled && !(distanceSqr <= deactivationDistanceSqr))
                SetEnabled(false);
            else if (!enabled && distanceSqr <= activationDistanceSqr)
                SetEnabled(true);
        }

        public void SetAttackWindUpComplete(bool complete)
        {
            Brain.Storage.SetValue<bool>(EnemyBrainFSMKeys.AttackWindUpComplete, complete);
        }

        public void ConsumeSightingReactionTrigger()
        {
            Brain.ConsumeSightingReactionTrigger();
            BrainTargetSpotted = Brain.GetSightingReactionTrigger();
        }

        public void ResetGameplayElement()
        {
            SetWorldVelocity(Vector3.zero);
            SetWorldPosition(m_startPosition);
            SetWorldRotation(m_startRotation);
            Activate();
            UpdateTransformPosition();
            m_transform.rotation = WorldRotation;
            m_onLevelActivated?.Invoke();
        }

        public void OnRevive()
        {
            if (!IsDead)
                return;
            IsDead = false;
            Storage.RemoveValue<float>(ActorFSMKeys.EnemyDeadTimer);
            ResetGameplayElement();
            m_onRespawned?.Invoke();
        }

        public Vector3 GetPosition()
        {
            return WorldPosition;
        }

        public bool IsDestroyed()
        {
            return this == null;
        }

        public Enemy() { }
    }
}
