using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009b7; bodies translated from the 1.10.1 x86-64 binary.
    // Forwards the slider value (and pointer down/up with the slider value) to authored events.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetMusicSeeker : UIWidget, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Slider m_slider;
        [SerializeField] private UnityEvent<float> m_onValueChanged;
        [SerializeField] private UnityEvent<float> m_onInputDown;
        [SerializeField] private UnityEvent<float> m_onInputUp;

        // 0x0600380b (0x69E220)
        public void Action_OnValueChanged(float value)
        {
            m_onValueChanged?.Invoke(value);
        }

        // 0x0600380c (0x69E280): the slider is read only when the event exists.
        public void OnPointerDown(PointerEventData eventData)
        {
            m_onInputDown?.Invoke(m_slider.value);
        }

        // 0x0600380d (0x69E2E0)
        public void OnPointerUp(PointerEventData eventData)
        {
            m_onInputUp?.Invoke(m_slider.value);
        }

        // 0x0600380e (0x69E340)
        public UIWidgetMusicSeeker() { }
    }
}
