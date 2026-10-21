using System;
using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009a8; bodies translated from the 1.10.1 x86-64 binary.
    // A slider that eases towards a value, finishing the previous animation's callback first.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetChallengesSlider : UIWidget
    {
        [SerializeField] private Slider m_slider;
        [SerializeField] private float m_animationDuration = 0.5f;
        private UIWidgetChallengesSliderParameters m_parameters;
        private Action m_onCompleteCurrentAnimation;

        // 0x060037d4 (0x69C220)
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetChallengesSliderParameters>();
            m_slider.maxValue = m_parameters.MaxValue;
            m_slider.value = m_parameters.Value;
            ((RectTransform)m_slider.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_parameters.Size);
        }

        // 0x060037d5 (0x69C300)
        public Vector2 GetAnchoredPosition()
        {
            return ((RectTransform)transform).anchoredPosition;
        }

        // 0x060037d6 (0x69C360): an animation still running is stopped and its callback fired.
        public void SetValue(float value, Action onComplete)
        {
            StopAllCoroutines();
            if (m_onCompleteCurrentAnimation != null)
                m_onCompleteCurrentAnimation();
            m_onCompleteCurrentAnimation = onComplete;
            StartCoroutine(AnimateSlider(value));
        }

        // 0x060037d7 (0x69C420, iterator MoveNext 0x69C4F0): each frame the slider moves a smoothstep
        // fraction of the way from its current value to the target.
        private IEnumerator AnimateSlider(float target)
        {
            float startTime = Time.time;
            while (target > m_slider.value)
            {
                yield return null;
                float t = (Time.time - startTime) / m_animationDuration;
                m_slider.value = Mathf.SmoothStep(m_slider.value, target, t);
            }
            if (m_onCompleteCurrentAnimation != null)
                m_onCompleteCurrentAnimation();
            m_onCompleteCurrentAnimation = null;
        }

        // 0x060037d8
        public UIWidgetChallengesSlider() { }
    }
}
