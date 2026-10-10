using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [SelectionBase]
    public class Collectable : EntityActivatable
    {
        [SerializeField] private CollectableType m_type;
        [SerializeField] private string m_triggerTag = "Player";
        [SerializeField] private UnityEvent m_onInitialise;
        [SerializeField] private UnityEvent m_onActive;
        [Tooltip("If previously collected then use the ghosted variant of this collectable.")]
        [SerializeField] private bool m_ghostedIfCollected;
        [SerializeField]
        [Tooltip("Events to fire when collectable activated as ghosted.")]
        private UnityEvent m_onActiveGhosted;
        [Tooltip("When collected as ghosted, the collectable type to award.")]
        [SerializeField] private CollectableType m_ghostedType;
        [Tooltip("When collected as ghosted, the collectable amount to award.")]
        [SerializeField] private int m_ghostedAmount;
        [Tooltip("Ghosted state will be cancelled and collectable returned to normal on character respawn.")]
        [SerializeField] private bool m_ghostedCancelOnCharacterRespawn;
        [HideInInspector]
        [SerializeField] private bool[] m_freezeRotation = new bool[3];
        [SerializeField] private GameObject m_visualisation;
        [SerializeField] private Vector3 m_offsetTranslate;
        [InspectorReadOnly]
        [ShowIf("m_type", CollectableType.Orb)]
        [SerializeField] private string m_guid;
        [SerializeField] private CollectableChangeMetadata m_metadata;
        [SerializeField] private CollectableChangeMetadata m_metadataAfterFirstCollected;
        [SerializeField] private bool m_destroyOnComplete = true;
        [SerializeField]
        [Tooltip("Collision trigger is enforced to be a sphere so radius can be set from tier distance.")]
        private SphereCollider m_triggerCollider;
        [Tooltip("Specifies the collision trigger distances to use at each collectable attract tier.")]
        [SerializeField] private List<float> m_triggerDistanceTiers = new List<float>();
        public bool AutoRegisterToCollectionManager = true;
        private static readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();
        private static readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        private static readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private bool m_registeredOnCharacterChange;
        private bool m_markedAsCollected;
        private int m_collectedCount;
        private bool m_hasBeenTriggered;
        private bool m_isDestroyed;
        private string m_levelName;
        public Action<Collectable> OnCollected;
        public Action<Collectable> OnDeactivated;

        public CollectableType Type => m_type;
        public string GuidId => m_guid;
        public bool HasBeenTriggered => m_hasBeenTriggered;
        public bool HasTriggerCollider => m_triggerCollider != null;
        public Vector3 FreezeRotation => new Vector3(m_freezeRotation[0] ? 1f : 0f,
            m_freezeRotation[1] ? 1f : 0f, m_freezeRotation[2] ? 1f : 0f);
        private bool IsGhosted => m_ghostedIfCollected && m_collectedCount > 0;

        protected override void Start()
        {
            base.Start();
            m_levelName = gameObject.scene.name;
            m_collectableManagerRef.InvokeOnValid(Initialise);
        }

        private void Initialise(CollectableManager collectableManager)
        {
            if (!AutoRegisterToCollectionManager) return;
            m_levelManagerRef.InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated, true));
            collectableManager.OnCollectableForceDeactivate += ForceDeactivate;
        }

        private void OnLevelActivated(LevelManagerLevel level)
        {
            AutoRegister();
            InvokeActiveEvents();
        }

        private void AutoRegister()
        {
            m_markedAsCollected = false;
            m_collectedCount = 0;
            CollectableManager collectableManager = m_collectableManagerRef.Get();
            if (m_type == CollectableType.Orb) collectableManager.OnCollectableStateUpdate += OnCollectableStateUpdate;
            collectableManager.RegisterCollectable(m_type, 1);
            CharacterManager characterManager = m_characterManagerRef.Get();
            RegisterForCharacterRespawn(characterManager, true);
            RegisterOnCharacterChange(characterManager, true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_onInitialise.Invoke();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_hasBeenTriggered) OnComplete();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_isDestroyed = true;
            CollectableManager collectableManager = m_collectableManagerRef.GetSafe();
            if (collectableManager != null)
            {
                collectableManager.OnCollectableStateUpdate -= OnCollectableStateUpdate;
                collectableManager.OnCollectableForceDeactivate -= ForceDeactivate;
            }
            LevelManager levelManager = m_levelManagerRef.GetSafe();
            if (levelManager != null) levelManager.RemoveLevelActivatedAction(OnLevelActivated);
            CharacterManager characterManager = m_characterManagerRef.GetSafe();
            if (characterManager != null)
            {
                RegisterForCharacterRespawn(characterManager, false);
                RegisterOnCharacterChange(characterManager, false);
            }
        }

        private void OnCollectableStateUpdate(CollectableType collectableType, CollectableState collectableState,
            CollectableChangeMetadata metadata)
        {
            if (collectableType != CollectableType.Orb || m_markedAsCollected) return;
            m_markedAsCollected = true;
            m_collectedCount = unchecked(m_collectedCount + 1);
            Deactivate();
            OnComplete();
        }

        private void OnTriggerEnter(Collider other) { OnTriggerCollect(other); }
        private void OnTriggerStay(Collider other) { OnTriggerCollect(other); }
        public void OnTriggerCollect(Character character) { OnTriggerCollect(character.ColliderCollision); }

        private void OnTriggerCollect(Collider other)
        {
            if (!enabled || m_markedAsCollected || !other.CompareTag(m_triggerTag)) return;
            CharacterManager characterManager = m_characterManagerRef.Get();
            if (m_triggerCollider != null && characterManager.IsCurrentCharacterColliderCollision(other)
                && characterManager.TryGetCurrentCharacter(out Character character))
            {
                Vector3 center = other.bounds.center;
                Vector3 direction = m_triggerCollider.bounds.center - center;
                float distance = direction.magnitude;
                if (distance > 0.0001f)
                {
                    direction /= distance;
                    if (Physics.Raycast(center, direction, out RaycastHit hit, distance, character.Settings.Collider.TrackMask)
                        && hit.collider != m_triggerCollider && hit.distance < distance) return;
                }
            }
            Transform target = other.transform;
            Vector3 offset = other.bounds.size * 0.5f;
            IEffectData effectData = new TargetedEffect { Offset = offset, Target = target };
            Component sender = this;
            ProcessManager.GetSystem<MessageManager>().ComponentMessagesWithCompletion.PublishMessage<IEffectData>(
                in sender, in effectData, OnComplete, -1);
            m_hasBeenTriggered = true;
            OnCollected?.Invoke(this);
        }

        public void Action_MarkAsCollected()
        {
            if (m_markedAsCollected) return;
            m_markedAsCollected = true;
            if (m_collectableManagerRef.IsNull()) return;
            CollectableManager collectableManager = m_collectableManagerRef.Get();
            CollectableChangeMetadata metadata = m_collectedCount == 0 ? m_metadata : m_metadataAfterFirstCollected;
            if (IsGhosted)
            {
                if (m_ghostedType != CollectableType.None && m_ghostedAmount > 0)
                    collectableManager.ChangeCollectableAmount(m_ghostedType, m_ghostedAmount, metadata);
            }
            else if (m_type == CollectableType.Orb) collectableManager.CollectOrb(m_type, metadata);
            else collectableManager.ChangeCollectableAmount(m_type, 1, metadata);
            m_collectedCount = unchecked(m_collectedCount + 1);
            Deactivate();
        }

        protected override void Deactivate()
        {
            base.Deactivate();
            OnDeactivated?.Invoke(this);
        }

        public void Action_MarkAsNotCollected() { m_markedAsCollected = false; }

        private void OnComplete()
        {
            m_hasBeenTriggered = false;
            if (!m_markedAsCollected) Action_MarkAsCollected();
            if (!m_isDestroyed && m_destroyOnComplete) Destroy(gameObject);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (m_visualisation != null) m_visualisation.transform.localPosition = m_offsetTranslate;
            Action_MarkAsNotCollected();
            InvokeActiveEvents();
        }

        private void RegisterForCharacterRespawn(CharacterManager characterManager, bool active)
        {
            if (!m_ghostedCancelOnCharacterRespawn) return;
            if (active) characterManager.OnCharacterRespawn += OnCharacterRespawn;
            else characterManager.OnCharacterRespawn -= OnCharacterRespawn;
        }

        private void OnCharacterRespawn()
        {
            if (IsGhosted && !m_markedAsCollected)
            {
                m_collectedCount = 0;
                m_onActive?.Invoke();
            }
        }

        private void RegisterOnCharacterChange(CharacterManager characterManager, bool active)
        {
            if (m_registeredOnCharacterChange == active) return;
            if (active)
            {
                if (m_triggerDistanceTiers.Count == 0 || m_triggerCollider == null) return;
                characterManager.RegisterOnCharacterChange(OnCharacterChange, true);
            }
            else characterManager.ReleaseOnCharacterChange(OnCharacterChange);
            m_registeredOnCharacterChange = active;
        }

        // Original 06003b05 clamps to Count, preserving the upper-end index fault.
        private void OnCharacterChange(Character character)
        {
            int count = m_triggerDistanceTiers.Count;
            CollectableManager collectableManager = m_collectableManagerRef.Get();
            CollectableType collectableType = IsGhosted ? m_ghostedType : m_type;
            int tierIndex = collectableManager.GetCollectableTierIndex(collectableType);
            m_triggerCollider.radius = m_triggerDistanceTiers[Mathf.Min(tierIndex, count)];
        }

        private void InvokeActiveEvents()
        {
            if (IsGhosted) m_onActiveGhosted?.Invoke();
            else m_onActive?.Invoke();
        }

        public void Action_ManuallyActivate() { OnActivate(); }

        private void ForceDeactivate(CollectableType type)
        {
            if (m_type != type) return;
            Deactivate();
            gameObject.SetActive(false);
        }

        public Collectable() { }
    }
}
