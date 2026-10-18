using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a52; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class EntityActivationEvent : EntityActivatable
    {
        [SerializeField]
        [Tooltip("Events fired when activation is triggered.")]
        private UnityEvent m_onActivate = new UnityEvent();
        [SerializeField]
        [Tooltip("Events fired when deactivation is triggered.")]
        private UnityEvent m_onDeactivate = new UnityEvent();

        // 0x06003b53/0x06003b54 (0x6BFAD0/0x6BFAE0): neither override calls the base, so the
        // inherited registered-as-active flag is never updated for this entity.
        protected override void OnActivate()
        {
            m_onActivate.Invoke();
        }

        protected override void OnDeactivate()
        {
            m_onDeactivate.Invoke();
        }
    }
}
