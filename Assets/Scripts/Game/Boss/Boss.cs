using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001d5. Complete authored owner and both serializable
    // nested owners, inferred from the complete shipping ARM64 and x86 bodies.
    // The local functions and cached lambda retain genuine source constructs;
    // compiler-generated identity/layout and exceptional native parity are held.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class Boss : Actor, IPlayerDamageable
    {
        [SerializeField] private BossDefinition m_definition;
        [SerializeField] private BossBodyComponents[] m_bossInteractionComponents;
        [SerializeField] private SmoothedTransformProxy m_smoothedTransformProxy;
        [SerializeField] public Transform m_movementTargetingDebugBox;
        [SerializeField] public Transform m_movementTargetingDebugBoxCenter;
        [SerializeField] public Color m_debugLineColour;
        [SerializeField] private ActorComponentLookup m_actorComponentLookup;
        [SerializeField] private CinemachineTargetGroupAssigner_Boss m_cinemachineTargetGroupAssigner;
        [SerializeField, Tooltip("Only for non-attack linked effects.")]
        private BossGeneralEventTriggers[] m_bossGeneralEventTriggers;
        [SerializeField] private SmoothedTransformRotator m_smoothedTransformRotator;
        [SerializeField] protected Transform m_animationRoot;

        public BossDefinition Definition => m_definition; // 060009dd
        public Transform Target { get; private set; } // 060009de / 060009df

        private SystemRef<BossManager> m_bossManagerRef;
        private BossManager m_bossManager;
        private CharacterManager m_characterManager;
        private Transform m_transform;
        private int m_firedCount;
        private int m_currentHealth;
        private Vector3 m_originPoint;
        private readonly Dictionary<ActorAttachPointType, BossBodyComponents> m_attachPointDictionary =
            new Dictionary<ActorAttachPointType, BossBodyComponents>(HardlightEnumComparers.ActorAttachPointTypeComparer);
        private readonly Dictionary<HomingTarget, BossBodyComponents> m_homingTargetDictionary =
            new Dictionary<HomingTarget, BossBodyComponents>();
        private readonly TargetedEffect m_targetedEffectData = new TargetedEffect();
        private bool m_activeBossEntity;
        private bool m_hasBeenActiveThisLoop;
        private int m_movementPriority;
        private bool m_wasDisabled;
        private int m_layerMask;
        private int m_onlyEnemiesMask;
        private RaycastHit[] m_raycastHits = new RaycastHit[10];
        private const float ConeOfVisionDensityDegrees = 2f;

        public Bounds Bounds => m_colliderCollision.bounds; // 060009e0
        public int CurrentHealth => m_currentHealth; // 060009e1
        public bool IsActivePartOfMultiBodyBoss => m_activeBossEntity; // 060009e2
        public bool HasBeenActiveThisLoop => m_hasBeenActiveThisLoop; // 060009e3
        public int MovementPriority => m_movementPriority; // 060009e4
        public override float BrainMovementMagnitude => 0f; // 060009e5
        public SmoothedTransformRotator SmoothedTransformRotator => m_smoothedTransformRotator; // 060009e6
        public Transform AnimationRoot => m_animationRoot; // 060009e7

        // 060009e8: base first, position/storage before rotation, then live providers.
        protected override void Awake()
        {
            base.Awake();
            m_transform = transform;
            m_targetedEffectData.Target = m_transform;
            SetWorldPosition(m_transform.position);
            m_originPoint = WorldPosition;
            Storage.SetValue(BossFSMKeys.OriginPoint, m_originPoint);
            SetWorldRotation(m_transform.rotation);
            m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
            m_bossManagerRef.InvokeOnValid(RegisterBoss);
            ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(RegisterCharacterManager);
            m_layerMask = LayerMask.GetMask("Character", "Enemy");
            m_onlyEnemiesMask = LayerMask.GetMask("Enemy");
        }

        // 060009e9: a callback fault leaves m_wasDisabled set.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_wasDisabled) m_fsm?.InitialiseUser(this, null, null);
            m_wasDisabled = false;
        }

        // 060009ea: Animator/FSM use CLR null; this uses Unity object equality.
        protected override void OnDisable()
        {
            base.OnDisable();
            Animator?.Rebind();
            if (Storage != null && this != null && m_bossManager.IsRegistered(this))
                m_fsm?.ClearUser(this, null);
            m_wasDisabled = true;
        }

        protected override void OnDestroy() // 060009eb
        {
            if (m_bossManager != null && m_bossManager.IsRegistered(this))
                m_bossManager.Deregister(this);
            base.OnDestroy();
        }

        private void RegisterBoss(BossManager bossManager) // 060009ec
        {
            m_bossManager = bossManager;
            m_bossManager.Register(this);
        }

        private bool RegisterBossHitToManager() => m_bossManager.RegisterBossHit(); // 060009ed

        public void RestoreAllBossHealth() // 060009ee
        {
            m_currentHealth = m_definition.Traits.HealthPool;
            m_bossManager.RestoreHealthToPool(m_currentHealth);
        }

        public bool ManagerHasFlagForPhaseContinuation() => m_bossManager.HasFlagForPhaseContinuation(); // 060009ef

        private void RegisterCharacterManager(CharacterManager characterManager) // 060009f0
        {
            m_characterManager = characterManager;
            m_characterManager.RegisterOnCharacterChange(SetTarget, true);
        }

        public void InitialiseBoss() => Initialise(m_definition); // 060009f1
        private void SetTarget(Character character) => Target = character.transform; // 060009f2

        // 060009f3: this does not assign m_definition. Dictionary.Add retains the
        // original duplicate-key faults, and the proxy branch rereads the stored definition.
        private void Initialise(BossDefinition definition)
        {
            InitialiseTraits(definition.Traits, m_actorComponentLookup);
            InitialiseAbilities(null);
            foreach (BossBodyComponents component in m_bossInteractionComponents)
            {
                component.Initialise();
                if (component.HomingTarget != null)
                    m_homingTargetDictionary.Add(component.HomingTarget, component);
                m_attachPointDictionary.Add(component.AttachPoint, component);
            }
            InitialiseEffectSequences();
            VisualProxy = m_smoothedTransformProxy;
            if (VisualProxy != null && m_definition.Traits.DetachVisualProxy)
                VisualProxy.Detach(null);
            base.Initialise(definition, m_actorComponentLookup);
            m_actorComponentLookup.ActorFootsteps.InitialiseActor(this);
        }

        // 060009f4 and original generated local functions 06000a20/06000a21.
        // Recovery precedes vulnerable. The attack predicate reads BOTH values
        // before comparing them; do not short-circuit the second storage call.
        private void InitialiseEffectSequences()
        {
            foreach (BossBodyComponents component in m_bossInteractionComponents)
            {
                InitialiseAttackEffect(component.WindUpEffects, component.AttackType, BossEffectTriggerTypes.WindUp);
                InitialiseAttackEffect(component.AttackStrikeEffects, component.AttackType, BossEffectTriggerTypes.AttackStrike);
                InitialiseAttackEffect(component.RecoveryEffects, component.AttackType, BossEffectTriggerTypes.Recovery);
                InitialiseAttackEffect(component.VulnerableEffects, component.AttackType, BossEffectTriggerTypes.Vulnerable);
                InitialiseAttackEffect(component.InjuredEffects, component.AttackType, BossEffectTriggerTypes.Injured);
            }
            foreach (BossGeneralEventTriggers entry in m_bossGeneralEventTriggers)
                InitialiseEffect(entry.Effects, entry.Trigger);

            void InitialiseEffect(EffectSequence effectSequence, BossEffectTriggerTypes trigger)
            {
                if (effectSequence == null) return;
                effectSequence.SetTrigger(this,
                    () => Storage.GetValue(BossFSMKeys.BossEffectType, default(BossEffectTriggerTypes), true) == trigger);
            }

            void InitialiseAttackEffect(EffectSequence effectSequence, BossAttackTypes attack, BossEffectTriggerTypes trigger)
            {
                if (effectSequence == null) return;
                effectSequence.SetTrigger(this, () =>
                {
                    BossAttackTypes currentAttack = Storage.GetValue(BossFSMKeys.BossAttackType, default(BossAttackTypes), true);
                    BossEffectTriggerTypes currentTrigger = Storage.GetValue(BossFSMKeys.BossEffectType, default(BossEffectTriggerTypes), true);
                    return currentAttack == attack && currentTrigger == trigger;
                });
            }
        }

        // 060009f5: ignores isBeingDestroyed, no base close; deferred proxy destruction.
        protected override void DoClose(bool isBeingDestroyed)
        {
            CloseTraits();
            m_characterManager?.ReleaseOnCharacterChange(SetTarget);
            m_transform = null;
            m_characterManager = null;
            if (VisualProxy != null)
            {
                UnityEngine.Object.Destroy(VisualProxy.gameObject);
                VisualProxy = null;
            }
        }

        protected override void DoOnFixedUpdatePreFSM(float deltaTime) => UpdateAbilities(deltaTime); // 060009f6

        public override void DoOnFixedUpdatePostFSM(float deltaTime) // 060009f7
        {
            base.DoOnFixedUpdatePostFSM(deltaTime);
            m_transform.position = WorldPosition;
            m_transform.rotation = WorldRotation;
            if (m_definition.Traits.DetachVisualProxy)
            {
                if (VisualProxy == null || !VisualProxy.isActiveAndEnabled) return;
                Vector3 position = m_transform.position;
                SmoothedTransformProxy proxy = VisualProxy;
                Quaternion rotation = m_transform.rotation;
                proxy.UpdatePositionAndRotation(position, rotation, deltaTime);
            }
        }

        protected override void DoOnUpdate(float deltaTime) { } // 060009f8, original empty body
        protected override bool ShouldUpdate() => base.ShouldUpdate() && m_characterManager.CharacterValid; // 060009f9
        public override TimeCategory GetTimeCategory() => TimeCategory.EnemyMovement; // 060009fa

        protected override void UpdateBodyPosition(float deltaTime, out Vector3 position,
            out Quaternion rotation, out Vector3 velocity) // 060009fb
        {
            Vector3 displacement = WorldVelocity * deltaTime;
            m_transform.position = m_transform.position + displacement;
            position = m_transform.position;
            rotation = m_transform.rotation;
            velocity = WorldVelocity;
        }

        private void UpdateAbilities(float deltaTime) // 060009fc, foreach finally disposes
        {
            foreach (KeyValuePair<ActorAbilityType, ActorAbility> entry in m_abilities)
                entry.Value.Update(deltaTime);
        }

        private void UpdatePosition(float deltaTime) // 060009fd
        {
            Vector3 displacement = WorldVelocity * deltaTime;
            m_transform.position = m_transform.position + displacement;
        }

        public override void MovementStop(bool detectCollisions) => SetWorldVelocity(Vector3.zero); // 060009fe
        public override void MovementResume(Vector3 position, Vector3 velocity) // 060009ff
        {
            SetWorldPosition(position);
            SetWorldVelocity(velocity);
        }

        public BossTraits GetTraits() => m_definition.Traits; // 06000a00

        public void SetActiveHomingTarget(ActorAttachPointType attachPointType, bool active) // 06000a01
        {
            if (m_attachPointDictionary[attachPointType].HomingTarget == null) return;
            m_attachPointDictionary[attachPointType].HomingTarget.gameObject.SetActive(active);
            if (active) SetHazard(attachPointType, false);
        }

        public void SetHazard(ActorAttachPointType attachPointType, bool active) // 06000a02
        {
            if (m_attachPointDictionary[attachPointType].Hazard == null) return;
            m_attachPointDictionary[attachPointType].Hazard.enabled = active;
        }

        public void TriggerEffects() // 06000a03, real in-argument completion message and cached no-op
        {
            MessageManager manager = ProcessManager.GetSystemSafe<MessageManager>(null, true);
            if (manager == null) return;
            Component component = this;
            IEffectData effectData = m_targetedEffectData;
            manager.ComponentMessagesWithCompletion.PublishMessage(in component, in effectData, () => { }, -1);
        }

        public bool IsComponentDestroyed(ActorAttachPointType attachPointType) =>
            m_attachPointDictionary[attachPointType].Destroyed; // 06000a04

        public void ReceiveHit(HomingTarget target) // 06000a05
        {
            BossBodyComponents component = m_homingTargetDictionary[target];
            if (component.Destroyed) return;
            if (!m_bossManager.RegisterBossHit()) return;
            component.RegisterHit();
            --m_currentHealth;
            Storage.SetValue(BossFSMKeys.WasHit, true);
            if (m_currentHealth == 0) OnBossDefeated();
        }

        public void ReceiveHit() // 06000a06
        {
            if (!m_bossManager.RegisterBossHit()) return;
            --m_currentHealth;
            Storage.SetValue(BossFSMKeys.WasHit, true);
            if (m_currentHealth == 0) OnBossDefeated();
        }

        public void Stagger() => Storage.SetValue(BossFSMKeys.WasHit, true); // 06000a07
        private void OnBossDefeated() => m_bossManager.BossDefeated(this); // 06000a08

        public void FixDestroyedPart(ActorAttachPointType target) // 06000a09
        {
            BossBodyComponents component = m_attachPointDictionary[target];
            if (component.Destroyed)
            {
                component.RepairPart();
                ++m_currentHealth;
            }
        }

        public void ReceiveHit(Collider damagedCollider) { } // 06000a0a, original empty interface body

        // 06000a0b: read vulnerability even for hard failure; two live storage writes.
        public void RegisterHitPlayer(DamageAction damageAction, HazardType hazardType)
        {
            bool wasVulnerable = Storage.GetValue(BossFSMKeys.CurrentlyVulnerable, false, true);
            if (damageAction == DamageAction.HardFailure)
            {
                m_bossManager.BossHardFailure(this);
                return;
            }
            if (hazardType == HazardType.BossDamageDealer)
            {
                Storage.SetValue(BossFSMKeys.BossHitPlayer, true);
                return;
            }
            if (hazardType == HazardType.BossCrabClawWave && wasVulnerable)
            {
                Storage.SetValue(BossFSMKeys.BossHitPlayer, true);
                Storage.SetValue(BossFSMKeys.ForceNotVulnerable, true);
            }
        }

        public int GetHealthPool() => m_definition.Traits.HealthPool; // 06000a0c

        // 06000a0d: exact type tests, including the original CLR Type-null test.
        public Transform GetSpawnPoint<T>(ActorAttachPointType attach) where T : class
        {
            Type type = typeof(T);
            if (type == null) return null;
            if (type == typeof(Hazard)) return m_attachPointDictionary[attach].Hazard.transform;
            if (type == typeof(Transform)) return m_attachPointDictionary[attach].AttackCameraTargetSpawnPoint;
            if (type == typeof(Projectile)) return m_attachPointDictionary[attach].ProjectileSpawnPoint;
            if (type == typeof(Collider)) return m_attachPointDictionary[attach].Collider.transform;
            return null;
        }

        public void AddCameraLookAtTarget(Transform targetTransform) => m_cinemachineTargetGroupAssigner.AddTarget(targetTransform); // 06000a0e
        public void RemoveCameraLookAtTarget(Transform targetTransform) => m_cinemachineTargetGroupAssigner.RemoveTarget(targetTransform); // 06000a0f
        public void SetActiveBossEntity() => m_activeBossEntity = true; // 06000a10
        public void SetMovementPriority(int movement) => m_movementPriority = movement; // 06000a11

        public void ClearActiveBossEntity() // 06000a12
        {
            if (m_activeBossEntity) m_hasBeenActiveThisLoop = true;
            m_activeBossEntity = false;
        }

        public void ClearLoop() => m_hasBeenActiveThisLoop = false; // 06000a13
        public void Debug_ForceNextPhaseFSM() => Storage.SetValue(BossFSMKeys.ForceNextPhaseTrigger, true); // 06000a14

        // 06000a15: preserve unsorted ray-hit traversal, last matching distance,
        // original layer-mask versus layer-index comparison, and odd debug colors.
        // The Ray constructor supplies the sole normalization; direction stays raw
        // for debug and cone rotation. Infinite active cones remain an original hold.
        public bool HasLineOfSightOnCharacter(float maxDistance, bool fullConeMustBeClear, float coneOfVisionDegrees = 0f)
        {
            if (m_characterManager == null || !m_characterManager.TryGetCurrentCharacter(out Character character)) return false;
            Transform characterTransform = character.transform;
            Vector3 direction = characterTransform.position - WorldPosition;
            int count = Physics.RaycastNonAlloc(new Ray(WorldPosition, direction), m_raycastHits, maxDistance, m_layerMask);
            bool found = false;
            bool hitEnemy = false;
            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = m_raycastHits[i];
                if (hit.distance == 0f) continue;
                hitEnemy |= m_onlyEnemiesMask == hit.transform.gameObject.layer;
                if (hit.transform != characterTransform) continue;
                found = hit.transform == characterTransform;
                maxDistance = hit.distance;
            }
            Debug.DrawRay(WorldPosition, direction, found && !hitEnemy ? Color.red : Color.green);
            if (coneOfVisionDegrees == 0f || found != fullConeMustBeClear) return found;
            float angle = 0f;
            bool result = false;
            while (angle * 2f <= coneOfVisionDegrees)
            {
                angle += ConeOfVisionDensityDegrees;
                if (CastRay(Quaternion.Euler(Vector3.up * angle) * direction, ref maxDistance,
                    fullConeMustBeClear, characterTransform, out result)) return result;
                if (CastRay(Quaternion.Euler(Vector3.up * -angle) * direction, ref maxDistance,
                    fullConeMustBeClear, characterTransform, out result)) return result;
            }
            return found && !hitEnemy;
        }

        // 06000a16: result is written on normal returns, after any Unity faults.
        private bool CastRay(Vector3 direction, ref float maxDistance, bool fullConeMustBeClear,
            Transform characterTransform, out bool result)
        {
            if (Physics.Raycast(new Ray(WorldPosition, direction), out RaycastHit hit, maxDistance, m_onlyEnemiesMask))
            {
                bool matches = hit.transform == characterTransform;
                Debug.DrawRay(WorldPosition, direction, matches ? Color.red : Color.green);
                if (matches)
                {
                    maxDistance = hit.distance;
                    if (!fullConeMustBeClear)
                    {
                        result = true;
                        return true;
                    }
                }
                else if (fullConeMustBeClear)
                {
                    result = false;
                    return true;
                }
            }
            result = false;
            return false;
        }

        private VariableLengthLasers GetLaserEmitter(ActorAttachPointType attachPointType) =>
            m_attachPointDictionary[attachPointType].LaserEmitters; // 06000a17
        public Vector3 GetLaserEmitterPosition(ActorAttachPointType attachPointType) =>
            m_attachPointDictionary[attachPointType].LaserEmitters.transform.position; // 06000a18

        public void EnableLasers(bool enable, ActorAttachPointType attachPointType) // 06000a19
        {
            if (m_attachPointDictionary[attachPointType].LaserEmitters == null) return;
            m_attachPointDictionary[attachPointType].LaserEmitters.EnableLasers(enable);
        }

        // 06000a1a: transform captured before SetLaserPositions; later fresh array
        // reads, collider cast before LookAt, callback faults and Unity-null ordering retained.
        public void SetLaserPositions(Vector3[] positions, float hazardDepth, float hazardHeight,
            float hazardYOffset, ActorAttachPointType attachPointType, bool staticTargets)
        {
            VariableLengthLasers lasers = GetLaserEmitter(attachPointType);
            Transform cachedTransform = lasers.transform;
            lasers.SetLaserPositions(positions);
            if (staticTargets) return;
            BossBodyComponents component = m_attachPointDictionary[attachPointType];
            BoxCollider box = component.Collider as BoxCollider;
            lasers.transform.LookAt(positions[1], UpDirection);
            Vector3 start = cachedTransform.InverseTransformPoint(positions[0]);
            Vector3 end = cachedTransform.InverseTransformPoint(positions[1]);
            Vector3 mid = new Vector3((start.x + end.x) * 0.5f, hazardYOffset, (start.z + end.z) * 0.5f);
            lasers.SetPfxParentPosition(mid);
            float length = (positions[1] - positions[0]).magnitude;
            lasers.SetMidPositionedEmissionScale(length * 0.5f);
            lasers.SetStartAndEndPfx(start, end);
            if (box == null) return;
            box.center = mid;
            box.size = new Vector3(hazardDepth, hazardHeight, Mathf.Abs(length));
            component.Collider = box;
        }

        public void ShowVisualProxy() // 06000a1b
        {
            if (VisualProxy == null) return;
            VisualProxy.gameObject.SetActive(true);
        }

        public void HideVisualProxy() // 06000a1c
        {
            if (VisualProxy == null) return;
            VisualProxy.gameObject.SetActive(false);
        }

        public void ToggleVisualProxySSOA(bool active) => VisualProxy.ToggleSSOA(active); // 06000a1d

        public bool AnyPartMatchesCollider(Collider colliderToCheckAgainst) // 06000a1e
        {
            foreach (BossBodyComponents component in m_bossInteractionComponents)
                if (component.Collider == colliderToCheckAgainst) return true;
            return false;
        }

        // 06000a1f: implicit constructor allocates both dictionaries, TargetedEffect,
        // then the ten RaycastHit slots before calling the genuine Actor constructor.

        // Original Game.Runtime 020001d6, five complete methods and sixteen fields.
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class BossBodyComponents
        {
            public ActorAttachPointType AttachPoint;
            public BossAttackTypes AttackType;
            public HomingTarget HomingTarget;
            public int HitPoints = 1;
            public BossHazard Hazard;
            public Collider Collider;
            public VariableLengthLasers LaserEmitters;
            public Transform AttackCameraTargetSpawnPoint;
            public Transform ProjectileSpawnPoint;
            public EffectSequence WindUpEffects;
            public EffectSequence AttackStrikeEffects;
            public EffectSequence VulnerableEffects;
            public EffectSequence RecoveryEffects;
            public EffectSequence InjuredEffects;
            private int m_currentHitPoints;
            private bool m_destroyed;

            public bool Destroyed => m_destroyed; // 06000a22
            public void Initialise() // 06000a23
            {
                m_currentHitPoints = HitPoints;
                m_destroyed = false;
            }
            public void RegisterHit() // 06000a24, exactly zero rather than <= zero
            {
                --m_currentHitPoints;
                if (m_currentHitPoints == 0) m_destroyed = true;
            }
            public void RepairPart() // 06000a25, one increment and unconditional flag reset
            {
                ++m_currentHitPoints;
                m_destroyed = false;
            }
            // 06000a26: implicit constructor writes HitPoints=1 before Object base.
        }

        // Original Game.Runtime 020001d7, two public fields and only Object constructor.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class BossGeneralEventTriggers
        {
            public EffectSequence Effects;
            public BossEffectTriggerTypes Trigger;
            // 06000a27 implicit Object constructor. Natural cached Action 06000a28-2a
            // and two predicate display families 06000a2b-2e are emitted from the lambdas.
        }
    }
}
