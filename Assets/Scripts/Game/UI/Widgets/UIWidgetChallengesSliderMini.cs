using System.Collections;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009ab; bodies translated from the 1.10.1 x86-64 binary.
    // The small experience bar: set to its value, and refilled from empty by AnimateNewReward.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetChallengesSliderMini : UIWidget
    {
        [SerializeField] private Slider m_slider;
        [SerializeField] private float m_animationTimeSeconds = 10f;
        private UIWidgetChallengesSliderMiniParameters m_parameters;

        // 0x060037e0 (0x69C710)
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetChallengesSliderMiniParameters>();
            m_slider.maxValue = m_parameters.MaxValue;
            m_slider.value = m_parameters.Value;
        }

        // 0x060037e1 (0x69C7A0)
        public void AnimateNewReward()
        {
            CoroutineUtils.RunCoroutine(Animate());
        }

        // 0x060037e2 (0x69C810, iterator MoveNext 0x69C8D0): empties the bar, then eases it back
        // to the parameters' value over the animation time.
        private IEnumerator Animate()
        {
            m_slider.value = 0f;
            float timeElapsed = 0f;
            while (m_animationTimeSeconds > timeElapsed)
            {
                m_slider.value = Mathf.Lerp(m_slider.value, m_parameters.Value, timeElapsed / m_animationTimeSeconds);
                timeElapsed += Time.deltaTime;
                yield return null;
            }
        }

        // 0x060037e3
        public UIWidgetChallengesSliderMini() { }
    }
}
