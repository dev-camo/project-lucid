using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200084e; bodies translated from the 1.10.1 x86-64 binary.
    // A loading bar whose fill is the right anchor of a rect, shaped by an animation curve.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetSDTLoadingBar : UIWidget
    {
        [SerializeField] private RectTransform m_loadingBarUI;
        [SerializeField] private AnimationCurve m_curve;

        // 0x0600301a: the curve maps the progress to the horizontal anchor; the vertical stays 1.
        public void UpdateLoadingProgress(float progress)
        {
            m_loadingBarUI.anchorMax = new Vector2(m_curve.Evaluate(progress), 1f);
        }

        // 0x0600301b
        public UIWidgetSDTLoadingBar() { }
    }
}
