using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Game.Runtime 0x02000a44. Genuine collision/terrain contact bridge; the full
    // Actor/Character/App and authored metadata dependency graph remains open.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterCollisionData : ColliderData
    {
        [SerializeField]
        [Tooltip("Metadata to define the terrain type.")]
        private MetadataGroups m_metadata;

        [Tooltip("Ribbon associated to tracker metadata.")]
        [SerializeField]
        private Ribbon m_trackerRibbon;

        [Tooltip("Game object that determines the tracker forward direction. This is used to orientate the controls to align with the tracker entry and exit points.")]
        [SerializeField]
        private GameObject m_trackerForward;

        [Tooltip("Optional collision modifier data.")]
        [SerializeField]
        private List<CharacterCollisionModifier> m_collisionModifiers = new List<CharacterCollisionModifier>();
        private CharacterManager m_characterManager;

        public bool HasModifiableContacts { get; set; }
        public bool HasTracker { get; private set; }
        public bool HasMovementOverride { get; private set; }
        public bool IsClimbable { get; private set; }
        public bool HasEffects { get; private set; }
        public bool HasCameraOverride { get; private set; }
        public bool HasHalfPipeMetadata { get; private set; }
        public MetadataGroup TrackerMetadata { get; private set; }
        public MetadataGroup ClimbableMetadata { get; private set; }
        public MetadataGroup EffectMetadata { get; private set; }
        public MetadataGroup CameraOverrideMetadata { get; private set; }
        public MetadataGroup HalfPipeMetadata { get; private set; }
        public bool HasColliderModifier { get; private set; }
        public CharacterColliderModifier ColliderModifier { get; private set; }
        public bool HasTargetingModifier { get; private set; }
        public CharacterTargetingModifier TargetingModifier { get; private set; }
        public bool HasHalfPipeTrajectoryDefinition { get; private set; }
        private HalfPipeTrajectoryDefinition m_halfPipeTrajectoryDefinition;
        public HalfPipeTrajectoryDefinition HalfPipeTrajectoryDefinition => m_halfPipeTrajectoryDefinition;
        public bool HasHalfPipeIgnoreDefinition { get; private set; }

        // 0x06003ad7: keep Unity destroyed-object semantics instead of returning
        // the stored reference unconditionally.
        public Ribbon GetTrackerRibbon() => m_trackerRibbon != null ? m_trackerRibbon : null;

        // 0x06003ad8: authored tracker presence precedes Unity liveness query.
        public bool HasTrackerForward() => HasTracker && m_trackerForward != null;
        public TerrainMovementDefinition MovementOverride { get; private set; }

        // 0x06003adb / ARM 0x695008: subscribe to the actual system registry.
        private void Awake()
        {
            ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(OnCharacterManagerValid);
        }

        // 0x06003adc / ARM 0x6950f8: exact original terrain lookup order. A
        // missing definition leaves prior fields untouched; no synthetic reset.
        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            m_characterManager = characterManager;
            var dataManager = ProcessManager.GetSystemRef<App>(null, true).Get().DataManager;
            if (dataManager.TerrainDefinitions.TryGetValue(TerrainMetadataType.Tracker, out var trackerDefinition))
            {
                TrackerMetadata = m_metadata.GetGroup(trackerDefinition.MetadataGroupKey);
                HasTracker = TrackerMetadata != null;
                if (HasTracker && TrackerMetadata.TryGetObject<TerrainMovementDefinition>(trackerDefinition.MetadataMovementKey, out var movementOverride))
                {
                    HasMovementOverride = movementOverride != null;
                    if (HasMovementOverride)
                        MovementOverride = movementOverride;
                }
            }
            if (dataManager.TerrainDefinitions.TryGetValue(TerrainMetadataType.Climbable, out var climbableDefinition))
            {
                ClimbableMetadata = m_metadata.GetGroup(climbableDefinition.MetadataGroupKey);
                IsClimbable = ClimbableMetadata != null;
            }
            if (dataManager.TerrainDefinitions.TryGetValue(TerrainMetadataType.Effects, out var effectsDefinition))
            {
                EffectMetadata = m_metadata.GetGroup(effectsDefinition.MetadataGroupKey);
                HasEffects = EffectMetadata != null;
            }
            if (dataManager.TerrainDefinitions.TryGetValue(TerrainMetadataType.CameraOverride, out var cameraDefinition))
            {
                CameraOverrideMetadata = m_metadata.GetGroup(cameraDefinition.MetadataGroupKey);
                HasCameraOverride = CameraOverrideMetadata != null;
            }
            if (dataManager.TerrainDefinitions.TryGetValue(TerrainMetadataType.HalfPipe, out var halfPipeDefinition))
            {
                HalfPipeMetadata = m_metadata.GetGroup(halfPipeDefinition.MetadataGroupKey);
                HasHalfPipeMetadata = HalfPipeMetadata != null;
                // The original rereads metadata and the dictionary for the two
                // half-pipe keys; retain those observable accesses and order.
                if (HalfPipeMetadata != null)
                {
                    var trajectoryKey = dataManager.HalfPipeDefinitions[HalfPipeType.Default].MetadataTrajectoryKey;
                    HasHalfPipeTrajectoryDefinition = HalfPipeMetadata.TryGetObject<HalfPipeTrajectoryDefinition>(
                        trajectoryKey, out m_halfPipeTrajectoryDefinition) && m_halfPipeTrajectoryDefinition != null;
                    var ignoreKey = dataManager.HalfPipeDefinitions[HalfPipeType.Default].MetadataIgnoreKey;
                    HasHalfPipeIgnoreDefinition = HalfPipeMetadata.TryGetObject<HalfPipeIgnoreDefinition>(
                        ignoreKey, out var ignoreDefinition) && ignoreDefinition != null;
                }
            }
            HasColliderModifier = TryFindModifier<CharacterColliderModifier>(modifier => ColliderModifier = modifier);
            HasTargetingModifier = TryFindModifier<CharacterTargetingModifier>(modifier => TargetingModifier = modifier);
        }

        // 0x06003add / generic ARM 0x8c8434. First matching original modifier,
        // with foreach disposal on return and callback exceptions propagated.
        private bool TryFindModifier<T>(Action<T> setter) where T : CharacterCollisionModifier
        {
            foreach (var modifier in m_collisionModifiers)
            {
                if (modifier is T)
                {
                    setter((T)modifier);
                    return true;
                }
            }
            return false;
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (IsValid(collider))
                OnEnter(null);
        }

        // Collision.collider is evaluated even when metadata has no data.
        private void OnCollisionEnter(Collision collision)
        {
            if (IsValid(collision.collider))
                OnEnter(collision);
        }
        private void OnTriggerStay(Collider collider)
        {
            if (IsValid(collider))
                OnStay(null);
        }
        private void OnCollisionStay(Collision collision)
        {
            if (IsValid(collision.collider))
                OnStay(collision);
        }
        private void OnTriggerExit(Collider collider)
        {
            if (IsValid(collider))
                OnExit(null);
        }
        private void OnCollisionExit(Collision collision)
        {
            if (IsValid(collision.collider))
                OnExit(collision);
        }

        // Native caller3ae4..3ae6 reads manager field0x38, exactly original
        // GetCurrentCharacterUnsafe06001422 after inline, without validity checks.
        private void OnEnter(Collision collision) => m_characterManager.GetCurrentCharacterUnsafe().OnCollisionDataEnter(this, collision);
        private void OnStay(Collision collision) => m_characterManager.GetCurrentCharacterUnsafe().OnCollisionDataStay(this, collision);
        private void OnExit(Collision collision) => m_characterManager.GetCurrentCharacterUnsafe().OnCollisionDataExit(this, collision);

        // 0x06003ae7: short-circuit metadata first, then capture the manager
        // receiver before evaluating Collider.transform. Contact helpers reread
        // the manager and its unchecked current-character field after this call.
        private bool IsValid(Collider collider) => m_metadata.HasData && m_characterManager.IsCurrentCharacterTransform(collider.transform);

        // 0x06003ae8: the authored modifier list is initialized before ColliderData
        // base construction. All other original own fields begin at CLR defaults.
        public CharacterCollisionData() { }
    }
}
