using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputSystem : MonoBehaviour, ISystem
    {
        [SerializeField] private ControlMapping m_controlMapping;
        [SerializeField] private InputMonitor m_inputMonitor;

        public ControlMapping ControlMapping => m_controlMapping; // Original 060023e1.
        public InputMonitor InputMonitor => m_inputMonitor; // Original 060023e2.

        // Original 060023e3 supplies null, false, false to RegisterSystem.
        private void Awake() => ProcessManager.RegisterSystem(this);
        private void OnDestroy() => ProcessManager.UnregisterSystem(this); // Original 060023e4.
        public InputSystem() : base() { } // Original 060023e5; no field initializers.
    }
}
