using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a66; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivator : MonoBehaviour, IEntityActivatable
    {
        [SerializeField] private EntityActivationDefinition m_definition;
        [SerializeField] private UnityEvent m_onActivation;
        [SerializeField] private UnityEvent m_onDeactivation;
        private SystemRef<EntityActivationManager> m_entityActivationManagerRef;

        // 0x06003bc3/0x06003bc4
        public EntityActivationDefinition Definition => m_definition;
        public Transform Transform => transform;

        // 0x06003bc5
        private void Start()
        {
            m_entityActivationManagerRef = ProcessManager.GetSystemRef<EntityActivationManager>();
            m_entityActivationManagerRef.InvokeOnValid(Initialise);
        }

        // 0x06003bc6 (0x6C4170): the argument is ignored; registration goes through the
        // reference's checked Get().
        private void Initialise(EntityActivationManager entityActivationManager)
        {
            m_entityActivationManagerRef.Get().Register(this, gameObject.activeInHierarchy, OnActivateCallback, OnDeactivateCallback);
        }

        // 0x06003bc7
        private void OnDestroy()
        {
            m_entityActivationManagerRef?.GetSafe()?.Deregister(this);
        }

        // 0x06003bc8/0x06003bc9
        public void OnActivateEvent()
        {
            m_entityActivationManagerRef.Get().Activate(this);
        }

        public void OnDeactivateEvent()
        {
            m_entityActivationManagerRef.Get().Deactivate(this);
        }

        // 0x06003bca/0x06003bcb
        private void OnActivateCallback()
        {
            m_onActivation.Invoke();
        }

        private void OnDeactivateCallback()
        {
            m_onDeactivation.Invoke();
        }
    }
}
