using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a54; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLevelVisibilityEntity : MonoBehaviour, IEntityActivatable
    {
        [Tooltip("Defines the activation logic.")]
        [SerializeField]
        private EntityActivationDefinition m_entityActivationDefinition;
        [FormerlySerializedAs("m_onTriggerLevelActivatedEvents")]
        [Tooltip("Events that are triggered on enabled.")]
        [SerializeField]
        private UnityEvent m_onEnabled;
        [Tooltip("Events that are triggered on disabled.")]
        [SerializeField]
        private UnityEvent m_onDisabled;
        private bool m_enabledOnAwake;
        private bool m_hasRegistered;
        private bool m_deactivationInProgress;
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        protected readonly SystemRef<EntityActivationManager> m_entityActivationManagerRef = ProcessManager.GetSystemRef<EntityActivationManager>();

        // 0x06003b5d/0x06003b5e
        public EntityActivationDefinition Definition => m_entityActivationDefinition;
        public Transform Transform => transform;

        // 0x06003b5f
        protected virtual void Awake()
        {
            m_enabledOnAwake = gameObject.activeInHierarchy;
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        // 0x06003b60 (0x6C0720): only an external deactivation of this object itself is
        // forwarded; the entity's own OnEntityDisable is excluded by the in-progress flag.
        private void OnDisable()
        {
            if (m_hasRegistered && !gameObject.activeSelf && !m_deactivationInProgress)
                m_entityActivationManagerRef?.GetSafe()?.Deactivate(this);
        }

        // 0x06003b61
        protected virtual void OnDestroy()
        {
            if (m_entityActivationManagerRef.IsValid() && m_hasRegistered)
                m_entityActivationManagerRef.Get().Deregister(this);
            if (m_levelManagerRef.IsValid())
                m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
        }

        // 0x06003b62
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }

        // 0x06003b63: a destroyed entity unsubscribes through the checked Get().
        private void OnLevelActivated(LevelManagerLevel _)
        {
            if (this == null)
            {
                m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
                return;
            }
            m_entityActivationManagerRef.InvokeOnValid(OnEntityManagerValid);
        }

        // 0x06003b64 (0x6C0A80): re-registration on every level activation. The awake
        // visibility is restored first, and only a definition with activation (or
        // deactivation) logic receives the matching callback.
        private void OnEntityManagerValid(EntityActivationManager entityActivationManager)
        {
            entityActivationManager.Deregister(this);
            if (m_enabledOnAwake != gameObject.activeInHierarchy)
                gameObject.SetActive(m_enabledOnAwake);
            Action onActivate = null;
            if (m_entityActivationDefinition.CanActivate)
                onActivate = OnEntityEnable;
            Action onDeactivate = null;
            if (m_entityActivationDefinition.CanDeactivate)
                onDeactivate = OnEntityDisable;
            entityActivationManager.Register(this, m_enabledOnAwake, onActivate, onDeactivate);
            m_hasRegistered = true;
        }

        // 0x06003b65
        private void OnEntityEnable()
        {
            if (this == null || gameObject == null) return;
            gameObject.SetActive(true);
            m_onEnabled?.Invoke();
        }

        // 0x06003b66
        private void OnEntityDisable()
        {
            if (this == null || gameObject == null) return;
            m_deactivationInProgress = true;
            gameObject.SetActive(false);
            m_deactivationInProgress = false;
            m_onDisabled?.Invoke();
        }

        // 0x06003b67
        public void SetDefinition(EntityActivationDefinition definition)
        {
            m_entityActivationDefinition = definition;
        }
    }
}
