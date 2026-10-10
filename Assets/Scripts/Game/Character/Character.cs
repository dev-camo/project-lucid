using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type020002aa; all89 own fields, with genuine native-derived
    // bodies added in original-class groups. This source graph is incomplete/unaccepted.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class Character : Actor, IGameplayModifiable, IDamageable
    {
        // Original 0x040009bf; native own offset0x0.
        private const TimeCategory CharacterTimeCategory = (TimeCategory)(-172618227);
        // Original 0x040009c0; native own offset0x150.
        private DataManager m_dataManager;
        // Original 0x040009c1; native own offset0x158.
        private SystemRef<SaveManager> m_saveManagerRef;
        // Original 0x040009c2; native own offset0x160.
        private CharacterManager m_characterManager;
        // Original 0x040009c3; native own offset0x168.
        private SystemRef<CinemachineCameraManager> m_cinemachineCameraManagerRef;
        // Original 0x040009c4; native own offset0x170.
        private readonly SystemRef<GameplayIslandManager> m_gameplayIslandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>();
        // Original 0x040009c5; native own offset0x178.
        [SerializeField]
        [Tooltip("The rigid body that controls the character's physics motion.")]
        private Rigidbody m_body;
        // Original 0x040009c6; native own offset0x180.
        [Tooltip("The trigger collider that is used to enlarge the character's hit volume for certain abilities.")]
        [SerializeField]
        protected SphereCollider m_colliderTrigger;
        // Original 0x040009c7; native own offset0x188.
        [SerializeField]
        [Tooltip("The custom gravity that acts on the rigid body.")]
        private RigidbodyGravity m_customGravity;
        // Original 0x040009c8; native own offset0x190.
        [SerializeField]
        [Tooltip("The gravity transform proxy to match current orientation.")]
        private Transform m_gravityProxy;
        // Original 0x040009c9; native own offset0x198.
        [SerializeField]
        [Tooltip("A proxy transform used to orientate other systems following the character.")]
        private Transform m_customProxy;
        // Original 0x040009ca; native own offset0x1a0.
        [SerializeField]
        private TransformFakeParent m_fakeParent;
        // Original 0x040009cb; native own offset0x1a8.
        [SerializeField]
        private TerrainTrackerMetadataKeyLookupDefinition m_terrainTrackerMetadataKeyLookup;
        // Original 0x040009cc; native own offset0x1b0.
        [SerializeField]
        private GameObject m_blobShadowObject;
        // Original 0x040009cd; native own offset0x1b8.
        [SerializeField]
        private CharacterStickyControlsDefinition m_stickyControlsDefinition;
        // Original 0x040009ce; native own offset0x1c0.
        [SerializeField]
        [Tooltip("FSM storage values that persist between characters when quick swapping.")]
        [Header("Character swap settings")]
        private List<string> m_characterSwapFSMValuesToPersist;
        // Original 0x040009cf; native own offset0x1c8.
        [SerializeField]
        [Tooltip("Time scales to animate immediately after character swap.")]
        private TimeScaledCategoryAnimation m_swapTimeAnimation;
        // Original 0x040009d0; native own offset0x1d0.
        [Tooltip("Particle effect spawner to use when waiting for next character to spawn during character swap.")]
        [SerializeField]
        private ParticleEffectSpawner m_swapGapParticleSpawner;
        // Original 0x040009d1; native own offset0x1d8.
        [Tooltip("Particle effect spawner override to use when waiting for certain characters to spawn during character swap.")]
        [SerializeField]
        private SerializableDictionary<CharacterId,ParticleEffectSpawner> m_swapGapParticleSpawnerOverride = new SerializableDictionary<CharacterId, ParticleEffectSpawner>(HardlightEnumComparers.CharacterIdComparer);
        // Original 0x040009d2; native own offset0x1e0.
        [SerializeField]
        [Tooltip("Animation definition to use when just starting a character swap.")]
        private ActorAnimationDefinition m_swapOutAnimationDefinition;
        // Original 0x040009d3; native own offset0x1e8.
        [SerializeField]
        [Tooltip("Animation definition to use when just spawned in after a character swap.")]
        private ActorAnimationDefinition m_swapInAnimationDefinition;
        // Original 0x040009d4; native own offset0x1f0.
        [Tooltip("UI visibility overrides to set during character swap when waiting for next character to spawn.")]
        [SerializeField]
        private UIVisibilityGroupOverrider m_swapGapVisibilityGroupOverrider;
        // Original 0x040009d5; native own offset0x1f8.
        private ParticleEffectSpawner m_swapGapParticle;
        // Original 0x040009d6; native own offset0x200.
        private BlobShadowRaycaster m_blobShadowRaycaster;

        // Native-derived original method bodies follow; absent methods are unresolved
        // rather than emitted as default returns, exceptions or dependency shells.
        // 06000eac..ee6: actual original accessors. Actor base and genuine field
        // dependencies remain source-open; original method order is not yet approved.
        public SaveDataSettings SaveDataSettings => m_saveManagerRef.Get().GetSaveDataSettings();
        public TransformFakeParent FakeParent => m_fakeParent;
        public GameObject BlobShadowObject => m_blobShadowObject;
        // Original 0x040009d7; native own offset0x208.
        // 0x06000eaf/0x06000eb0; original natural compiler backing/accessors.
        public CharacterTracker Tracker { get; private set; }
        // Original 0x040009d8; native own offset0x210.
        // 0x06000eb1/0x06000eb2; original natural compiler backing/accessors.
        public CharacterCollider Collider { get; private set; }
        public UIVisibilityGroupOverrider SwapGapVisibilityGroupOverrider => m_swapGapVisibilityGroupOverrider;
        // Original 0x040009d9; native own offset0x218.
        // 0x06000eb4/0x06000eb5; original natural compiler backing/accessors.
        public CharacterQueuedActions QueuedActions { get; private set; }
        // Original 0x040009da; native own offset0x220.
        private CharacterBrain m_brain;
        // Original 0x040009db; native own offset0x228.
        private CapsuleCollider m_capsuleColliderCollision;
        public Collider ColliderTrigger => m_colliderTrigger;
        public CharacterTraits Traits => Definition.Traits;
        // Original 0x040009dc; native own offset0x230.
        // 0x06000eb8/0x06000eb9; original natural compiler backing/accessors.
        public CharacterSettings Settings { get; private set; }
        // Original 0x040009dd; native own offset0x238.
        // 0x06000eba/0x06000ebb; original natural compiler backing/accessors.
        public GlobalConstantDefinition_Character Constants { get; private set; }
        // Original 0x040009de; native own offset0x240.
        // 0x06000ebc/0x06000ebd; original natural compiler backing/accessors.
        public CharacterTracking ActiveTracking { get; private set; }
        // Original 0x040009df; native own offset0x248.
        private readonly Dictionary<CharacterTrackingType,CharacterTracking> m_trackingTypes = new Dictionary<CharacterTrackingType, CharacterTracking>();
        // Original 0x040009e0; native own offset0x250.
        private float m_fixedDeltaTime;
        // Original 0x040009e1; native own offset0x258.
        private Coroutine m_fixedUpdateCoroutine;
        // Original 0x040009e2; native own offset0x260.
        private SystemRef<TrackManager> m_trackManagerRef;
        // Original 0x040009e3; native own offset0x268.
        private Vector3 m_worldPositionLastFrame;
        // Original 0x040009e4; native own offset0x274.
        private Vector3 m_worldVelocityNormalisedLastFrame;
        // These native reads deliberately use the typed reference without diagnosis.
        public TrackManager TrackManager => m_trackManagerRef.GetSafe();
        public Bounds LevelBounds => m_trackManagerRef.GetSafe().Bounds;
        public float LateralBoundsThreshold => m_capsuleColliderCollision.radius;
        public Bounds Bounds => m_capsuleColliderCollision.bounds;
        public float Height => m_capsuleColliderCollision.height;
        public float HalfHeight => m_capsuleColliderCollision.height * 0.5f;
        public LayerMask TrackMask => Settings.Collider.TrackMask;
        public LayerMask CollisionMask => Settings.Collider.CollisionMask;
        // Original 0x040009e5; native own offset0x280.
        // 0x06000ec6/0x06000ec7; original natural compiler backing/accessors.
        public Vector3 GravityNormalisedLastFrame { get; private set; }
        public override Vector3 Gravity => m_customGravity.Gravity;
        public override Vector3 GravityNormalised => m_customGravity.GravityNormalised;
        public Transform GravityProxy => m_gravityProxy;
        public Transform CustomProxy => m_customProxy;
        // Original 0x040009e6; native own offset0x28c.
        // 0x06000ecc/0x06000ecd; original natural compiler backing/accessors.
        public bool ControllerEnabled { get; private set; }
        // Original 0x040009e7; native own offset0x290.
        // 0x06000ece/0x06000ecf; original natural compiler backing/accessors.
        public Vector2 RawControllerMovement { get; private set; }
        // Original 0x040009e8; native own offset0x298.
        // 0x06000ed0/0x06000ed1; original natural compiler backing/accessors.
        public float RawControllerMovementMagnitude { get; private set; }
        // Original 0x040009e9; native own offset0x29c.
        // 0x06000ed2/0x06000ed3; original natural compiler backing/accessors.
        public Vector3 RawIntendedForward { get; private set; }
        // Original 0x040009ea; native own offset0x2a8.
        // 0x06000ed4/0x06000ed5; original natural compiler backing/accessors.
        public Vector2 ControllerMovement { get; private set; }
        // Original 0x040009eb; native own offset0x2b0.
        // 0x06000ed6/0x06000ed7; original natural compiler backing/accessors.
        public float ControllerMovementMagnitude { get; private set; }
        // Original 0x040009ec; native own offset0x2b4.
        private Vector3 m_cameraForwardOnCharacterPlane;
        // Original 0x040009ed; native own offset0x2c0.
        private Vector3 m_cameraForward;
        // Original 0x040009ee; native own offset0x2cc.
        private Vector3 m_cameraWorldUp;
        // Original 0x040009ef; native own offset0x2d8.
        private Vector3 m_cameraPosition;
        // Original 0x040009f0; native own offset0x2e4.
        private Quaternion m_cameraRotation;
        // Original 0x040009f1; native own offset0x2f4.
        private Matrix4x4 m_cameraWorldToProjectionMatrix;
        // Original 0x040009f2; native own offset0x334.
        private CameraType m_abilityCameraTypeOverride;
        // Original 0x040009f3; native own offset0x338.
        private StackableDataHandle m_abilityCameraTypeOverrideHandle;
        // Original 0x040009f4; native own offset0x340.
        private StackableDataHandle m_abilityCameraSettingsOverrideHandle;
        // Original 0x040009f5; native own offset0x348.
        private StackableDataHandle m_abilityCameraHeadingOverrideHandle;
        // Original 0x040009f6; native own offset0x350.
        private StackableDataHandle m_gameInputPauseDisabledHandle;
        // Native7054ec reads RawControllerMovementMagnitude, not the applied magnitude.
        public bool HasControllerMovement => RawControllerMovementMagnitude > 0.0001f;
        public override float BrainMovementMagnitude => ControllerMovementMagnitude;
        public Vector3 CameraForwardOnCharacterPlane => m_cameraForwardOnCharacterPlane;
        public Vector3 CameraForward => m_cameraForward;
        public Vector3 CameraWorldUp => m_cameraWorldUp;
        public Vector3 CameraPosition => m_cameraPosition;
        public Quaternion CameraRotation => m_cameraRotation;
        public Matrix4x4 CameraWorldToProjectionMatrix => m_cameraWorldToProjectionMatrix;
        // Original 0x040009f7; native own offset0x358.
        // 0x06000ee0/0x06000ee1; original natural compiler backing/accessors.
        public float TimeSecondsLastAirborne { get; private set; }
        // Original 0x040009f8; native own offset0x35c.
        // 0x06000ee2/0x06000ee3; original natural compiler backing/accessors.
        public CharacterId IdType { get; private set; }
        // Original 0x040009f9; native own offset0x360.
        // 0x06000ee4/0x06000ee5; original natural compiler backing/accessors.
        public Vector3 AppliedConstantDirectionModifier { get; private set; }
        // Original 0x040009fa; native own offset0x370.
        private StackableData m_modifiers = new StackableData();
        // Original 0x040009fb; native own offset0x378.
        private readonly Dictionary<StackableDataHandle,Action> m_modifierOverridesOnCancel = new Dictionary<StackableDataHandle, Action>();
        // Original 0x040009fc; native own offset0x380.
        private CharacterReplay m_replay;
        // Exact ordered >0 test, not Unity Vector3 approximate equality to zero.
        public bool HasConstantDirectionModifier => AppliedConstantDirectionModifier.sqrMagnitude > 0f;
        // Original 0x040009fd; native own offset0x388.
        // 0x06000ee7/0x06000ee8; original natural compiler backing/accessors.
        public CharacterBoostStamina BoostStamina { get; private set; }
        // Original 0x040009fe; native own offset0x390.
        // 0x06000ee9/0x06000eea; original natural compiler backing/accessors.
        public CharacterChaosStamina ChaosStamina { get; private set; }
        // Original 0x040009ff; native own offset0x398.
        // 0x06000eeb/0x06000eec; original natural compiler backing/accessors.
        public CharacterHomingPool HomingPool { get; private set; }
        // Original 0x04000a00; native own offset0x3a0.
        // 0x06000eed/0x06000eee; original natural compiler backing/accessors.
        public CharacterSpinDashRoll SpinDashRoll { get; private set; }
        // Original 0x04000a01; native own offset0x3a8.
        public CharacterColliderTrigger OnCharacterTriggerEnter;
        // Original 0x04000a02; native own offset0x3b0.
        public CharacterColliderTrigger OnCharacterTriggerExit;
        // Original 0x04000a03; native own offset0x3b8.
        public Action<CharacterCollisionData,Collision> OnCollisionDataEnterEvent;
        // Original 0x04000a04; native own offset0x3c0.
        public Action<CharacterCollisionData,Collision> OnCollisionDataStayEvent;
        // Original 0x04000a05; native own offset0x3c8.
        public Action<CharacterCollisionData,Collision> OnCollisionDataExitEvent;
        // Original 0x04000a06; native own offset0x3d0.
        public Action<Character> OnFixedUpdatePreFSM = _ => { };
        // Original 0x04000a07; native own offset0x3d8.
        public Action<Character> OnFixedUpdatePostFSM = _ => { };
        // Original 0x04000a08; native own offset0x3e0.
        public Action OnTakeDamage;
        // Original 0x04000a09; native own offset0x0.
        private const float TimeDilationThresholdMin = 0.1f;
        // Original 0x04000a0a; native own offset0x0.
        private const float TimeDilationThresholdMax = 10f;
        // Original 0x04000a0b; native own offset0x3e8.
        private float m_timeDilationPreviousFrame = 1f;
        // Original 0x04000a0c; native own offset0x3ec.
        private float m_timeDilationCurrentFrame = 1f;
        // Original 0x04000a0d; native own offset0x3f0.
        private bool m_swapPrepareIsInProgress;
        // Original 0x04000a0e; native own offset0x3f1.
        private bool m_swapStartIsInProgress;
        // Original 0x04000a0f; native own offset0x3f4.
        private float m_switchCooldownTimer;
        // Original 0x04000a10; native own offset0x3f8.
        private bool m_restartIsInProgress;
        // Original 0x04000a11; native own offset0x400.
        // 0x06000eef/0x06000ef0; original natural compiler backing/accessors.
        public CharacterDefinition Definition { get; private set; }
        // Original 0x04000a12; native own offset0x408.
        private Transform m_blobShadowRigRoot;
        // Original 0x04000a13; native own offset0x410.
        private bool m_uiEnabled = true;
        // Original 0x04000a14; native own offset0x411.
        private bool m_bodyWasKinematic;
        // Original 0x04000a15; native own offset0x418.
        private readonly Dictionary<CharacterAbilityUIType,UIContainerCharacterAbility> m_characterAbilityUI = new Dictionary<CharacterAbilityUIType, UIContainerCharacterAbility>(HardlightEnumComparers.CharacterAbilityUITypeComparer);
        // Original 0x04000a16; native own offset0x420.
        private bool m_retainImpulses;
        // Original 0x04000a17; native own offset0x428.
        private readonly Dictionary<GraphStorageKey,object> m_fsmObjectsCached = new Dictionary<GraphStorageKey, object>();

        // Exact original nested delegate020002ab; its four runtime-generated methods
        // are native context only, not separately recovered authored source bodies.
        public delegate void CharacterColliderTrigger(Collider otherCollider);

        // 06000ef1; ARM705658. Original private method hides the base Awake method;
        // base construction of storage precedes the required blob-shadow component read.
        private void Awake()
        {
            base.Awake();
            m_blobShadowRaycaster = m_blobShadowObject.GetComponent<BlobShadowRaycaster>();
        }

        // 06000ef2; ARM7056c8. Native release code leaves enableDebug unused. Keep
        // the original system-ref registration, tracking and callback boundaries:
        // construction happens before abilities and virtual placement, then the
        // real proxy snaps independently. Receiver reads after callbacks stay live.
        public void Initialise(CharacterDefinition definition, Transform lastRespawnPoint,
            Vector3 position, Quaternion rotation, Vector3 velocity,
            CharacterComponentLookup characterComponentLookup, bool enableDebug,
            IReadOnlyDictionary<ActorAbilityType, AbilityDefinition> abilityOverrides)
        {
            Definition = definition;
            ActorName = Hardlight.Localisation.StringTable.GetString(definition.Name);
            Settings = definition.Settings;
            IdType = definition.Id;
            m_capsuleColliderCollision = m_colliderCollision as CapsuleCollider;
            InitialiseTraits(definition.Traits, characterComponentLookup);
            ToggleFormRenderers(false);
            m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>(null, true);
            m_cinemachineCameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
            m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
            m_characterManager = ProcessManager.GetSystem<CharacterManager>(null, true);
            m_dataManager = ProcessManager.GetSystem<DataManager>(null, true);
            m_fixedDeltaTime = 0f;
            Constants = m_dataManager.GlobalConstantDefinitions[GlobalConstantType.Character]
                as GlobalConstantDefinition_Character;
            base.Initialise(definition, characterComponentLookup);
            AddTracker<CharacterTrackingColliderFree>();
            AddTracker<CharacterTrackingColliderTracker>();
            AddTracker<CharacterTrackingSurfaceTracker>();
            Tracker = new CharacterTracker(this, m_trackManagerRef.GetSafe(), m_customGravity);
            Collider = new CharacterCollider(this, m_trackManagerRef.GetSafe(), m_terrainTrackerMetadataKeyLookup);
            HomingPool = new CharacterHomingPool(this, m_dataManager.TargetingTypePriorityDefinitions);
            DetectTracking();
            m_body.mass = Definition.Traits.Mass;
            m_customGravity.enabled = true;
            m_customGravity.CanApplyForce = true;
            InitialiseModifiers();
            if (!AnySwapInProgress()) AllowCollisions(false);
            VisualProxy = characterComponentLookup.SmoothedTransformProxy;
            VisualProxy.Detach(null);
            Audio.SetActive(false);
            if (m_blobShadowObject != null)
            {
                m_blobShadowObject.transform.SetParent(VisualProxy.transform);
                m_blobShadowRaycaster.EnforceLocalPosition();
                m_blobShadowObject.SetActive(true);
            }
            RestoreFSMValues();
            BoostStamina = new CharacterBoostStamina(this);
            ChaosStamina = new CharacterChaosStamina(this);
            SpinDashRoll = new CharacterSpinDashRoll(this);
            InitialiseAbilities(abilityOverrides);
            SetWorldPosition(position);
            SetWorldRotation(rotation);
            SetWorldVelocity(velocity);
            VisualProxy.SnapToLocation(position, rotation);
            SetFSMPauseValues();
            Storage.SetValue<Transform>(ActorFSMKeys.LastRespawnPoint, lastRespawnPoint);
            if (m_pfxController != null)
            {
                OnCollisionDataEnterEvent += m_pfxController.OnCollisionDataEnter;
                OnCollisionDataExitEvent += m_pfxController.OnCollisionDataExit;
            }
            m_trackManagerRef.GetSafe().BodyCollisionCanModify.Add(m_body.GetInstanceID(), BodyCollisionCanModify);
            characterComponentLookup.ActorFootsteps.InitialiseActor(this);
            m_timeDilationPreviousFrame = 1f;
            m_timeDilationCurrentFrame = 1f;
            m_blobShadowRigRoot = characterComponentLookup.BlobShadowRigRoot;
            m_gravityProxy.parent = null;
            m_customProxy.parent = null;
        }

        // 06000ef3; ARM707078. StartOutput precedes the live replay-mode read;
        // the selected real brain initializes before queued actions. Required
        // camera registration may invoke its callback immediately. The fixed
        // coroutine is started only when absent, before SkipLevelEnter is stored.
        public void OnStart(Vector3 velocity, bool skipLevelEnter = true)
        {
            m_replay = new CharacterReplay(this);
            m_replay.StartOutput();
            m_brain = m_replay.InputActive
                ? (CharacterBrain)new CharacterBrain_Replay()
                : new CharacterBrain_Player(Constants.ControlsMappingDefinition);
            m_brain.Initialise();
            QueuedActions = new CharacterQueuedActions(this);
            m_cinemachineCameraManagerRef.InvokeOnValid(OnCameraManagerInitialised);
            AllowCollisions(true);
            MovementResume(WorldPosition, velocity);
            Audio.SetActive(true);
            VisualProxy.gameObject.SetActive(true);
            if (!AnySwapInProgress()) ToggleFormRenderers(true);
            RegisterTrackManager();
            if (m_fixedUpdateCoroutine == null)
                m_fixedUpdateCoroutine = StartCoroutine(FixedUpdateCoroutine());
            Storage.SetValue(ActorFSMKeys.SkipLevelEnter, skipLevelEnter);
        }

        // 06000ef4; ARM7075e4. Playback input activates the original Replay camera;
        // the returned override handle is intentionally not stored by this callback.
        private void OnCameraManagerInitialised(CinemachineCameraManager cameraManager)
        {
            if (m_replay.InputActive) cameraManager.ApplyCameraTypeOverride(CameraType.Replay);
        }
        // 06000ef5; ARM707608. Settings/heading are refreshed only when TYPE changes.
        // Removal callbacks run before their handle fields are cleared; the live type
        // field is reread after those removals, rather than reusing the caller's value.
        public void UpdateAbilityCameraOverride(CameraType cameraTypeOverride,
            Nullable<CameraProxyTargetSettings> cameraSettingsOverride = null, CameraHeadingOverride cameraHeadingOverride = null)
        {
            if (m_abilityCameraTypeOverride == cameraTypeOverride) return;
            CinemachineCameraManager cameraManager = m_cinemachineCameraManagerRef.Get();
            m_abilityCameraTypeOverride = cameraTypeOverride;
            if (m_abilityCameraSettingsOverrideHandle != null)
            {
                cameraManager.RemoveCameraProxySettingOverride(m_abilityCameraSettingsOverrideHandle);
                m_abilityCameraSettingsOverrideHandle = null;
            }
            if (m_abilityCameraHeadingOverrideHandle != null)
            {
                cameraManager.RemoveCameraSettingOverride(m_abilityCameraHeadingOverrideHandle);
                m_abilityCameraHeadingOverrideHandle = null;
            }
            CameraType currentType = m_abilityCameraTypeOverride;
            StackableDataHandle typeHandle = m_abilityCameraTypeOverrideHandle;
            if (currentType == CameraType.None)
            {
                cameraManager.RemoveCameraSettingOverride(typeHandle);
                m_abilityCameraTypeOverrideHandle = null;
                return;
            }
            if (typeHandle != null) cameraManager.ApplyCameraTypeOverride(currentType, typeHandle);
            else m_abilityCameraTypeOverrideHandle = cameraManager.ApplyCameraTypeOverride(currentType);
            if (cameraSettingsOverride.HasValue)
                cameraManager.SetCameraProxySettings(cameraSettingsOverride.Value, ref m_abilityCameraSettingsOverrideHandle);
            if (cameraHeadingOverride != null)
                m_abilityCameraHeadingOverrideHandle = cameraManager.ApplyCameraSettingOverride(
                    CameraSettingType.HeadingOverride, cameraHeadingOverride);
        }
        // 06000ef6; ARM7077fc. The original character-specific animator retains its
        // authored animation-settings asset and replaces the genuine Actor handler.
        protected override void CreateAnimatorHandler(Animator animator)
        {
            Animator = new CharacterAnimator(animator, Settings.AnimationSettings);
        }
        // 06000ef7; ARM70787c. Replacing tracking constructs the original tracker
        // first, then retrieves the auto-registered original TrackManager system ref.
        public void ReplaceTrackManager(TrackManager trackManager)
        {
            Tracker = new CharacterTracker(this, trackManager, m_customGravity);
            m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>(null, true);
        }
        // 06000ef8/9; CLR null-safe system-ref reads are intentionally passed to the
        // real tracker/collider handlers. Registration rereads the live ref per call.
        public void RegisterTrackManager()
        {
            Tracker.SetTrackableSurfaces(m_trackManagerRef.GetSafe());
            Collider.RegisterTrackManager(m_trackManagerRef.GetSafe());
        }
        public void UnregisterTrackManager()
        {
            Collider.UnregisterTrackManager(m_trackManagerRef.GetSafe());
        }

        // 06000efa / ARM7079b4. Previous kinematic state gates the actual current
        // body queries; the Rigidbody reference is reloaded between each query.
        public bool IsActive() => !m_bodyWasKinematic && !m_body.isKinematic && !m_body.IsSleeping();

        // 06000efb/fc; ARM707a18/707b24. Original body sleep/collision/coroutine order.
        public override void MovementStop(bool detectCollisions = false)
        {
            SetWorldVelocity(Vector3.zero);
            m_body.Sleep();
            m_body.detectCollisions = detectCollisions;
            TryStopFixedUpdateCoroutine();
        }
        public override void MovementResume(Vector3 position, Vector3 velocity)
        {
            m_body.detectCollisions = true;
            m_body.WakeUp();
            SetWorldPosition(position);
            SetWorldVelocity(velocity);
            SetTimeScaledBodyVelocity(velocity);
            TryStartFixedUpdateCoroutine();
        }
        // 06000efd; ARM707d6c. Publish the restart flag before either collider call.
        public void OnRestart()
        {
            m_restartIsInProgress = true;
            m_colliderTrigger.enabled = false;
            AllowCollisions(false);
        }

        // 06000efe; ARM707cc0. Preserve sleeping status after the physics write,
        // and capture its receiver before the time-scale query.
        public void SetTimeScaledBodyVelocity(Vector3 characterVelocity)
        {
            if (m_body.isKinematic) return;
            bool wasSleeping = m_body.IsSleeping();
            Rigidbody body = m_body;
            float ratio = GetPhysicsTimeDilationRatio();
            body.velocity = characterVelocity / ratio;
            if (wasSleeping) m_body.Sleep();
        }
        // 06000eff; ARM707f44. Kinematic bodies use the Actor's stored velocity;
        // the Character override is intentionally bypassed before applying the ratio.
        public Vector3 GetTimeScaledBodyVelocity()
        {
            Vector3 velocity = m_body.isKinematic ? base.WorldVelocity : m_body.velocity;
            float ratio = GetPhysicsTimeDilationRatio();
            return velocity * ratio;
        }

        // 06000f00; ARM707db8. Original UnityGlobal/PlayerPhysics ratio and zero
        // comparisons; player movement time is a different category.
        private float GetPhysicsTimeDilationRatio()
        {
            if (m_timeManagerRef == null || m_timeManagerRef.IsNull()) return 1f;
            TimeManager timeManager = m_timeManagerRef.Get();
            float globalScale = timeManager.GetTimescale(TimeCategory.UnityGlobal);
            float physicsScale = timeManager.GetTimescale(TimeCategory.PlayerPhysics);
            if (Mathf.Approximately(globalScale, 0f) || Mathf.Approximately(physicsScale, 0f)) return 1f;
            return Mathf.Clamp(globalScale / physicsScale, TimeDilationThresholdMin, TimeDilationThresholdMax);
        }

        // 06000f01; ARM707fbc. The optional delay is deliberately untracked. Its
        // natural callback reads live storage and removes the input handle before
        // enabling the real camera system. Level flags follow the shadow binding.
        public void OnEnterLevel(float controlsDelayTime)
        {
            if (Storage.HasValue<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle))
            {
                Hardlight.Utils.CoroutineUtils.Delay(() =>
                {
                    StackableDataHandle handle = Storage.GetValue<StackableDataHandle>(
                        ActorFSMKeys.InputOverrideModifierHandle, null, true);
                    if (handle != null) RemoveModifierOverrides(handle);
                    Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle);
                    ProcessManager.GetSystem<CinemachineCameraManager>().SetControlsEnabled(true);
                }, controlsDelayTime);
            }
            m_blobShadowObject.transform.SetParent(VisualProxy.transform);
            m_blobShadowRaycaster.EnforceLocalPosition();
            Storage.RemoveValue<bool>(ActorFSMKeys.IsExitingLevel);
            Storage.SetValue(ActorFSMKeys.IsEnteringLevel, false);
            Storage.SetValue(ActorFSMKeys.SkipLevelEnter, true);
        }
        // 06000f02; ARM7082cc. Remove the typed respawn Transform first, then
        // publish the authored exiting-level flag through a fresh storage read.
        public void OnExitLevel()
        {
            Storage.RemoveValue<Transform>(ActorFSMKeys.LastRespawnPoint);
            Storage.SetValue(ActorFSMKeys.IsExitingLevel, true);
        }
        // 06000f03 / ARM708474. Present false and absent entries both return false.
        public bool IsExitingLevel() => Storage.TryGetValue(ActorFSMKeys.IsExitingLevel, out bool exiting) && exiting;
        // 06000f04; ARM70851c. Replay stops before detaching live PFX callbacks.
        // The time animation disables only while destroying; stamina/FSM caches
        // are captured before the original Actor close pipeline starts.
        public override void Close(bool isBeingDestroyed = false)
        {
            if (m_replay != null) m_replay.EndOutput();
            if (m_pfxController != null)
            {
                OnCollisionDataEnterEvent -= m_pfxController.OnCollisionDataEnter;
                OnCollisionDataExitEvent -= m_pfxController.OnCollisionDataExit;
            }
            if (isBeingDestroyed) ActivateSwapTimeAnimation(false);
            if (BoostStamina != null) BoostStamina.CacheFSMValues();
            CacheFSMValues();
            base.Close(isBeingDestroyed);
        }
        // 06000f05; ARM708b1c. Clear each live reference only after its original
        // close callback succeeds. Track-manager registration is reread for the
        // collider, and reusable blob parenting differs from destruction cleanup.
        protected override void DoClose(bool isBeingDestroyed)
        {
            if (m_fixedUpdateCoroutine != null)
            {
                StopCoroutine(m_fixedUpdateCoroutine);
                m_fixedUpdateCoroutine = null;
            }
            EnableGameInput(true);
            CloseAbilities();
            if (m_brain != null)
            {
                m_brain.Close();
                m_brain = null;
            }
            QueuedActions = null;
            if (BoostStamina != null)
            {
                BoostStamina.Close();
                BoostStamina = null;
            }
            if (ChaosStamina != null)
            {
                ChaosStamina.Close();
                ChaosStamina = null;
            }
            if (SpinDashRoll != null)
            {
                SpinDashRoll.Close();
                SpinDashRoll = null;
            }
            m_trackingTypes.Clear();
            TrackManager trackManager = m_trackManagerRef.GetSafe();
            if (Tracker != null)
            {
                Tracker.Close(trackManager, m_customGravity);
                Tracker = null;
            }
            if (trackManager != null)
            {
                trackManager.BodyCollisionCanModify.Remove(m_body.GetInstanceID());
                Collider.UnregisterTrackManager(m_trackManagerRef.GetSafe());
            }
            if (Collider != null)
            {
                Collider.Close(trackManager);
                Collider = null;
            }
            if (HomingPool != null)
            {
                HomingPool.Close();
                HomingPool = null;
            }
            if (!isBeingDestroyed && m_blobShadowObject != null)
            {
                m_blobShadowObject.transform.SetParent(transform);
                m_blobShadowObject.SetActive(false);
            }
            if (m_cinemachineCameraManagerRef.IsValid())
                UpdateAbilityCameraOverride(CameraType.None, null, null);
            CloseTraits();
            if (isBeingDestroyed)
            {
                if (m_gravityProxy != null)
                {
                    UnityEngine.Object.Destroy(m_gravityProxy.gameObject);
                    m_gravityProxy = null;
                }
                if (m_customProxy != null)
                {
                    UnityEngine.Object.Destroy(m_customProxy.gameObject);
                    m_customProxy = null;
                }
            }
        }

        // 06000f06; ARM7092a8. Original pre-FSM update order is observable through
        // the public callback and replay host, so no initialization/null shortcuts apply.
        protected override void DoOnFixedUpdatePreFSM(float deltaTime)
        {
            base.DoOnFixedUpdatePreFSM(deltaTime);
            OnFixedUpdatePreFSM(this);
            m_replay.OutputPreFSM(m_body);
            m_fixedDeltaTime = deltaTime;
            if (m_bodyWasKinematic || m_body.isKinematic || m_body.IsSleeping())
                SetWorldVelocity((WorldPosition - m_worldPositionLastFrame) / deltaTime);
            ProcessBrain();
            UpdateCameraCache();
            RawIntendedForward = CharacterMovementUtilities.GetIntendedForward(this, RawControllerMovement);
            m_timeDilationPreviousFrame = m_timeDilationCurrentFrame;
            m_timeDilationCurrentFrame = GetPhysicsTimeDilationRatio();
            Collider.PerformGroundCheck();
            Collider.Finalise();
            UpdateStickyControls(deltaTime);
            Tracker.Update(deltaTime);
            BoostStamina.Update(m_brain, deltaTime);
            ChaosStamina.Update(m_brain, deltaTime);
            SpinDashRoll.Update(m_brain, deltaTime);
            UpdateAbilities(deltaTime);
            HomingPool.Update(deltaTime);
        }
        // 06000f07; ARM70a3b8. The genuine stamina receiver precedes the brain read.
        public void TriggerChaosDeactivation() => ChaosStamina.TriggerDeactivation(m_brain);

        // 06000f08; ARM70943c. The original pre-FSM method contains this same flow.
        private void UpdateVelocity(float deltaTime)
        {
            if (m_bodyWasKinematic || m_body.isKinematic || m_body.IsSleeping())
                SetWorldVelocity((WorldPosition - m_worldPositionLastFrame) / deltaTime);
        }

        // 06000f09; ARM70a070. Missing storage values are genuinely stored as
        // defaults. Only an active turn-camera flag retrieves the controls instance.
        private void UpdateStickyControls(float deltaTime)
        {
            if (!Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true)) return;
            CharacterStickyControls controls = Storage.GetValue<CharacterStickyControls>(
                ActorFSMKeys.TurnCameraStickyControls, null, true);
            if (controls != null) controls.Update(this, deltaTime);
        }
        // 06000f0a; ARM70965c. Authored turn states precede actual ground/gravity
        // tracking. Replay receiver and transform/property reads stay at their native
        // boundaries; cached character/camera-up vectors belong to the turn branch.
        private void UpdateCameraCache()
        {
            CinemachineCameraManager cameraManager = m_cinemachineCameraManagerRef.Get();
            Cinemachine.CinemachineBrain brain = cameraManager.CinemachineBrain;
            Transform cameraTransform = brain.transform;
            Vector3 characterUp = UpDirection;
            Vector3 cameraUp = cameraTransform.up;
            m_cameraPosition = m_replay.FileExtractValue(cameraTransform.position);
            m_cameraRotation = m_replay.FileExtractValue(cameraTransform.rotation);
            m_cameraForward = m_replay.FileExtractValue(cameraTransform.forward);
            m_cameraWorldUp = m_replay.FileExtractValue(brain.DefaultWorldUp);
            m_cameraWorldToProjectionMatrix = CameraUtilities.WorldToProjectionMatrix(cameraManager.MainCamera);
            m_cameraWorldToProjectionMatrix = m_replay.FileExtractValue(m_cameraWorldToProjectionMatrix);
            if (Storage.GetValue(ActorFSMKeys.TurnTrackerActive, false, true))
            {
                m_cameraForwardOnCharacterPlane = m_replay.FileExtractValue(ForwardDirection);
                return;
            }
            if (Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true))
            {
                Quaternion turnRotation = Storage.GetValue(ActorFSMKeys.TurnCameraRotation, default(Quaternion), true);
                Vector3 turnForward = turnRotation * Vector3.forward;
                Vector3 turnUp = turnRotation * Vector3.up;
                Vector3 turnRight = turnRotation * Vector3.right;
                float angle = Vector3.Angle(cameraUp, turnUp);
                Vector3 forward = Quaternion.AngleAxis(angle, turnRight) * turnForward;
                Vector3 projected = Vector3.ProjectOnPlane(forward, characterUp).normalized;
                m_cameraForwardOnCharacterPlane = m_replay.FileExtractValue(projected);
                return;
            }
            if (ActiveTracking.IsOnGround())
            {
                Quaternion rotation = Quaternion.FromToRotation(cameraTransform.up, UpDirection);
                Vector3 forward = (rotation * cameraTransform.forward).normalized;
                m_cameraForwardOnCharacterPlane = m_replay.FileExtractValue(forward);
                return;
            }
            Vector3 cameraForward = cameraTransform.forward;
            float gravityDot = Vector3.Dot(cameraForward, GravityNormalised);
            if (gravityDot > Constants.CameraForwardToGravityParallelDotThreshold)
            {
                m_cameraForwardOnCharacterPlane = m_replay.FileExtractValue(cameraTransform.up);
                return;
            }
            Vector3 scaledGravity = GravityNormalised * gravityDot;
            CharacterReplay replay = m_replay;
            Vector3 gravityProjected = (cameraForward - scaledGravity).normalized;
            m_cameraForwardOnCharacterPlane = replay.FileExtractValue(gravityProjected);
        }

        // 06000f0b; ARM70a3cc. Original post-FSM order, including the callback
        // before frame caches and the unconditional body pose writes.
        public override void DoOnFixedUpdatePostFSM(float deltaTime)
        {
            base.DoOnFixedUpdatePostFSM(deltaTime);
            ApplyModifiers(deltaTime);
            Tracker.PostUpdate(deltaTime);
            PostUpdateAbilities(deltaTime);
            UpdateImpactTracking();
            Collider.Reset();
            m_gravityProxy.up = -GravityNormalised;
            m_gravityProxy.position = WorldPosition;
            m_body.position = WorldPosition;
            m_body.rotation = WorldRotation;
            SetTimeScaledBodyVelocity(WorldVelocity);
            if (!ActiveTracking.IsOnGround()) TimeSecondsLastAirborne = GetTotalFixedTime();
            OnFixedUpdatePostFSM(this);
            m_worldPositionLastFrame = WorldPosition;
            m_worldVelocityNormalisedLastFrame = WorldVelocityNormalised;
            GravityNormalisedLastFrame = GravityNormalised;
        }
        // 06000f0c; ARM70af84. Snapshot the transform before virtual gravity access.
        private void UpdateGravityProxy()
        {
            Transform gravityProxy = m_gravityProxy;
            gravityProxy.up = -GravityNormalised;
            m_gravityProxy.position = WorldPosition;
        }
        // 06000f0d/0e; use the exact original Transform LookAt overloads. The vector
        // overload derives world-up from sight direction crossed with character right.
        public void UpdateCustomProxy(Vector3 lookFrom, Transform target)
        {
            m_customProxy.position = lookFrom;
            m_customProxy.LookAt(target);
        }
        public void UpdateCustomProxy(Vector3 lookFrom, Vector3 lookAt)
        {
            m_customProxy.position = lookFrom;
            Vector3 sightDirection = (lookAt - lookFrom).normalized;
            Vector3 worldUp = Vector3.Cross(sightDirection, RightDirection);
            m_customProxy.LookAt(lookAt, worldUp);
        }

        // 06000f0f wrapper and original d__237 native body713ae0. There are two
        // distinct suspension sites: initialization waits for a nonzero fixed delta;
        // subsequent iterations always resume after fixed physics, without rechecking it.
        private IEnumerator FixedUpdateCoroutine()
        {
            var waitForFixedUpdate = new WaitForFixedUpdate();
            while (m_fixedDeltaTime == 0f) yield return waitForFixedUpdate;
            while (true)
            {
                yield return waitForFixedUpdate;
                m_bodyWasKinematic = m_body.isKinematic;
                Collider.Finalise();
                UpdateConnectionTracking(m_fixedDeltaTime);
                if (m_fakeParent.Evaluate(out Vector3 position, out Quaternion rotation))
                {
                    SetWorldPosition(position);
                    if (!m_fakeParent.IgnoreRotation) SetWorldRotation(rotation);
                }
                Vector3 upDirection = UpDirection;
                SmoothedTransformProxy proxy = VisualProxy;
                if (Vector3.Dot(upDirection, Vector3.up) > proxy.MaxSnapCosAngle)
                {
                    Quaternion upRotation = Quaternion.FromToRotation(Vector3.up, UpDirection);
                    float forwardAngle = Vector3.SignedAngle(upRotation * Vector3.forward,
                        ForwardDirection, UpDirection);
                    VisualProxy.UpdatePositionAndRotation(m_body.position, upRotation, forwardAngle, m_fixedDeltaTime);
                }
                else
                    proxy.UpdatePositionAndRotation(m_body.position, m_body.rotation, m_fixedDeltaTime);
                m_replay.OutputPostFSM(m_body);
            }
        }

        // 06000f10; ARM70b1dc. Brain movement is cached before the late ability pass.
        protected override void DoOnUpdate(float deltaTime)
        {
            m_brain.CacheMovement();
            LateUpdateAbilities(deltaTime);
        }

        // 06000f11; ARM70b360. Preserve the short-circuit dependency/query order;
        // only swap-start and restart suppress this pass after the base gates.
        protected override bool ShouldUpdate() => base.ShouldUpdate() && m_brain != null &&
            m_trackManagerRef.IsValid() && !m_swapStartIsInProgress && !m_restartIsInProgress;
        // 06000f12; ARM70b400. An exactly-zero cooldown is required, followed by
        // the original modifier, two moving-contact gates, and debug gate.
        public bool CanSwitch() => m_switchCooldownTimer == 0f &&
            GetModifierValue<bool>((int)GameplayModifierType.SwitchAvailable) &&
            !Collider.ConnectionTracker.HasMovement && !Collider.ImpactTracker.HasMovement && !DebugIsEnabled();

        // 06000f13/14; ARM70b4b4/70b6b8. A linked collider can opt out of movement
        // tracking. The two authored deceleration keys are updated jump before air.
        public void SetConnectionTrackingValues()
        {
            bool moving = Collider.HasLinkedCollisionData && Collider.LinkedCollisionData.IgnoreMovement
                ? false : Collider.ConnectionTracker.Velocity.sqrMagnitude > 0.0001f;
            Storage.SetValue(ActorFSMKeys.JumpDecelerationDisabled, moving);
            Storage.SetValue(ActorFSMKeys.AirDecelerationDisabled, moving);
        }
        public void UpdateConnectionTrackingValues()
        {
            if (!Collider.HasLinkedCollisionData || Collider.LinkedCollisionData.IgnoreMovement) return;
            Storage.SetValue(ActorFSMKeys.JumpDecelerationDisabled, true);
            Storage.SetValue(ActorFSMKeys.AirDecelerationDisabled, true);
        }

        // 06000f15; ARM70b880. Retain the contact-tracker instance and previous
        // pose/velocity across its update. Newly acquired or lost contacts use the
        // original body-alignment path; continuous contacts transfer their delta.
        private void UpdateConnectionTracking(float deltaTime)
        {
            CharacterCollisionTracker connection = Collider.ConnectionTracker;
            bool wasActive = connection.Active;
            Vector3 previousVelocity = connection.Velocity;
            Quaternion previousRotation = connection.Rotation;
            Vector3 velocity = GetTimeScaledBodyVelocity();
            Collider.UpdateCollisionTracking(deltaTime);
            if (!wasActive || !connection.Active)
            {
                connection.AlignBodyToMovement(m_body);
                return;
            }
            Vector3 transferredVelocity = connection.Velocity;
            if (!m_body.isKinematic)
            {
                bool wasSleeping = m_body.IsSleeping();
                Rigidbody body = m_body;
                float ratio = GetPhysicsTimeDilationRatio();
                body.velocity = (velocity + (transferredVelocity - previousVelocity)) / ratio;
                if (wasSleeping) m_body.Sleep();
            }
            Rigidbody rotationBody = m_body;
            Quaternion currentRotation = connection.Rotation;
            Quaternion inversePrevious = Quaternion.Inverse(previousRotation);
            Quaternion rotationDelta = currentRotation * inversePrevious;
            rotationBody.rotation = rotationDelta * m_body.rotation;
        }

        // 06000f16; ARM70ad90. Transfer velocity uses direct magnitude division,
        // without Unity's normalized small-vector fallback. Ordered comparisons
        // deliberately allow unordered/NaN values to follow the native apply path.
        private void UpdateImpactTracking()
        {
            CharacterCollisionTracker impact = Collider.ImpactTracker;
            if (impact.Active)
            {
                float transferSpeed = impact.SetTransferVelocity();
                if (transferSpeed < 0.0001f) return;
                Vector3 velocity = WorldVelocity;
                float magnitude = impact.Velocity.magnitude;
                Vector3 transferVelocity = impact.Velocity;
                Vector3 direction = transferVelocity / magnitude;
                float projection = Vector3.Dot(velocity, direction);
                if (projection > magnitude) return;
                SetWorldVelocity(velocity + (transferVelocity - direction * projection));
            }
            else if (impact.HasCollision && BoostStamina.Active)
            {
                float approach = Vector3.Dot(-m_worldVelocityNormalisedLastFrame, impact.Normal);
                if (approach > BoostStamina.ImpactDeactivationCosAngle)
                    BoostStamina.ActivateCooldownFromImpact();
            }
        }

        // Original 0x06000f17. Invoke the real per-ability gizmo hook in dictionary
        // enumeration order; its Boolean return is deliberately ignored.
        private void OnDrawGizmosSelected()
        {
            foreach (var pair in m_abilities) pair.Value.OnDrawGizmosSelected();
        }

        // 06000f18 / ARM706548. Recreate the authored modifier store without
        // clearing the separate cancellation dictionary. Keep the original typed
        // retrieval choices, including the bool HoverData and Vector3 free-look
        // registrations; correcting those would alter original cache behavior.
        private void InitialiseModifiers()
        {
            m_modifiers = new StackableData();
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.ControlsEnabled, StackableData.RetrievalOperation.Latest, false);
            m_modifiers.SetBaseValue((int)GameplayModifierType.ControlsEnabled, true);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.FormLocked, StackableData.RetrievalOperation.Latest, false);
            m_modifiers.SetBaseValue((int)GameplayModifierType.FormLocked, false);
            m_modifiers.SetRetrievalOperation<Vector3>((int)DirectionModifierType.Override, StackableData.RetrievalOperation.Latest, default(Vector3));
            m_modifiers.SetBaseValue((int)DirectionModifierType.Override, Vector3.zero);
            m_modifiers.SetRetrievalOperation<float>((int)DirectionModifierType.Speed, StackableData.RetrievalOperation.Latest, 0f);
            m_modifiers.SetBaseValue((int)DirectionModifierType.Speed, 0f);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.ApplyGravity, StackableData.RetrievalOperation.LogicalAnd, false);
            m_modifiers.SetBaseValue((int)GameplayModifierType.ApplyGravity, true);
            m_modifiers.SetRetrievalOperation<float>((int)GameplayModifierType.GravityMultiplier, StackableData.RetrievalOperation.Multiply, 0f);
            m_modifiers.SetBaseValue((int)GameplayModifierType.GravityMultiplier, 1f);
            m_modifiers.SetRetrievalOperation<Vector3>((int)DirectionModifierType.Constant, StackableData.RetrievalOperation.Addition, default(Vector3));
            m_modifiers.SetBaseValue((int)DirectionModifierType.Constant, Vector3.zero);
            m_modifiers.SetRetrievalOperation<Vector3>((int)DirectionModifierType.Acceleration, StackableData.RetrievalOperation.Addition, default(Vector3));
            m_modifiers.SetBaseValue((int)DirectionModifierType.Acceleration, Vector3.zero);
            m_modifiers.SetRetrievalOperation<Vector3>((int)GameplayModifierType.FreeLookHeadingOverride, StackableData.RetrievalOperation.Latest, default(Vector3));
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.HoverData, StackableData.RetrievalOperation.Latest, false);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.AutoRail, StackableData.RetrievalOperation.LogicalOr, false);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.GripReset, StackableData.RetrievalOperation.LogicalOr, false);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.Invulnerable, StackableData.RetrievalOperation.LogicalOr, false);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.StickyControlsAvailable, StackableData.RetrievalOperation.Latest, false);
            m_modifiers.SetBaseValue((int)GameplayModifierType.StickyControlsAvailable, true);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.TargetingAvailable, StackableData.RetrievalOperation.Latest, false);
            m_modifiers.SetBaseValue((int)GameplayModifierType.TargetingAvailable, true);
            m_modifiers.SetRetrievalOperation<bool>((int)GameplayModifierType.SwitchAvailable, StackableData.RetrievalOperation.LogicalOr, false);
        }

        // 06000f19 / ARM70a5ac. Original per-step control, gravity and influenced
        // velocity processing. Capture values/receivers in native order rather
        // than folding side-effectful reads into the assignment expression.
        private void ApplyModifiers(float deltaTime)
        {
            float timestamp = GetTotalFixedTime();
            bool controlsEnabled = AreControlsEnabled();
            m_brain.SetEnabled(controlsEnabled, timestamp);
            if (TryGetDirectionOverride(out Vector3 directionOverride))
                SetWorldVelocity(directionOverride);
            TryGetSpeedOverride(out float speedOverride, out bool speedCanBeExceeded);
            if (Mathf.Abs(speedOverride) > 0f && (!speedCanBeExceeded || WorldVelocityMagnitude < speedOverride))
                SetWorldVelocity(WorldVelocityNormalised * speedOverride);
            bool resetGrip = GetModifierValue<bool>((int)GameplayModifierType.GripReset);
            if (resetGrip) ResetGrip();
            bool applyGravity = GetModifierValue<bool>((int)GameplayModifierType.ApplyGravity);
            m_customGravity.CanApplyForce = applyGravity;
            float gravityMultiplier = GetModifierValue<float>((int)GameplayModifierType.GravityMultiplier);
            Rigidbody body = m_body;
            body.mass = gravityMultiplier * Definition.Traits.Mass;
            Vector3 acceleration = m_modifiers.Get<Vector3>((int)DirectionModifierType.Acceleration, true, default(Vector3));
            // Native b.le/jbe skips nonpositive AND unordered values.
            if (acceleration.sqrMagnitude > 0f)
            {
                Vector3 influencedVelocity = WorldVelocity;
                influencedVelocity += acceleration * deltaTime;
                ClampInfluencedVelocityInDirection(ref influencedVelocity);
                SetWorldVelocity(influencedVelocity);
            }
            Vector3 constant = m_modifiers.Get<Vector3>((int)DirectionModifierType.Constant, true, default(Vector3));
            if (constant.sqrMagnitude > 0f)
            {
                Vector3 influencedVelocity = WorldVelocity;
                influencedVelocity += constant;
                ClampInfluencedVelocityInDirection(ref influencedVelocity);
                AppliedConstantDirectionModifier = influencedVelocity - WorldVelocity;
                SetWorldVelocity(influencedVelocity);
            }
            else AppliedConstantDirectionModifier = Vector3.zero;
            CharacterCollisionTracker connection = Collider.ConnectionTracker;
            if (connection.Active)
            {
                Vector3 platformVelocity = connection.Velocity;
                SetWorldVelocity(platformVelocity + WorldVelocity);
            }
            if (m_switchCooldownTimer > 0f)
                m_switchCooldownTimer = Mathf.Max(m_switchCooldownTimer - deltaTime, 0f);
        }

        // 06000f1a / ARM70c0f8. Zero limits leave that axis unbounded; negative
        // limits and unordered values use the original clamp predicates unchanged.
        private void ClampInfluencedVelocityInDirection(ref Vector3 influencedVelocity)
        {
            Vector3 maximum = m_modifiers.Get<Vector3>((int)GameplayModifierType.MaxVelocityFromInfluence, true, default(Vector3));
            if (maximum.sqrMagnitude == 0f) return;
            ClampIfNeeded(ref influencedVelocity.x, maximum.x);
            ClampIfNeeded(ref influencedVelocity.y, maximum.y);
            ClampIfNeeded(ref influencedVelocity.z, maximum.z);

            void ClampIfNeeded(ref float velocity, float max)
            {
                if (max != 0f) velocity = Mathf.Clamp(velocity, -max, max);
            }
        }
        // 06000f1b; ARM70c244. Zero does nothing; negative and NaN values retain
        // original timestamp arithmetic rather than being clamped to a duration.
        public void SetAirControlsLockTime(float lockTime)
        {
            if (lockTime == 0f) return;
            float timestamp = GetTotalFixedTime();
            Storage.SetValue(ActorFSMKeys.AirControlsLockTimeEnd, timestamp + lockTime);
        }
        // 06000f1c; ARM70be3c. Each write reloads storage and can propagate failure.
        public void ResetGrip()
        {
            Storage.SetValue(ActorFSMKeys.GroundGripTime, 0f);
            Storage.SetValue(ActorFSMKeys.GroundGripValue, 1f);
            Storage.SetValue(ActorFSMKeys.GroundGripRestoreTimer, 0f);
            Storage.SetValue(ActorFSMKeys.GroundBoostRestoreTimer, 0f);
        }
        // 06000f1d; ARM70c378. Read the original direction override stack directly.
        public bool IsDirectionOverrideActive() =>
            m_modifiers.Get<Vector3>((int)DirectionModifierType.Override, true, default(Vector3)).sqrMagnitude > 0f;
        // 06000f1e..20; actual modifier and custom-gravity integration boundaries.
        public bool IsTargetingAvailable() => GetModifierValue<bool>((int)GameplayModifierType.TargetingAvailable);
        public Vector3 GetGravityAt(Vector3 position) => m_customGravity.GetGravity(position);
        public float GetGravityMaxDistance() => m_customGravity.GetMaxDistance();
        // 06000f21; ARM70c47c. Chaos-air ability has priority; only a failed trigger
        // dispatch attempts the ordinary airborne burst ability.
        public void TriggerBurstAttackAir(bool trigger)
        {
            if (TryTriggerAbility<CharacterAbility_BurstAttackChaosAir>(ActorAbilityType.Character_BurstAttackChaosAir, trigger)) return;
            TryTriggerAbility<CharacterAbility_BurstAttackAir>(ActorAbilityType.Character_BurstAttackAir, trigger);
        }
        // 06000f22; original dictionary traversal/disposal, without a value snapshot.
        public void OnEnableUI()
        {
            foreach (var pair in m_abilities) pair.Value.OnEnableUI();
        }

        // 06000f23..26; original dictionary enumeration, not filtered or copied.
        // A callback may mutate the dictionary and invalidate enumeration; exceptions
        // propagate after disposal. Closing clears the dictionary only after traversal.
        private void UpdateAbilities(float deltaTime)
        {
            foreach (var pair in m_abilities) pair.Value.Update(m_brain, deltaTime);
        }
        private void PostUpdateAbilities(float deltaTime)
        {
            foreach (var pair in m_abilities) pair.Value.PostUpdate(deltaTime);
        }
        private void LateUpdateAbilities(float deltaTime)
        {
            foreach (var pair in m_abilities) pair.Value.LateUpdate(deltaTime);
        }
        private void CloseAbilities()
        {
            foreach (var pair in m_abilities) pair.Value.Close();
            m_abilities.Clear();
        }

        // 06000f27; ARM7061e0. Register the three original tracking implementations
        // in free-collider, collider-tracker, then authored surface-tracker order.
        private void InitialiseTracking()
        {
            AddTracker<CharacterTrackingColliderFree>();
            AddTracker<CharacterTrackingColliderTracker>();
            AddTracker<CharacterTrackingSurfaceTracker>();
        }
        // 06000f28; ARM709254. Clearing does not call OnLeave or clear ActiveTracking.
        private void CloseTracking() => m_trackingTypes.Clear();
        // 06000f29; ARM70627c. Both raycasts run, in physics then surface order.
        // Only a surface hit selects a tracker mode; a physics-only hit remains free.
        public void DetectTracking()
        {
            if (m_trackManagerRef.GetSafe() == null) return;
            float distance = Constants.DetectTrackingRaycastDistance;
            Vector3 centre = Bounds.center;
            Ray ray = new Ray(centre, GravityNormalised);
            bool colliderHit = Physics.Raycast(ray, distance, TrackMask);
            bool surfaceHit = SurfacePhysics.Raycast(ray, distance, Tracker.Surfaces,
                out RaycastHit hit, out ISurface surface, null, true);
            SetTrackingType(surfaceHit ? (colliderHit ? CharacterTrackingType.ColliderTracker : CharacterTrackingType.SurfaceTracker)
                : CharacterTrackingType.ColliderFree);
        }
        // 06000f2a; ARM8c6f8c. The original reflection constructor receives this
        // Character. A failed 'as T' is ignored; duplicate tracking keys throw.
        private void AddTracker<T>() where T : CharacterTracking
        {
            T tracking = Activator.CreateInstance(typeof(T), new object[] { this }) as T;
            if (tracking != null) m_trackingTypes.Add(tracking.Type, tracking);
        }

        // 06000f2b; ARM70c660. Re-entering an unchanged tracking type still runs
        // leave/enter; only the tracker notification requires a type change. Failed
        // dictionary lookup does not leave, replace or notify the current tracker.
        public void SetTrackingType(CharacterTrackingType toTrackingType)
        {
            CharacterTrackingType fromTrackingType = ActiveTracking != null ? ActiveTracking.Type : toTrackingType;
            if (!m_trackingTypes.TryGetValue(toTrackingType, out CharacterTracking tracking)) return;
            ActiveTracking?.OnLeave();
            ActiveTracking = tracking;
            ActiveTracking.OnEnter();
            if (fromTrackingType != toTrackingType)
                Tracker.OnTrackingTypeChanged(fromTrackingType, toTrackingType);
        }
        // 06000f2c; original character tracker is notified before collision tracking.
        public void OnMoveAwayFromGround()
        {
            Tracker.OnMoveAway();
            Collider.OnMoveAway();
        }

        // 06000f2d; ARM7094e0. Replay supplies the movement output even when it does
        // not override actions. Raw movement remains visible while the controller is off.
        private void ProcessBrain()
        {
            float timestamp = GetTotalFixedTime();
            m_brain.Update(timestamp);
            if (m_replay.FileExtractBrain(m_brain, out Vector2 movement, out CharacterActionFlags actions))
                m_brain.ApplyActions(actions, timestamp);
            ControllerEnabled = m_brain.Enabled;
            RawControllerMovement = movement;
            RawControllerMovementMagnitude = RawControllerMovement.magnitude;
            if (ControllerEnabled)
            {
                ControllerMovement = RawControllerMovement;
                ControllerMovementMagnitude = RawControllerMovementMagnitude;
            }
            else
            {
                ControllerMovement = Vector2.zero;
                ControllerMovementMagnitude = 0f;
            }
        }
        // 06000f2e; ARM70c77c. Preserve raw-input magnitude even after remapping its
        // heading through the authored curve. Missing FSM turn values are stored.
        public void ApplyInputModifier(AnimationCurve angleLookup)
        {
            if (angleLookup == null || angleLookup.length < 1) return;
            float magnitude = RawControllerMovementMagnitude;
            if (magnitude == 0f || !ControllerEnabled) return;
            Vector2 rawDirection = RawControllerMovement / magnitude;
            float angle = Mathf.Atan2(rawDirection.x, rawDirection.y) * Mathf.Rad2Deg;
            if (Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true))
                angle -= Storage.GetValue(ActorFSMKeys.TurnCameraInputRelative, 0f, true);
            float radians = angleLookup.Evaluate(angle) * Mathf.Deg2Rad;
            ControllerMovement = new Vector2(Mathf.Sin(radians) * magnitude, Mathf.Cos(radians) * magnitude);
            ControllerMovementMagnitude = magnitude;
        }
        // 06000f2f; ARM70c9f0. An active authored turn-camera frame substitutes its
        // stored quaternion (default all-zero), then local y+z is clamped, not normalized.
        public float CalculateControllerMovementToCamera()
        {
            Quaternion cameraRotation = Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true)
                ? Storage.GetValue(ActorFSMKeys.TurnCameraRotation, default(Quaternion), true)
                : m_cameraRotation;
            Vector3 localForward = Quaternion.Inverse(cameraRotation) * ForwardDirection;
            return localForward.x * ControllerMovement.x +
                Mathf.Clamp(localForward.y + localForward.z, -1f, 1f) * ControllerMovement.y;
        }
        // 06000f30; Active is the original wildcard, not an additional tracking mode.
        public bool IsOnGround(CharacterTrackingType trackingType) =>
            (trackingType == CharacterTrackingType.Active || ActiveTracking.Type == trackingType) && ActiveTracking.IsOnGround();
        // 06000f31; ARM70cc20. Capture the tracker receiver before cached position.
        public float DistanceOnTrackerNormal() => Tracker.DistanceOnNormal(WorldPosition);
        // 06000f32; ARM70cc38. Snapshot the character rotation before tracker access.
        public void UpdateTrackerOrientation()
        {
            Quaternion rotation = WorldRotation;
            if (CharacterMovementUtilities.AreRotationsReversed(rotation, Tracker.TrackerRotation)) Tracker.TurnAround();
        }

        // 06000f33; ARM70ccc0. Preserve local velocity before reversing the pose.
        // Native SIMD multiplies WorldRotation * RotationReverseLocal; the reversed
        // multiplication would rotate about a different axis for a tilted character.
        public void TurnAround(float speedMin = 0f)
        {
            Tracker.TurnAround();
            Vector3 localVelocity = LocalVelocity;
            SetWorldRotation(WorldRotation * RotationReverseLocal);
            float speed = WorldVelocityMagnitude;
            if (speed > 0f && speed < speedMin) localVelocity *= speedMin / speed;
            SetLocalVelocity(localVelocity);
        }
        // 06000f34/35; keep the tracker receiver before the virtual velocity query.
        public void DetectTurnAroundFromVelocity() => Tracker.DetectSurfaceReversedFromVelocity(WorldVelocity);
        public void DetectTurnAroundFromInput(float inputToTrackerThreshold) =>
            Tracker.DetectSurfaceReversedFromInput(inputToTrackerThreshold);

        // 06000f36; ARM70ce84. The authored tracker flag and turn-around default
        // are stored when absent. Capture position/rotation before the virtual
        // velocity read; failures clear only the original tracker flag.
        public bool UpdateJumpOnRailTracker(float deltaTime)
        {
            if (!Storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true)) return false;
            Vector3 position = WorldPosition;
            Quaternion rotation = WorldRotation;
            Vector3 velocity = WorldVelocity;
            float step = WorldVelocityMagnitude * deltaTime;
            bool canTurnAround = Storage.GetValue(ActorFSMKeys.JumpOnRailCanTurnAround, false, true);
            if (Tracker.UpdateJumpTracker(step, ref position, ref rotation, ref velocity, canTurnAround))
            {
                UpdateJumpOnRailMagnetism(ref position, ref rotation, ref velocity, deltaTime);
                SetWorldPosition(position);
                SetWorldRotation(rotation);
                SetWorldVelocity(velocity);
                return true;
            }
            Storage.SetValue(ActorFSMKeys.JumpOnRailTracker, false);
            return false;
        }

        // 06000f37; ARM70d1e8. Native unordered comparisons continue the force
        // path, while the position clamp remains an ordered strict-greater test.
        // The curve callback precedes the live cached RigidbodyGravity receiver.
        private void UpdateJumpOnRailMagnetism(ref Vector3 position, ref Quaternion rotation,
            ref Vector3 velocity, float deltaTime)
        {
            AnimationCurve magnetism = Storage.GetValue<AnimationCurve>(ActorFSMKeys.JumpOnRailMagnetism, null, true);
            if (magnetism == null) return;
            float distance = Tracker.DistanceOnNormal(position);
            float distanceMax = Storage.GetValue(ActorFSMKeys.JumpOnRailDistanceMax, 0f, true);
            if (distance <= 0f && distance > -distanceMax) return;
            Vector3 characterUp = rotation * Vector3.up;
            Vector3 trackerUp = Tracker.TrackerRotation * Vector3.up;
            float upDot = Vector3.Dot(characterUp, trackerUp);
            float angleCosineMax = Storage.GetValue(ActorFSMKeys.JumpOnRailAngleCosineMax, 0f, true);
            if (upDot < angleCosineMax) return;
            if (Mathf.Abs(distance) > distanceMax)
                position -= trackerUp * (distance - distanceMax);
            float multiplier = magnetism.Evaluate(distance);
            RigidbodyGravity gravity = m_customGravity;
            float force = -(multiplier * gravity.GravityMagnitude);
            velocity += (trackerUp * force - gravity.Gravity) * deltaTime;
        }
        // 06000f38; ARM70d65c. Controls belong to the authored sticky-controls asset.
        public void SetTurnCameraActive(Quaternion cameraRotation) =>
            m_stickyControlsDefinition.StickyControls.SetTurnCameraActive(this, cameraRotation);
        // 06000f39; ARM70d670. The minimum is already squared by the definition.
        public void OrientateToPlane(Vector3 planeNormal) =>
            base.OrientateToPlane(planeNormal, LocalVelocity.xz().sqrMagnitude > Constants.MovementInputSpeedMinSqr);
        // 06000f3a; ARM70d6e8. Applied input, rather than raw disabled input, chooses
        // the heading around the character's current up axis from cached camera forward.
        public void OrientateToInputForward()
        {
            if (ControllerMovementMagnitude == 0f) return;
            float inputAngle = Vector2.SignedAngle(ControllerMovement, Vector2.up);
            Vector3 lookDirection = Quaternion.AngleAxis(inputAngle, UpDirection) * CameraForwardOnCharacterPlane;
            OrientateToPlaneWithLookDirection(UpDirection, lookDirection);
        }

        // Original 0x06000f3b. The shipped diagnostic prints a zero velocity,
        // even though other Character diagnostics can access actual velocity.
        public void DebugGetTransformInfo(StringBuilder stringInfoBuilder)
        {
            Vector3 velocity = Vector3.zero;
            float velocityMagnitude = velocity.magnitude;
            stringInfoBuilder.AppendLine(string.Format("Position = {0:F2}", WorldPosition));
            stringInfoBuilder.AppendLine(string.Format("Velocity = {0:F2}, {1:F2}", velocityMagnitude, velocity));
            float mass = m_body.mass;
            float traitMass = Definition.Traits.Mass;
            RigidbodyGravity gravity = m_customGravity;
            string disabled = gravity.CanApplyForce ? string.Empty : " (disabled)";
            Vector3 gravityVector = gravity.Gravity;
            float massRatio = mass / traitMass;
            stringInfoBuilder.AppendLine(string.Format("Gravity = {0:F2} x{1:F2}{2}", gravityVector, massRatio, disabled));
            // Capture the actual receiver before position/mass getters; those
            // getters may invoke engine work while a callback changes fields.
            RigidbodyGravity debugGravity = m_customGravity;
            Vector3 position = WorldPosition;
            float debugMass = m_body.mass;
            debugGravity.GetDebugInfo(position, debugMass, stringInfoBuilder);
            HomingPool.GetFSMTargetDebugInfo(stringInfoBuilder);
            Collider.GetDebugInfo(stringInfoBuilder);
            m_stickyControlsDefinition.StickyControls.GetDebugInfo(this, stringInfoBuilder);
        }

        // Original 0x06000f3c. This is a real impact-history formatter tailcall,
        // not an empty release diagnostic.
        public override void DebugGetCollisionInfo(StringBuilder stringInfoBuilder)
        {
            Collider.GetDebugInfoImpactHistory(stringInfoBuilder);
        }

        // Original 0x06000f3d. Preserve embedded newlines as well as AppendLine's
        // own newline, and the extra four blank lines before/after each record.
        public void DebugGetAbilitiesInfo(StringBuilder nameInfo, StringBuilder enabledInfo, StringBuilder updateInfo, StringBuilder extraInfo)
        {
            nameInfo.AppendLine("Name\n");
            enabledInfo.AppendLine("Enabled\n");
            updateInfo.AppendLine("Update\n");
            extraInfo.AppendLine("\n");
            DebugAddAbilitiesLine(nameInfo, enabledInfo, updateInfo, extraInfo);
            foreach (var pair in m_abilities)
            {
                pair.Value.GetDebugInfo(nameInfo, enabledInfo, updateInfo, extraInfo);
                DebugAddAbilitiesLine(nameInfo, enabledInfo, updateInfo, extraInfo);
            }
        }

        // Original 0x06000f3e. No nullable-builder shortcuts were present.
        private void DebugAddAbilitiesLine(StringBuilder nameInfo, StringBuilder enabledInfo, StringBuilder updateInfo, StringBuilder extraInfo)
        {
            nameInfo.AppendLine();
            enabledInfo.AppendLine();
            updateInfo.AppendLine();
            extraInfo.AppendLine();
        }

        // Original 0x06000f3f. Distance is evaluated even when the real optional
        // magnetism curve is null; the formatted arguments are multiplier first.
        public void DebugGetJumpOnRailInfo(StringBuilder stringInfo)
        {
            if (!Storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true)) return;
            AnimationCurve magnetism = Storage.GetValue<AnimationCurve>(ActorFSMKeys.JumpOnRailMagnetism, null, true);
            float distance = Tracker.DistanceOnNormal(WorldPosition);
            float multiplier = magnetism == null ? 0f : magnetism.Evaluate(distance);
            stringInfo.AppendLine(string.Format("Rail magnetism = {0:F2}, Distance = {1:F2}", multiplier, distance));
        }

        // 06000f40; ARM70e0ec. Ordered speed comparison against the authored
        // input-speed minimum. NaN is not a collision-modification permission.
        private bool BodyCollisionCanModify() => WorldVelocityMagnitude > Constants.MovementInputSpeedMin;

        // 06000f41/42; plain CharacterCollider reference guards, before forwarding.
        private void OnCollisionEnter(Collision collision)
        {
            if (Collider != null) Collider.OnCollisionEnter(collision);
        }
        private void OnCollisionStay(Collision collision)
        {
            if (Collider != null) Collider.OnCollisionStay(collision);
        }

        // 06000f43/44; disabled incoming colliders suppress enter only. Exit does
        // not inspect enabled, and the original event can be unassigned.
        private void OnTriggerEnter(Collider otherCollider)
        {
            if (!otherCollider.enabled) return;
            OnCharacterTriggerEnter?.Invoke(otherCollider);
        }
        private void OnTriggerExit(Collider otherCollider) => OnCharacterTriggerExit?.Invoke(otherCollider);

        // 06000f45..47; update the link before callbacks. Collision-null paths
        // still change linkage, while exit clears only the exact Unity-object match.
        public void OnCollisionDataEnter(CharacterCollisionData collisionData, Collision collision = null)
        {
            Collider.SetLinkedCollisionData(collisionData);
            if (collision != null) OnCollisionDataEnterEvent?.Invoke(collisionData, collision);
        }
        public void OnCollisionDataStay(CharacterCollisionData collisionData, Collision collision = null)
        {
            Collider.SetLinkedCollisionData(collisionData);
            if (collision != null) OnCollisionDataStayEvent?.Invoke(collisionData, collision);
        }
        public void OnCollisionDataExit(CharacterCollisionData collisionData, Collision collision = null)
        {
            if (Collider.LinkedCollisionData == collisionData) Collider.SetLinkedCollisionData(null);
            if (collision != null) OnCollisionDataExitEvent?.Invoke(collisionData, collision);
        }

        // 06000f48 / ARM70e368. Ignore the metadata lookup return and use its
        // boolean out value. An enabled entry removes an existing disabling
        // override; a missing/false entry creates one only when the handle is null.
        public void AddModifierEnabledOverrideFromMetadata(GameplayModifierType modifierType, MetadataGroup metadata,
            MetadataKeyType key, ref StackableDataHandle handle)
        {
            metadata.TryGetValue(key, out bool enabled);
            StackableDataHandle existingHandle = handle;
            if (enabled)
            {
                if (existingHandle != null)
                {
                    RemoveModifierOverrides(existingHandle);
                    handle = null;
                }
            }
            else if (existingHandle == null) handle = AddModifierOverride((int)modifierType, false);
        }

        // 06000f49 / ARM70e4f8. All three storage reads execute when initialized;
        // bitwise OR preserves their original order and default insertion effects.
        public bool AreModifiersBlocked()
        {
            if (!Initialised) return true;
            bool dying = Storage.GetValue(ActorFSMKeys.TriggerDyingState, false, true);
            bool outOfBounds = Storage.GetValue(ActorFSMKeys.IsOutOfBounds, false, true);
            bool hardFailure = Storage.GetValue(ActorFSMKeys.InHardFailureState, false, true);
            return dying | outOfBounds | hardFailure;
        }

        // 06000f4a / ARM8c6c64. Registration follows actual override application;
        // Dictionary.Add retains duplicate-key failure and even a null callback.
        public StackableDataHandle AddModifierOverride<T>(int modifierType, T value, Action onCancel)
        {
            StackableDataHandle handle = AddModifierOverride(modifierType, value);
            m_modifierOverridesOnCancel.Add(handle, onCancel);
            return handle;
        }
        // 06000f4b / ARM8c66a8. Constant-direction override application removes
        // the newly effective contribution from current velocity before returning.
        public StackableDataHandle AddModifierOverride<T>(int modifierType, T value)
        {
            StackableDataHandle handle = m_modifiers.AddOverride(modifierType, value);
            if (ModifierOverridesVelocity(modifierType))
            {
                Vector3 velocity = WorldVelocity;
                Vector3 contribution = m_modifiers.Get<Vector3>(modifierType, true, default(Vector3));
                SetWorldVelocity(velocity - contribution);
            }
            return handle;
        }
        // 06000f4c / ARM8c7144. Recover the old contribution before replacing it,
        // then remove the new contribution. Other types still apply to the stack.
        public void AdjustModifierOverrides<T>(StackableDataHandle stackableDataHandle, int modifierType, T value)
        {
            bool overridesVelocity = ModifierOverridesVelocity(modifierType);
            Vector3 velocity = overridesVelocity
                ? WorldVelocity + m_modifiers.Get<Vector3>(modifierType, true, default(Vector3))
                : Vector3.zero;
            m_modifiers.AddOverride(stackableDataHandle, modifierType, value);
            if (overridesVelocity)
                SetWorldVelocity(velocity - m_modifiers.Get<Vector3>(modifierType, true, default(Vector3)));
        }
        // 06000f4d / ARM70e48c. Removing registration does not invoke its callback.
        public void RemoveModifierOverrides(StackableDataHandle stackableDataHandle)
        {
            m_modifierOverridesOnCancel.Remove(stackableDataHandle);
            m_modifiers.RemoveOverrides(stackableDataHandle);
        }
        // 06000f4e / ARM70e768. Traverse the live dictionary; callbacks precede
        // stack removal and may invalidate enumeration. Clear only after success.
        private void CancelModifierOverrides()
        {
            foreach (var pair in m_modifierOverridesOnCancel)
            {
                pair.Value();
                m_modifiers.RemoveOverrides(pair.Key);
            }
            m_modifierOverridesOnCancel.Clear();
        }
        // 06000f4f/50: retain actual cache/default and handle lookup semantics.
        public T GetModifierValue<T>(int modifierType) => m_modifiers.Get<T>(modifierType, true, default(T));
        public T GetModifierOverride<T>(StackableDataHandle handle, int modifierType) => m_modifiers.GetOverride<T>(handle, modifierType);
        // 06000f51: only Constant, not every direction modifier, removes velocity.
        private bool ModifierOverridesVelocity(int modifierType) => modifierType == (int)DirectionModifierType.Constant;

        // 06000f52 / ARM70e904. Remove the old handle/storage entry first, even
        // when enabling again, then install a fresh true override only if requested.
        public void SetModifierFormLocked(bool formLocked)
        {
            if (Storage.TryGetValue(ActorFSMKeys.FormLockedOverrideHandle, out StackableDataHandle handle))
            {
                RemoveModifierOverrides(handle);
                Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.FormLockedOverrideHandle);
            }
            if (formLocked)
            {
                handle = AddModifierOverride((int)GameplayModifierType.FormLocked, true);
                Storage.SetValue(ActorFSMKeys.FormLockedOverrideHandle, handle);
            }
        }
        // 06000f53 / ARM70eb9c. Clear-lock bypasses the current lock test; setting
        // the new lock happens before the original Actor form switch.
        public void SwitchToForm(ActorFormType formType, bool setLock, bool clearLock)
        {
            if (clearLock) SetModifierFormLocked(false);
            else if (IsFormLocked()) return;
            if (setLock) SetModifierFormLocked(true);
            base.SwitchToForm(formType);
        }

        // 06000f54; ARM70ec20. The trigger capsule follows the base collision
        // size; the ordered branch preserves the original NaN comparison behavior.
        public override void SetColliderCollisionSize(float radius, float height)
        {
            base.SetColliderCollisionSize(radius, height);
            m_capsuleColliderCollision.radius = radius;
            m_capsuleColliderCollision.height = height;
            float centreHeight = 2f * radius > height ? radius : height * 0.5f;
            m_capsuleColliderCollision.center = new Vector3(0f, centreHeight, 0f);
        }
        // 06000f55/56; trigger is enabled before its radius is changed.
        public void EnableColliderTrigger(float radius)
        {
            m_colliderTrigger.enabled = true;
            m_colliderTrigger.radius = radius;
        }
        public void DisableColliderTrigger() => m_colliderTrigger.enabled = false;

        // 06000f57; ARM70ecdc. Original fixed serialized player movement category.
        public override TimeCategory GetTimeCategory() => CharacterTimeCategory;

        // 06000f58; ARM70ece8. Native deltaTime is unused. Kinematic bodies use the
        // genuine base velocity cache, then real category dilation is applied before
        // compensating the two frame-dilation snapshots. Modifier pre-step always runs.
        protected override void UpdateBodyPosition(float deltaTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            position = m_body.position;
            rotation = m_body.rotation;
            Vector3 bodyVelocity = m_body.isKinematic ? base.WorldVelocity : m_body.velocity;
            float physicsRatio = GetPhysicsTimeDilationRatio();
            velocity = bodyVelocity * physicsRatio;
            float currentDilation = m_timeDilationCurrentFrame;
            if (!Mathf.Approximately(currentDilation, 0f))
            {
                float previousDilation = m_timeDilationPreviousFrame;
                if (!Mathf.Approximately(previousDilation, 0f))
                    velocity /= m_timeDilationCurrentFrame / previousDilation;
            }
            ApplyPreFixedUpdateModifiers(ref velocity);
        }

        // 06000f59/5a; ARM70ef98/70f008. During the Actor fixed-update pass,
        // body writes are deferred to the original post-FSM phase.
        public override void SetWorldPosition(Vector3 worldPosition)
        {
            base.SetWorldPosition(worldPosition);
            if (!FixedUpdateActive) m_body.position = worldPosition;
        }
        public override void SetWorldRotation(Quaternion worldRotation)
        {
            base.SetWorldRotation(worldRotation);
            if (!FixedUpdateActive) m_body.rotation = worldRotation;
        }

        // 06000f5b/5c; ARM70f080/70f088. Physics velocity is written before
        // the Actor's cached velocity; force bypasses only the fixed-pass guard.
        public override void SetWorldVelocity(Vector3 worldVelocity) => SetWorldVelocity(worldVelocity, false);
        public void SetWorldVelocity(Vector3 worldVelocity, bool forceUpdateRigidbody)
        {
            if (forceUpdateRigidbody || !FixedUpdateActive)
                SetTimeScaledBodyVelocity(worldVelocity);
            base.SetWorldVelocity(worldVelocity);
        }
        // 06000f5d; ARM70f140. The active fixed pass uses its current cached
        // velocity, including changes made by abilities before body synchronization.
        public override Vector3 WorldVelocity => FixedUpdateActive ? base.WorldVelocity : GetTimeScaledBodyVelocity();

        // 06000f5e / ARM70ee7c. Remove prior platform and constant contributions
        // before the step; when constant direction expires, orient cached velocity.
        private void ApplyPreFixedUpdateModifiers(ref Vector3 velocity)
        {
            CharacterCollisionTracker connection = Collider.ConnectionTracker;
            if (connection.Active) velocity -= connection.Velocity;
            Vector3 constant = m_modifiers.Get<Vector3>((int)DirectionModifierType.Constant, true, default(Vector3));
            if (constant.sqrMagnitude > 0f) velocity -= AppliedConstantDirectionModifier;
            else if (AppliedConstantDirectionModifier.sqrMagnitude > 0f) OrientateVelocityToForward();
        }
        // 06000f5f/60: ordered positivity; short-circuit in actual authored order.
        public bool HasDirectionModifierType(DirectionModifierType modifierType) =>
            m_modifiers.Get<Vector3>((int)modifierType, true, default(Vector3)).sqrMagnitude > 0f;
        public bool HasAnyDirectionModifier() =>
            HasDirectionModifierType(DirectionModifierType.Acceleration) ||
            HasDirectionModifierType(DirectionModifierType.Constant) ||
            HasDirectionModifierType(DirectionModifierType.Override) ||
            HasDirectionModifierType(DirectionModifierType.Speed);
        public bool HasHoverData() => m_modifiers.HasData((int)GameplayModifierType.HoverData);
        // 06000f62: presence can return true even if the retrieved object is null.
        public bool TryGetHoverData(out HoverData hoverData)
        {
            hoverData = null;
            bool hasData = m_modifiers.HasData((int)GameplayModifierType.HoverData);
            if (hasData) hoverData = m_modifiers.Get<HoverData>((int)GameplayModifierType.HoverData, true, null);
            return hasData;
        }
        // 06000f63: free-look adds a genuine Unity liveness check after retrieval.
        public bool TryGetFreeLookHeadingOverride(out CameraRecenterHeadingDefinition_FreeLook freeLookHeadingDefinition)
        {
            freeLookHeadingDefinition = null;
            if (!m_modifiers.HasData((int)GameplayModifierType.FreeLookHeadingOverride)) return false;
            freeLookHeadingDefinition = m_modifiers.Get<CameraRecenterHeadingDefinition_FreeLook>((int)GameplayModifierType.FreeLookHeadingOverride, true, null);
            return freeLookHeadingDefinition != null;
        }
        // 06000f64 / ARM70f4d0. Use the last actual camera projection snapshot.
        public bool WorldPositionIsOnScreen(Vector3 worldPosition, Vector2 borders = default(Vector2)) =>
            CameraUtilities.WorldPositionIsOnScreen(m_cameraWorldToProjectionMatrix, worldPosition, borders);

        // 06000f65 / ARM70f508. Modifier, saved accessibility mode, then the real
        // invulnerability ability are queried in that short-circuit order.
        public bool IsInvulnerable()
        {
            if (m_modifiers.Get<bool>((int)GameplayModifierType.Invulnerable, true, false)) return true;
            if (m_saveManagerRef.Get().GetSaveDataSettings().FailState == AccessibilityFailState.Invincible) return true;
            return TryGetAbility(ActorAbilityType.Character_Invulnerability, out CharacterAbility_Invulnerability ability) && ability.Enabled;
        }
        // 06000f66 / ARM70f64c. Finish only an enabled original ability.
        public bool TryExitInvulnerability()
        {
            if (!TryGetAbility(ActorAbilityType.Character_Invulnerability, out CharacterAbility_Invulnerability ability) || !ability.Enabled)
                return false;
            ability.Finish();
            return true;
        }

        // 06000f67; ARM70f6f0. Destroying form/abilities win before invulnerable
        // form/abilities. The real accessibility setting is read after IsInvulnerable.
        private DamageAction DetermineHazardDamageAction(HazardDefinition hazardDefinition)
        {
            if (hazardDefinition.CharacterDestroyHazardFormType == FormType)
                return DamageAction.ReturnDamage;
            if (hazardDefinition.CharacterInvulnerableFormType == FormType)
                return DamageAction.None;
            if (hazardDefinition.CharacterDestroyHazardAbilities != null)
            {
                foreach (CharacterAbilityTypeGroup.AbilityTypeDefinition ability in hazardDefinition.CharacterDestroyHazardAbilities.Abilities)
                    if (ActorAbilityUtilities.IsAbilityInUse(this, ability.Type, ability.GracePeriod))
                        return DamageAction.ReturnDamage;
            }
            if (hazardDefinition.InvulnerableAbilities != null)
            {
                foreach (CharacterAbilityTypeGroup.AbilityTypeDefinition ability in hazardDefinition.InvulnerableAbilities.Abilities)
                    if (ActorAbilityUtilities.IsAbilityInUse(this, ability.Type, ability.GracePeriod))
                        return DamageAction.None;
            }
            if (IsInvulnerable()) return DamageAction.None;
            AccessibilityFailState failState = m_saveManagerRef.Get().GetSaveDataSettings().FailState;
            if (hazardDefinition.InstantDeath && failState == AccessibilityFailState.Default)
                return DamageAction.HardFailure;
            CollectableChangeData collectableCost = hazardDefinition.CollectableCost;
            if (collectableCost.Type == CollectableType.Ring && failState == AccessibilityFailState.Default)
            {
                ProcessManager.GetSystem<CollectableManager>(null, true)
                    .TryGetCollectableState(CollectableType.Ring, out CollectableState state);
                // Native intentionally ignores TryGet's bool and tests the returned state.
                if (state == null || (collectableCost.HasChange() && state.Collected == 0))
                    return DamageAction.HardFailure;
            }
            bool railActive = Storage.GetValue(ActorFSMKeys.RailActive, false, true);
            // ARM unsigned LS / x86 reversed SETB both classify unordered as Stumble.
            if (!hazardDefinition.AllowKnockback || railActive || !(WorldVelocityMagnitude <= hazardDefinition.StumbleSpeedThreshold))
                return DamageAction.Stumble;
            ActorAbilityType[] abilities = hazardDefinition.DoNotKnockbackAbilities;
            if (abilities != null)
            {
                foreach (ActorAbilityType abilityType in abilities)
                    if (TryGetAbilityFromType(abilityType, out ActorAbility ability) && ability.Enabled)
                        return DamageAction.Stumble;
            }
            return DamageAction.Knockback;
        }

        // 06000f68; ARM70fc98. The caller's callback runs after shield processing,
        // before the damage dispatch. Shield consumption still invokes damage/invulnerability
        // callbacks but returns None and skips the current-character cascade/reset path.
        public DamageAction TryTakeDamage(HazardDefinition hazardDefinition, Action<DamageAction, Collider> processDamageAction)
        {
            if (AnySwapInProgress()) return DamageAction.None;
            DamageAction damageAction = DetermineHazardDamageAction(hazardDefinition);
            Vector3 initialVelocity = WorldVelocity;
            bool shieldDamage = TriggerShieldDamage(damageAction);
            if (shieldDamage && !hazardDefinition.InstantDeath) damageAction = DamageAction.None;
            processDamageAction?.Invoke(damageAction, m_colliderCollision);
            switch (damageAction)
            {
                case DamageAction.None:
                    if (!shieldDamage) return DamageAction.None;
                    break;
                case DamageAction.ReturnDamage:
                    return damageAction;
                case DamageAction.Stumble:
                    TriggerStumble(hazardDefinition);
                    break;
                case DamageAction.Knockback:
                    TriggerKnockback();
                    break;
                case DamageAction.HardFailure:
                    TriggerHardFailure(hazardDefinition.InstantDeath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            OnTakeDamage?.Invoke();
            if (TryGetAbility(ActorAbilityType.Character_Invulnerability, out CharacterAbility_Invulnerability invulnerability))
                invulnerability.TriggerForDuration(hazardDefinition.InvulnerableDurationSeconds);
            if (shieldDamage) return DamageAction.None;
            if (m_characterManager.IsCurrentCharacter(this))
            {
                Vector3 bodyVelocity = m_body.isKinematic ? base.WorldVelocity : m_body.velocity;
                Vector3 resultingVelocity = bodyVelocity * GetPhysicsTimeDilationRatio();
                TriggerCascade(damageAction, hazardDefinition.CollectableCost, initialVelocity, resultingVelocity,
                    hazardDefinition.InitialToResultingVelocityScale, hazardDefinition.ZeroToFullVelocityScale);
                if (m_retainImpulses) m_retainImpulses = false;
                else
                {
                    float endTimestamp = GetTotalFixedTime();
                    if (m_brain != null) m_brain.EndImpulses(endTimestamp);
                }
                CharacterBoostStamina boost = BoostStamina;
                float cooldown = hazardDefinition.BoostCooldownDurationSeconds;
                boost.ActivateCooldown(cooldown);
                ChaosStamina.ActivateCooldown(cooldown);
                SpinDashRoll.DisableCharge();
            }
            return damageAction;
        }

        // 06000f69; ARM710380. Mutate the local cost by reference before changing
        // collectables. Original ring origin uses a second bounds query's X extent.
        private void TriggerCascade(DamageAction damageAction, CollectableChangeData changeData,
            Vector3 initialVelocity, Vector3 resultingVelocity, float initialToResultingVelocityScale,
            float zeroToResultingVelocityScale)
        {
            CollectableManager manager = ProcessManager.GetSystem<CollectableManager>(null, true);
            if (damageAction == DamageAction.HardFailure) manager.GetCollectableTierReviveMultiplier(ref changeData);
            else manager.GetCollectableTierMaxDamage(ref changeData);
            int amount = manager.ChangeCollectableAmount(changeData);
            if (amount == 0 || changeData.Type != CollectableType.Ring) return;
            Vector3 origin = m_capsuleColliderCollision.bounds.center;
            float extentX = m_capsuleColliderCollision.bounds.extents.x;
            Vector3 velocity = Vector3.Slerp(initialVelocity, resultingVelocity, initialToResultingVelocityScale);
            velocity = Vector3.Slerp(Vector3.zero, velocity, zeroToResultingVelocityScale);
            CascadeObjectPool pool = CascadeObjectPool.GetCascadeObjectPoolByType(CascadeObjectType.Ring);
            Quaternion objectRotation = Quaternion.FromToRotation(Vector3.up, -GravityNormalised);
            IReadOnlyCollection<GravitySource> gravitySources = m_customGravity.GetSources();
            origin.y += extentX;
            pool.TriggerCascade(CascadeObjectType.Ring, Mathf.Abs(amount), origin, WorldRotation,
                velocity, objectRotation, gravitySources);
            Audio.PlayOneShot(ActorAudioTypes.Ring_Drop);
        }
        // 06000f6a/f6b: preserve actual typed ability lookups and hazard callback.
        private void TriggerKnockback()
        {
            if (TryGetAbility(ActorAbilityType.Character_Knockback, out CharacterAbility_Knockback ability)) ability.Trigger();
        }
        private void TriggerStumble(HazardDefinition hazard)
        {
            if (TryGetAbility(ActorAbilityType.Character_Stumble, out CharacterAbility_Stumble ability)) ability.TriggerFromHazard(hazard);
        }

        // 06000f6c; ARM7106dc. Both authored gates are default-storing reads.
        // A shield can suppress ring loss without suppressing the out-of-bounds state.
        public void TriggerOutOfBounds(bool instantDeath, bool triggerRingLoss)
        {
            if (Storage.GetValue(ActorFSMKeys.IsOutOfBounds, false, true)) return;
            if (Storage.GetValue(ActorFSMKeys.TriggerDyingState, false, true)) return;
            if (triggerRingLoss && !TriggerShieldDamage(DamageAction.HardFailure))
            {
                CollectableChangeData cost = new CollectableChangeData
                {
                    Type = CollectableType.Ring,
                    FractionChange = -1f
                };
                TriggerCascade(DamageAction.HardFailure, cost, Vector3.zero, Vector3.zero, 0f, 1f);
            }
            if (instantDeath) TriggerHardFailure(true);
            else Storage.SetValue(ActorFSMKeys.IsOutOfBounds, true);
            SpinDashRoll.DisableCharge();
            OnTakeDamage?.Invoke();
        }

        // 06000f6d; ARM710064. Original typed lookup and shield callback; no
        // synthetic shield implementation or Enabled shortcut is introduced.
        private bool TriggerShieldDamage(DamageAction damageAction) =>
            TryGetAbility(ActorAbilityType.Character_Shield, out CharacterAbility_Shield shield) &&
            shield.TryTakeDamage(damageAction);
        // 06000f6e / ARM7101f4. Set the dying trigger first. Instant death disables
        // the pause input; the other path invokes the genuine knockback ability.
        private void TriggerHardFailure(bool instantDeath)
        {
            Storage.SetValue(ActorFSMKeys.TriggerDyingState, true);
            if (instantDeath) EnableGameInput(false);
            else TriggerKnockback();
        }
        // 06000f6f / ARM710a80. Both reads execute; neither inserts defaults.
        public bool DyingIsInProgress()
        {
            bool trigger = Storage.GetValueOnly(ActorFSMKeys.TriggerDyingState, false);
            float elapsed = Storage.GetValueOnly(ActorFSMKeys.InDyingStateTime, 0f);
            return trigger || elapsed > 0f;
        }

        // 06000f70..74; ARM710b5c..710be8. Use the exact original action literals.
        public void EndAirActiveImpulse() => m_brain.EndAction(GameAction.CharacterAirActivate);
        public void EndAirStompAttackImpulse() => m_brain.EndAction(GameAction.CharacterAirStompAttack);
        public void EndJumpImpulses() => m_brain.EndAction(GameAction.CharacterJump);
        public void EndLightspeedDashImpulse() => m_brain.EndAction(GameAction.CharacterLightspeedDash);
        public void EndRollImpulses()
        {
            m_brain.EndAction(GameAction.CharacterRoll);
            m_brain.EndAction(GameAction.CharacterSpinDashCharge);
        }

        // 06000f75/76; ARM710bec/710690. Retaining suppresses exactly one call;
        // the normal path obtains the timestamp even when the brain reference is null.
        public void RetainCurrentImpulses() => m_retainImpulses = true;
        public void EndImpulses()
        {
            if (m_retainImpulses)
            {
                m_retainImpulses = false;
                return;
            }
            float timestamp = GetTotalFixedTime();
            if (m_brain != null) m_brain.EndImpulses(timestamp);
        }

        // 06000f77..7b; ARM710bf8..710c64. Preserve default raw threshold and
        // the receiver snapshot before the time query, which may enter the scheduler.
        public void CacheBrainMovement() => m_brain.CacheMovement();
        public bool GetBrainRawState(GameAction action) => m_brain.GetRawState(action, 0.5f);
        public void LockBrain(float lockTime)
        {
            CharacterBrain brain = m_brain;
            float timestamp = GetTotalFixedTime();
            brain.LockCache(timestamp + lockTime);
        }
        public bool GetBrainAppliedState(GameAction action) => m_brain.GetAppliedState(action);
        public float GetBrainActionTimestamp(GameAction action) => m_brain.GetActionTimestamp(action);

        // 06000f7c; ARM7069e8. Each genuine Unity-object guard is evaluated in
        // order; enabling gravity follows the body and collision-collider writes.
        public void AllowCollisions(bool allow)
        {
            if (m_body != null) m_body.isKinematic = !allow;
            if (m_colliderCollision != null) m_colliderCollision.enabled = allow;
            if (m_customGravity != null) m_customGravity.enabled = allow;
        }
        // 06000f7d plus original d__348 MoveNext7139a0. This coroutine disables
        // collision detection, waits exactly one fixed update, and finishes. It does
        // not restore detection on normal completion or disposal; callers own resume.
        public IEnumerator ExitTriggerVolumes()
        {
            m_body.detectCollisions = false;
            yield return new WaitForFixedUpdate();
        }
        // 06000f7e; ARM710ce0. Clearing trigger mode does not disable the collider.
        public void SetAsTrigger(bool on)
        {
            m_body.isKinematic = on;
            m_colliderCollision.isTrigger = on;
            if (on) m_colliderCollision.enabled = true;
        }

        // 06000f7f / ARM710d38. The retain-velocity default is read even when the
        // respawn Transform is missing. Set the supplied completion key only after
        // a live destination finishes the full original teleport sequence.
        public void TeleportWithStatusKey(GraphStorageKey effectKey)
        {
            Transform respawnPoint = Storage.GetValue<Transform>(ActorFSMKeys.LastRespawnPoint, null, true);
            bool retainVelocity = Storage.GetValue(ActorFSMKeys.RespawnRetainVelocity, false, true);
            if (respawnPoint == null) return;
            Vector3 position = respawnPoint.position;
            Quaternion rotation = respawnPoint.rotation;
            Teleport(position, rotation, retainVelocity, true);
            Storage.SetValue(effectKey, true);
        }
        // 06000f80 / ARM71107c. This preserves native operation order and failure
        // propagation. In particular, retained velocity uses the OLD world rotation
        // and local velocity before applying the requested rotation.
        public void Teleport(Vector3 worldPosition, Quaternion worldRotation, bool retainVelocity = true, bool forceVisuals = true)
        {
            AllowCollisions(false);
            m_body.isKinematic = true;
            m_colliderCollision.isTrigger = true;
            m_colliderCollision.enabled = true;
            SetWorldPosition(worldPosition);
            Tracker.OnTeleport(worldPosition);
            HomingPool.ClearTarget(false);
            if (retainVelocity)
            {
                Vector3 velocity = WorldRotation * LocalVelocity;
                SetWorldRotation(worldRotation);
                SetWorldVelocity(velocity, true);
            }
            else
            {
                SetWorldRotation(worldRotation);
                SetWorldVelocity(Vector3.zero);
            }
            m_bodyWasKinematic = false;
            CancelModifierOverrides();
            SetFSMPauseValues();
            GameplayIslandManager island = m_gameplayIslandManagerRef.GetSafe();
            if (island != null) island.ForceActiveIsland(worldPosition);
            Collider.GroundHit.Invalidate();
            Collider.GroundHitLastFrame.Invalidate();
            UpdateAbilityCameraOverride(CameraType.None, null, null);
            CinemachineCameraManager cameraManager = m_cinemachineCameraManagerRef.Get();
            cameraManager.ForceAllVirtualCamerasToPosition(WorldPosition, WorldRotation, true, true);
            m_stickyControlsDefinition.StickyControls.DeactivateStickyControls(this);
            BoostStamina.Reset(true);
            ChaosStamina.Reset(true);
            m_body.isKinematic = false;
            m_colliderCollision.isTrigger = false;
            if (forceVisuals)
            {
                SmoothedTransformProxy proxy = VisualProxy;
                Vector3 position = m_body.position;
                Quaternion rotation = m_body.rotation;
                proxy.SnapToLocation(position, rotation);
            }
            Storage.SetValue(ActorFSMKeys.RespawnTeleportToAir, true);
            EnableGameInput(true);
        }
        // 06000f81 / ARM7114bc: the original status key is TeleportToAir.
        public bool TeleportIsInProgress() => Storage.GetValue(ActorFSMKeys.RespawnTeleportToAir, false, false);

        // 06000f82 / ARM709008. Input-system lookup runs even when an existing
        // handle makes the requested operation a no-op. Only pause-menu input is
        // controlled here; it does not synthesize player movement input.
        private void EnableGameInput(bool enable)
        {
            ControlMap controlMap = ProcessManager.GetSystem<InputSystem>().ControlMapping;
            StackableDataHandle handle = m_gameInputPauseDisabledHandle;
            if (enable)
            {
                if (handle != null)
                {
                    controlMap.RemoveGameInputDisabled(handle);
                    m_gameInputPauseDisabledHandle = null;
                }
            }
            else if (handle == null)
                m_gameInputPauseDisabledHandle = controlMap.AddGameInputDisabled(GameInput.TogglePauseMenu);
        }
        // 06000f83 / ARM7115bc. Timestamp query precedes the brain null check.
        public override void OnPause()
        {
            base.OnPause();
            SetFSMPauseValues();
            MovementStop(true);
            float timestamp = GetTotalFixedTime();
            if (m_brain != null) m_brain.SetEnabled(false, timestamp);
        }
        // 06000f84 / ARM706dd8. Store the active fixed-step cached pose; outside
        // that step use the actual Rigidbody and the captured CURRENT dilation.
        private void SetFSMPauseValues()
        {
            IGraphStorage positionStorage = Storage;
            GraphStorageKey positionKey = AppFSMKeys.CurrentCharacterPausePosition;
            Vector3 position = FixedUpdateActive ? WorldPosition : m_body.position;
            positionStorage.SetValue(positionKey, position);
            IGraphStorage velocityStorage = Storage;
            GraphStorageKey velocityKey = AppFSMKeys.CurrentCharacterPauseVelocity;
            Vector3 velocity = FixedUpdateActive ? WorldVelocity : m_body.velocity * m_timeDilationCurrentFrame;
            velocityStorage.SetValue(velocityKey, velocity);
        }
        // 06000f85 / ARM711628. An active swap-start skips resume completely.
        // Retained impulses are consumed once. Otherwise query time before the
        // repeated brain null check, then re-query time before enabling the brain.
        public override void OnResume()
        {
            if (m_swapStartIsInProgress) return;
            base.OnResume();
            if (m_brain != null)
            {
                if (m_retainImpulses) m_retainImpulses = false;
                else
                {
                    float endTimestamp = GetTotalFixedTime();
                    if (m_brain != null) m_brain.EndImpulses(endTimestamp);
                }
                float enableTimestamp = GetTotalFixedTime();
                m_brain.SetEnabled(true, enableTimestamp);
            }
            Vector3 position = Storage.GetValue(AppFSMKeys.CurrentCharacterPausePosition, default(Vector3), true);
            Vector3 velocity = Storage.GetValue(AppFSMKeys.CurrentCharacterPauseVelocity, default(Vector3), true);
            MovementResume(position, velocity);
        }
        // 06000f86 / ARM7118a0. Probe saved velocity without inserting defaults.
        public Vector3 GetPausedWorldVelocity() => Storage.GetValue(AppFSMKeys.CurrentCharacterPauseVelocity, default(Vector3), false);
        // 06000f87: return ordered positive vector length after storing the output.
        public bool TryGetDirectionOverride(out Vector3 directionOverride)
        {
            directionOverride = m_modifiers.Get<Vector3>((int)DirectionModifierType.Override, true, default(Vector3));
            return directionOverride.sqrMagnitude > 0f;
        }
        // 06000f88: storage permission is read before the scalar stack. Ordered
        // nonzero rejects NaN on both ARM (MI/LE) and x86 (ucomiss/setne).
        private bool TryGetSpeedOverride(out float speedOverride, out bool speedCanBeExceeded)
        {
            speedCanBeExceeded = TryGetValue(ActorFSMKeys.SpeedLockCanBeExceeded);
            speedOverride = m_modifiers.Get<float>((int)DirectionModifierType.Speed, true, 0f);
            return speedOverride < 0f || speedOverride > 0f;
        }

        // 06000f89; ARM7119a4. The actual CharacterAbility type check precedes
        // its virtual definition getter; no replacement boost-definition record.
        public bool TryGetAbilityBoostDefinition(ActorAbilityType abilityType,
            out CharacterStamina.Definition boostDefinition)
        {
            if (TryGetAbilityFromType(abilityType, out ActorAbility ability) && ability is CharacterAbility characterAbility)
                return characterAbility.CharacterAbilityDefinition.TryGetBoostDefinition(out boostDefinition);
            boostDefinition = null;
            return false;
        }

        // 06000f8a; ARM711a94. Store the caller's preference, but the original
        // immediate Teleport call intentionally uses true for both flags.
        public void TeleportToLevelStartPosition(Transform startPosition, bool respawnRetainVelocity)
        {
            Collider.UnregisterTrackManager(m_trackManagerRef.GetSafe());
            Storage.SetValue<Transform>(ActorFSMKeys.LastRespawnPoint, startPosition);
            Storage.SetValue(ActorFSMKeys.RespawnRetainVelocity, respawnRetainVelocity);
            Vector3 position = startPosition.position;
            Quaternion rotation = startPosition.rotation;
            Teleport(position, rotation, true, true);
        }
        public bool AreControlsEnabled() => m_modifiers.Get<bool>((int)GameplayModifierType.ControlsEnabled, true, false);
        public override bool IsFormLocked() => m_modifiers.Get<bool>((int)GameplayModifierType.FormLocked, true, false);
        // 06000f8d; ARM711d60. SetParent uses its original one-argument overload.
        public void ParentBlobShadowToRig()
        {
            m_blobShadowObject.transform.SetParent(m_blobShadowRigRoot);
            m_blobShadowRaycaster.EnforceLocalPosition();
        }
        // 06000f8e; ARM706b58. Native +0x30 is Actor's visual proxy, not Character's
        // rigidbody (+0x178). Obtain the shadow transform before reading that proxy.
        public void ParentBlobShadowToRoot()
        {
            Transform shadowTransform = m_blobShadowObject.transform;
            Transform rootTransform = VisualProxy.transform;
            shadowTransform.SetParent(rootTransform);
            m_blobShadowRaycaster.EnforceLocalPosition();
        }

        // 06000f8f/90; ARM707538/707adc. These are owned MonoBehaviour coroutines,
        // not the global CoroutineUtils host. Do not clear a handle before stopping it.
        private void TryStartFixedUpdateCoroutine()
        {
            if (m_fixedUpdateCoroutine == null)
                m_fixedUpdateCoroutine = StartCoroutine(FixedUpdateCoroutine());
        }
        private void TryStopFixedUpdateCoroutine()
        {
            if (m_fixedUpdateCoroutine == null) return;
            StopCoroutine(m_fixedUpdateCoroutine);
            m_fixedUpdateCoroutine = null;
        }
        // 06000f91/f92. Body collision detection changes before base registration.
        protected override void OnDisable()
        {
            m_body.detectCollisions = false;
            base.OnDisable();
        }
        protected override void OnEnable()
        {
            m_body.detectCollisions = true;
            base.OnEnable();
        }

        // 06000f93; ARM711e04. Parameters are constructed before lookup; an
        // existing typed container is reconfigured directly without null shortcuts.
        public void InstantiateAbilityUI(CharacterAbility characterAbility)
        {
            CharacterAbilityDefinition definition = characterAbility.CharacterAbilityDefinition;
            if (definition.ViewInfoDebugOnly) return;
            CharacterAbilityUIType uiType = definition.UIType;
            if (uiType == CharacterAbilityUIType.None) return;
            UIContainerCharacterAbilityParameters parameters = new UIContainerCharacterAbilityParameters(this, characterAbility);
            if (m_characterAbilityUI.TryGetValue(uiType, out UIContainerCharacterAbility existing))
            {
                existing.Setup(parameters);
                return;
            }
            UIContainer prefab = m_dataManager.CharacterAbilityUIDefinitions[uiType].ContainerIdentifier.GetOrCreateContainer();
            Transform parent = VisualProxy.transform;
            // Native uses a CLR type test, not Unity fake-null equality.
            if (UnityEngine.Object.Instantiate<UIContainer>(prefab, parent, false) is UIContainerCharacterAbility container)
            {
                container.Setup(parameters);
                m_characterAbilityUI.Add(uiType, container);
                if (!m_uiEnabled) container.gameObject.SetActive(false);
            }
        }

        // 06000f94; ARM71204c. Remove first, then close the actual identifier
        // before testing Unity object liveness and destroying its GameObject.
        public void DestroyAbilityUI(CharacterAbilityDefinition characterAbilityDefinition)
        {
            CharacterAbilityUIType uiType = characterAbilityDefinition.UIType;
            if (!m_characterAbilityUI.TryGetValue(uiType, out UIContainerCharacterAbility container)) return;
            m_characterAbilityUI.Remove(uiType);
            container.Identifier.Close();
            if (container != null) UnityEngine.Object.Destroy(container.gameObject);
        }

        // 06000f95; ARM712188. Store the flag before the original dictionary
        // enumerator. Deconstruction and foreach disposal retain mutation failures.
        public void ToggleUI(bool on)
        {
            m_uiEnabled = on;
            foreach (var (_, container) in m_characterAbilityUI)
                container.gameObject.SetActive(on);
        }
        // 06000f96; ARM712314. Both local writes precede querying/cancelling swap-in.
        public void BeginSwapStart(bool isShortcut)
        {
            m_switchCooldownTimer = -1f;
            m_swapStartIsInProgress = !isShortcut;
            if (SwapInIsInProgress()) EndSwapIn();
        }
        // 06000f97; ARM7125c0. This predicate includes preparation; AnySwap does not.
        public bool SwapOutSequenceIsInProgress() =>
            m_swapPrepareIsInProgress || m_swapStartIsInProgress || SwapOutIsInProgress();
        // 06000f98; ARM7126d8. Original vtable slot41 (+0x3c8) is OnResume.
        // Resume may replace the animation reference; retain its subsequent live reads.
        public void EndSwapStart(bool isShortcut)
        {
            m_swapStartIsInProgress = false;
            if (isShortcut) return;
            OnResume();
            ActivateSwapTimeAnimation(true);
        }
        // 06000f99; ARM7127a4. Gravity callback finishes before capturing Storage.
        public void BeginSwapOut()
        {
            m_customGravity.EnableMaintainLastGravity(WorldPosition);
            Storage.SetValue(ActorFSMKeys.SwapOutIsInProgress, true);
        }
        // 06000f9a; ARM7125dc. Missing values stay absent (storeDefault=false).
        private bool SwapOutIsInProgress() =>
            Storage.GetValue(ActorFSMKeys.SwapOutIsInProgress, false, false);
        // 06000f9b; ARM7128b0. The original closed generic argument is Boolean.
        public void EndSwapOut() => Storage.RemoveValue<bool>(ActorFSMKeys.SwapOutIsInProgress);
        // 06000f9c; ARM7129a4. The BCL IReadOnlyDictionary overload chooses the
        // original default. Publish/re-read the spawner before Spawn; animation
        // definition is read after obtaining the fullscreen effect-handle collection.
        public void PlaySwapGapEffects(CharacterId characterId)
        {
            ParticleEffectSpawner defaultSpawner = m_swapGapParticleSpawner;
            SerializableDictionary<CharacterId, ParticleEffectSpawner> overrides = m_swapGapParticleSpawnerOverride;
            m_swapGapParticle = System.Collections.Generic.CollectionExtensions.GetValueOrDefault(
                overrides, characterId, defaultSpawner);
            m_swapGapParticle.Spawn();
            List<FullscreenShaderManager.ParametersHandle> effects = GetFullscreenEffectHandleInstances(1699736586);
            TriggerAnimationEnter(m_swapOutAnimationDefinition, effects);
        }
        // 06000f9d; ARM712a44. Clear the field only after both completion calls.
        public void StopSwapGapEffects()
        {
            List<FullscreenShaderManager.ParametersHandle> effects = GetFullscreenEffectHandleInstances(1699736586);
            TriggerAnimationLeave(m_swapOutAnimationDefinition, effects);
            m_swapGapParticle.StopAndClear();
            m_swapGapParticle = null;
        }
        // 06000f9e; ARM712aa0. The storage and animation callbacks precede their
        // respective live constant read and preparation-flag publication.
        public void BeginSwapIn()
        {
            Storage.SetValue(ActorFSMKeys.SwapInIsInProgress, true);
            m_switchCooldownTimer = Constants.QuickSwitchCooldownTime;
            List<FullscreenShaderManager.ParametersHandle> effects = GetFullscreenEffectHandleInstances(1699736586);
            TriggerAnimationEnter(m_swapInAnimationDefinition, effects);
            m_swapPrepareIsInProgress = true;
        }
        // 06000f9f; ARM712be0. Preserve callback-before-flag ordering.
        public void ContinueSwapIn()
        {
            Storage.SetValue(ActorFSMKeys.SwapInIsInProgress, true);
            m_swapPrepareIsInProgress = false;
        }
        // 06000fa0; ARM712454. Removal, gravity release and animation leave are
        // ordered. GetSafe supplies a plain managed manager; the null test is CLR
        // reference comparison, followed by the current world-position read.
        public void EndSwapIn()
        {
            Storage.RemoveValue<bool>(ActorFSMKeys.SwapInIsInProgress);
            m_customGravity.DisableMaintainLastGravity();
            List<FullscreenShaderManager.ParametersHandle> effects = GetFullscreenEffectHandleInstances(1699736586);
            TriggerAnimationLeave(m_swapInAnimationDefinition, effects);
            GameplayIslandManager island = m_gameplayIslandManagerRef.GetSafe();
            if (island != null) island.ForceActiveIsland(WorldPosition);
        }
        // 06000fa1; ARM712358. A query must not create an absent flag.
        public bool SwapInIsInProgress() =>
            Storage.GetValue(ActorFSMKeys.SwapInIsInProgress, false, false);
        // 06000fa2; ARM7069a8. Deliberately excludes the prepare flag, and queries
        // swap-in before swap-out when the start flag did not short-circuit.
        public bool AnySwapInProgress() =>
            m_swapStartIsInProgress || SwapInIsInProgress() || SwapOutIsInProgress();

        // Original 0x06000fa3. Signed elapsed milliseconds are promoted to Int64;
        // the manager callback is required and deliberately has no null guard.
        public void ReportAirTime(int elapsedTimeInAirMS)
        {
            m_saveManagerRef.Get().CurrentSave.GetOrCreatePlayerStatData(SaveDataPlayerStat.Type.AirTimeMS).IncrementCounter(elapsedTimeInAirMS);
            m_characterManager.OnCharacterAirTimeReported();
        }

        // Original 0x06000fa4. Persist the counter before invoking the original
        // stopped-boosting callback; do not clamp negative elapsed values.
        public void ReportBoostingTime(int elapsedTimeBoostingMS)
        {
            m_saveManagerRef.Get().CurrentSave.GetOrCreatePlayerStatData(SaveDataPlayerStat.Type.BoostTimeMS).IncrementCounter(elapsedTimeBoostingMS);
            m_characterManager.OnCharacterStoppedBoosting();
        }
        // 06000fa5; ARM712e00. Unity object equality precedes the live field reread.
        public void ToggleBlobShadow(bool on)
        {
            if (m_blobShadowObject == null) return;
            m_blobShadowObject.SetActive(on);
        }

        // Original 0x06000fa6. Unity liveness is tested first; the actual field is
        // then reloaded and explicitly cast, retaining invalid-cast behavior.
        public bool DebugInvulnerable()
        {
            if (m_debug == null) return false;
            return ((CharacterDebug)m_debug).DebugInvulnerable();
        }

        // Original 0x06000fa7. This shares the same Unity gate and throwing cast.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        public void DebugToggleInvulnerable()
        {
            if (m_debug == null) return;
            ((CharacterDebug)m_debug).DebugToggleInvulnerable();
        }
        // 06000fa8; ARM7087cc. Preserve the engine null check and live field reread.
        public void ActivateSwapTimeAnimation(bool enable)
        {
            if (m_swapTimeAnimation == null) return;
            m_swapTimeAnimation.enabled = enable;
        }
        // 06000fa9; ARM708884. Capture the storage collection once, before obtaining
        // the authored list enumerator. Cache only keys that exist, retaining raw
        // value-wrapper/reference identity and ordinary Add/foreach failure behavior.
        private void CacheFSMValues()
        {
            m_fsmObjectsCached.Clear();
            IReadOnlyDictionary<GraphStorageKey, object> collection = Storage.GetCollection();
            foreach (string name in m_characterSwapFSMValuesToPersist)
            {
                GraphStorageKey key = new GraphStorageKey(name, 0, 0);
                if (collection.TryGetValue(key, out object value))
                    m_fsmObjectsCached.Add(key, value);
            }
        }
        // 06000faa; ARM706ba8. Capture each raw value and current Storage separately;
        // restoring uses SetValue<Object>, not reflection/conversion/unwrapping. Clear
        // only after the entire enumeration succeeds; failures leave the cache intact.
        private void RestoreFSMValues()
        {
            if (m_fsmObjectsCached.Count == 0) return;
            foreach (KeyValuePair<GraphStorageKey, object> entry in m_fsmObjectsCached)
                Storage.SetValue<object>(entry.Key, entry.Value);
            m_fsmObjectsCached.Clear();
        }

        // 06000fab; ARM7130ac. Full original initializer sequence remains above,
        // before Actor base construction. No constructor body work follows base.
        public Character() { }
    }
}
