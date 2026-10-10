using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HashedComponent : MonoBehaviour
    {
        [SerializeField] private UnityEvent m_onEnabled;
        [SerializeField] private UnityEvent m_onDisabled;

        // Original lifecycle callbacks invoke the authored events directly.
        // The original constructor does not allocate replacement events.
        private void OnEnable() => m_onEnabled.Invoke();
        private void OnDisable() => m_onDisabled.Invoke();
        public HashedComponent() { }
    }
}
