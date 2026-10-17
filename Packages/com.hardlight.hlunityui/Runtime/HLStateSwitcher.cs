using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 0x020000f7 (TypeDefIndex 12071); x86-64 bodies.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [ExecuteInEditMode]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLStateSwitcher : MonoBehaviour
    {
        [SerializeField]
        private bool m_enabled;
        [SerializeField]
        private UnityEvent m_onEnabled;
        [SerializeField]
        private UnityEvent m_onDisabled;

        public bool Enabled
        {
            // 0x060006f4 (0x1b73900)
            get { return m_enabled; }
            // 0x060006f5 (0x1b6fd10): only a change raises the events.
            set
            {
                if (m_enabled != value)
                {
                    m_enabled = value;
                    InvokeEvents(value);
                }
            }
        }

        // 0x060006f6 (0x1b73940): replays the serialized state.
        private void Start()
        {
            InvokeEvents(m_enabled);
        }

        // 0x060006f7 (0x1b73910): an enabled switcher without m_onEnabled falls
        // through to m_onDisabled (the native body reloads +0x30 when +0x28 is null).
        private void InvokeEvents(bool enabled)
        {
            if (enabled && m_onEnabled != null)
            {
                m_onEnabled.Invoke();
            }
            else if (m_onDisabled != null)
            {
                m_onDisabled.Invoke();
            }
        }
    }
}
