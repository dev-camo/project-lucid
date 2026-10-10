using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EntityActivatable : MonoBehaviour, IEntityActivatable
    {
        [SerializeField] private EntityActivationDefinition m_definition;
        private bool m_registeredForActivation;
        private bool m_registeredAsActive;
        private SystemRef<EntityActivationManager> m_entityActivationManagerRef;

        public EntityActivationDefinition Definition => m_definition;
        public Transform Transform => transform;
        protected virtual void Start() { RegisterForActivation(); }
        protected virtual void OnEnable() { Activate(); }
        protected virtual void OnDisable() { Deactivate(); }
        protected virtual void OnDestroy() { DeregisterForActivation(); }

        // Original 06003b4c publishes registration before resolving the manager.
        private void RegisterForActivation()
        {
            if (m_definition == null) return;
            m_registeredForActivation = true;
            m_entityActivationManagerRef = ProcessManager.GetSystemRef<EntityActivationManager>();
            EntityActivationManager manager = m_entityActivationManagerRef.Get();
            bool active = gameObject.activeInHierarchy;
            manager.Register(this, active, OnActivate, OnDeactivate);
        }

        protected void Activate()
        {
            if (!m_registeredAsActive) m_entityActivationManagerRef?.GetSafe()?.Activate(this);
        }

        protected virtual void Deactivate()
        {
            if (m_registeredAsActive) m_entityActivationManagerRef?.GetSafe()?.Deactivate(this);
        }

        private void DeregisterForActivation()
        {
            if (!m_registeredForActivation) return;
            m_entityActivationManagerRef?.GetSafe()?.Deregister(this);
            m_registeredForActivation = false;
        }

        protected virtual void OnActivate() { m_registeredAsActive = true; }
        protected virtual void OnDeactivate() { m_registeredAsActive = false; }
        protected EntityActivatable() { }
    }
}
