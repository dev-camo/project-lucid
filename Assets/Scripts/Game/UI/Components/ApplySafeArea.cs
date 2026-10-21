using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200082e; bodies 0x606BD0..0x606DB0 (1.10.1 x86-64).
    // Insets the RectTransform by the device safe area.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplySafeArea : MonoBehaviour
    {
        [Tooltip("If true, safe area is applied automatically on Awake. Otherwise, Apply action should be called manually.")]
        [SerializeField]
        private bool m_applyOnAwake = true;

        // 06002f7e: native repeats the Apply body inline.
        private void Awake()
        {
            if (m_applyOnAwake)
            {
                Apply();
            }
        }

        // 06002f7f
        public void Apply()
        {
            RectTransform rectTransform = transform as RectTransform;
            rectTransform.offsetMin = new Vector2(Screen.safeArea.x, Screen.safeArea.y);
            rectTransform.offsetMax = new Vector2(
                Screen.safeArea.xMax - Screen.width,
                Screen.safeArea.yMax - Screen.height);
        }
    }
}
