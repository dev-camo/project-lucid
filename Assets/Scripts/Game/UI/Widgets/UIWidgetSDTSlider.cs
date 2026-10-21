using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200084f; bodies translated from the 1.10.1 x86-64 binary.
    // A whole-number slider that draws one pip per step and tints the pips up to the current value.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetSDTSlider : UIWidget
    {
        [SerializeField] private Slider m_slider;
        [SerializeField] private UIWidgetSliderPip m_prefabToSpawn;
        [SerializeField] private RectTransform m_pipParent;
        [SerializeField] private Color m_emptyPipColour = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color m_fillPipColour = new Color(1f, 1f, 1f, 1f);
        private readonly List<UIWidgetSliderPip> m_sliderPips = new List<UIWidgetSliderPip>();

        // 0x0600301c (0x60D3D0; iterator 0x02000850, MoveNext 0x60D6F0): only a whole-number slider gets pips. They are
        // laid out two end-of-frame waits after start, spaced across the parent's width, one per step from the minimum.
        private IEnumerator Start()
        {
            if (!m_slider.wholeNumbers)
                yield break;
            WaitForEndOfFrame waitForFrame = new WaitForEndOfFrame();
            yield return waitForFrame;
            yield return waitForFrame;
            m_slider.onValueChanged.AddListener(OnSliderValueChanged);
            float pipSpacing = m_pipParent.rect.width / m_slider.maxValue;
            for (int i = 0; i < m_slider.maxValue + 1f; i++)
            {
                UIWidgetSliderPip pip = Instantiate(m_prefabToSpawn, m_pipParent);
                RectTransform pipTransform = (RectTransform)pip.transform;
                // Single-precision product, as in the native code.
                pipTransform.transform.localPosition = new Vector3((float)(i * pipSpacing) + m_pipParent.rect.x, 0f, 0f);
                m_sliderPips.Add(pip);
            }

            OnSliderValueChanged(m_slider.value);
        }

        // 0x0600301d (0x60D460)
        private void OnDestroy()
        {
            m_slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        // 0x0600301e (0x60D500)
        public void SetValue(float value)
        {
            m_slider.value = value;
        }

        // 0x0600301f (0x60D520)
        public void SetValueWithoutNotify(float value)
        {
            m_slider.SetValueWithoutNotify(value);
            OnSliderValueChanged(value);
        }

        // 0x06003020 (0x60D550): the slider's own value is read for every pip; the argument is not used.
        private void OnSliderValueChanged(float value)
        {
            for (int i = 0; i < m_sliderPips.Count; i++)
            {
                bool filled = m_slider.value >= i;
                m_sliderPips[i].Setup(new UIWidgetSliderPipParameters(filled ? m_fillPipColour : m_emptyPipColour));
            }
        }

        // 0x06003021 (0x60D650)
        public UIWidgetSDTSlider() { }
    }
}
