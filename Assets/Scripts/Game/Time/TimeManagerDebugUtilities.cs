using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeManagerDebugUtilities (TypeDef 0x020007f2, x86-64 0x5F7A20..0x5F7F8F).
    // Retail compiles out the [Conditional("BUILD_DEVELOPMENT")] calls, so Awake and OnDestroy are empty.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimeManagerDebugUtilities : MonoBehaviour
    {
        [SerializeField] private TimeManager m_timeManager;
        private const string DebugSpeedMenu = "GameSpeed";
        private readonly float[] m_debugSpeedSettings =
            { 5f, 2f, 1f, 0.95f, 0.9f, 0.85f, 0.8f, 0.75f, 0.7f, 0.65f, 0.6f, 0.5f, 0.25f, 0.1f, 0f };

        private void Awake() { } // 06002df8
        private void OnDestroy() { } // 06002df9

        // 06002dfa: the last (zero) setting resumes at normal speed, any other setting pauses.
        private void OnDebugGamePause(float amount)
        {
            int index = GetDebugSpeedSettingIndex();
            m_timeManager.OverrideUnityTimescale(index == m_debugSpeedSettings.Length - 1 ? 1f : 0f, true);
        }

        // 06002dfb: one setting faster, clamped at the first.
        private void OnDebugGameSpeedInc(float amount)
        {
            int index = Mathf.Max(GetDebugSpeedSettingIndex() - 1, 0);
            m_timeManager.OverrideUnityTimescale(m_debugSpeedSettings[index], true);
        }

        // 06002dfc: one setting slower, clamped at the last.
        private void OnDebugGameSpeedDec(float amount)
        {
            int index = Mathf.Min(GetDebugSpeedSettingIndex() + 1, m_debugSpeedSettings.Length - 1);
            m_timeManager.OverrideUnityTimescale(m_debugSpeedSettings[index], true);
        }

        // 06002dfd: the first setting that the current Unity time scale exceeds (with 0.001 slack), else the last.
        private int GetDebugSpeedSettingIndex()
        {
            float timeScale = Time.timeScale;
            for (int i = 0; i < m_debugSpeedSettings.Length; i++)
            {
                if (timeScale > m_debugSpeedSettings[i] - 0.001f) return i;
            }
            return m_debugSpeedSettings.Length - 1;
        }

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void AddDebugButtons() { } // 06002dfe

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private static void RemoveDebugButtons() { } // 06002dff

        // 06002e00: the three debug inputs, all on the down trigger of any joystick.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void AddDebugInput()
        {
            ControlMapping.Subscribe(GameInput.DebugGamePause, OnDebugGamePause, InputTrigger.Down);
            ControlMapping.Subscribe(GameInput.DebugGameSpeedInc, OnDebugGameSpeedInc, InputTrigger.Down);
            ControlMapping.Subscribe(GameInput.DebugGameSpeedDec, OnDebugGameSpeedDec, InputTrigger.Down);
        }

        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void RemoveDebugInput() // 06002e01
        {
            ControlMapping.Unsubscribe(GameInput.DebugGamePause, OnDebugGamePause);
            ControlMapping.Unsubscribe(GameInput.DebugGameSpeedInc, OnDebugGameSpeedInc);
            ControlMapping.Unsubscribe(GameInput.DebugGameSpeedDec, OnDebugGameSpeedDec);
        }

        // 06002e02: the button label is bold while Unity's time scale is overridden to this setting.
        private string TimescaleButtonText(float setting)
        {
            if (m_timeManager.UnityTimescaleOverride && Mathf.Abs(Time.timeScale - setting) < 0.001f)
                return string.Format("**{0}x**", setting);
            return string.Format("{0}x", setting);
        }

        public TimeManagerDebugUtilities() { } // 06002e03: the speed table is allocated before MonoBehaviour.
    }
}
