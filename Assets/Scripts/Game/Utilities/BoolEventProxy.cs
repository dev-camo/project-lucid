using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009e7; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BoolEventProxy : MonoBehaviour
    {
        [SerializeField] private bool m_inverseBool;
        [SerializeField] private UnityEvent<bool> m_unityEvent;

        // 0x060038ec (0x6A6D20): the forwarded value is value XOR m_inverseBool.
        public void Trigger(bool value)
        {
            m_unityEvent.Invoke(m_inverseBool ? !value : value);
        }

        // 0x060038ed: one error per persistent listener that targets this component.
        private void OnValidate()
        {
            if (!gameObject.CanValidate()) return;
            int count = m_unityEvent.GetPersistentEventCount();
            for (int i = 0; i < count; ++i)
            {
                if (m_unityEvent.GetPersistentTarget(i) == this)
                    HLOutput.LogError("Cannot target self with BoolEventProxy, otherwise will cause infinite callback.");
            }
        }
    }
}
