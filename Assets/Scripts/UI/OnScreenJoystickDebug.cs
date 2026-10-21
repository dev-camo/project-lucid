using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 02000978; bodies translated from the 1.10.1 x86-64 binary.
    // The Conditional("BUILD_DEVELOPMENT") members are declared as shipped; retail callers of the ones that
    // are compiled out are not restored.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenJoystickDebug : MonoBehaviour
    {
        [SerializeField] private Image m_verticalDeadZoneInner;
        [SerializeField] private Image m_horizontalDeadZoneInner;
        [SerializeField] private Image m_liveZone;
        private bool m_inputTypeIsTouch;
        private bool m_characterInfoShowing;

        // 060036a3 (1.10.1 RVA 0x691E60)
        [Conditional("BUILD_DEVELOPMENT")]
        protected void OnEnable()
        {
            DebugMenu.OnCharacterInfoToggle += ToggleCharacterInfoShowing;
        }

        // 060036a4 (RVA 0x691FA0)
        private void ToggleCharacterInfoShowing(bool isShowing)
        {
            m_characterInfoShowing = isShowing;
        }

        // 060036a5 (RVA 0x691FB0)
        [Conditional("BUILD_DEVELOPMENT")]
        public void ToggleIsInputTouch(bool isTouch)
        {
            m_inputTypeIsTouch = isTouch;
        }

        // 060036a6 (RVA 0x691FC0): the dead-zone images show only for touch input while the character info is up.
        [Conditional("BUILD_DEVELOPMENT")]
        private void ToggleDeadZones()
        {
            bool showDeadZones = m_inputTypeIsTouch && m_characterInfoShowing;
            m_verticalDeadZoneInner.enabled = showDeadZones;
            m_horizontalDeadZoneInner.enabled = showDeadZones;
            m_liveZone.enabled = showDeadZones;
        }

        // 060036a7 (RVA 0x692010)
        [Conditional("BUILD_DEVELOPMENT")]
        protected void OnDisable()
        {
            DebugMenu.OnCharacterInfoToggle -= ToggleCharacterInfoShowing;
        }

        // 060036a8 (RVA 0x692150)
        public OnScreenJoystickDebug()
        {
        }
    }
}
