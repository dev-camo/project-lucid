using System.Diagnostics;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200096f; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenControlDebugging : MonoBehaviour
    {
        // Nothing in the retail build reads these three labels; any calls that did were
        // [Conditional] and are compiled out, so they are not reconstructed.
        private const string DebugMenuPath = "Input";
        private const string HideInfoLabel = "Hide Touch control info";
        private const string ShowInfoLabel = "Show Touch control info";

        [SerializeField] private UserEditableUIComponentController[] m_touchControls;
        [SerializeField] private GUISkin m_guiSkin;
        private StringBuilder m_stringBuilder = new StringBuilder();
        private bool m_debugActive;

        // 06003651 (1.10.1 RVA 0x68CBB0) is a bare return.
        private void Awake()
        {
        }

        // 06003652 (RVA 0x68CBC0) is a bare return.
        private void OnDestroy()
        {
        }

        // 06003653 (RVA 0x68CBD0): the body ships in retail; only calls are conditional.
        [Conditional("BUILD_DEVELOPMENT")]
        private void OnGUI()
        {
            if (!m_debugActive) return;
            GUISkin previousSkin = GUI.skin;
            GUI.skin = m_guiSkin;
            GUILayout.BeginArea(Screen.safeArea);
            GUILayout.Box(ShowCurrentTouchPositionInfo());
            GUILayout.EndArea();
            GUI.skin = previousSkin;
        }

        // 06003654 (RVA 0x68CDD0)
        private void ToggleDebugText()
        {
            m_debugActive = !m_debugActive;
        }

        // 06003655 (RVA 0x68CD60)
        private string ShowCurrentTouchPositionInfo()
        {
            m_stringBuilder.Length = 0;
            foreach (UserEditableUIComponentController control in m_touchControls)
                control.GetPositionInfo(ref m_stringBuilder);
            return m_stringBuilder.ToString();
        }

        // 06003656 (RVA 0x68CFA0)
        public OnScreenControlDebugging()
        {
        }
    }
}
