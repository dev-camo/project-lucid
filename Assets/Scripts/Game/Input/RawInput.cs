using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 020006b3; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class RawInput : MonoBehaviour
    {
        [SerializeField] private UnityEvent m_onControllerInput;
        private bool m_fired;

        // 060023f8 (1.10.1 RVA 0x58C380)
        private void Update()
        {
            if (Input.anyKeyDown && !m_fired) FireOnController();
        }

        // 060023f9 (1.10.1 RVA 0x58C3C0): unguarded by m_fired; the event alone is null-checked.
        private void FireOnController()
        {
            m_fired = true;
            m_onControllerInput?.Invoke();
        }

        // 060023fa (1.10.1 RVA 0x58C3E0)
        private void OnEnable()
        {
            ControlMapping.Subscribe(GameInput.SkipTheSplash, OnInput, InputTrigger.Down, -1);
        }

        // 060023fb (1.10.1 RVA 0x58C480)
        private void OnDisable()
        {
            ControlMapping.Unsubscribe(GameInput.SkipTheSplash, OnInput, -1);
        }

        // 060023fc (1.10.1 RVA 0x58C520): NaN compares unequal to zero and fires.
        private void OnInput(float value)
        {
            if (value != 0f && !m_fired) FireOnController();
        }

        // 060023fd (1.10.1 RVA 0x58C550)
        public RawInput()
        {
        }
    }
}
