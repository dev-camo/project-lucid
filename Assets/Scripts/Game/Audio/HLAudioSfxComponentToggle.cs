using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001bd.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(UIToggle))]
    public class HLAudioSfxComponentToggle : HLAudioSfxComponent
    {
        [SerializeField] private UIToggle m_toggle;
        [HashEnum]
        [SerializeField]
        private HLAudioClipIdentifier m_offAudioClipIdentifier;

        private void OnEnable()
        {
            m_toggle.OnValueChanged.AddListener(PlayOneShotAtSource);
        }

        private void OnDisable()
        {
            m_toggle.OnValueChanged.RemoveListener(PlayOneShotAtSource);
        }

        // Switching off is silent unless an off clip is set.
        private void PlayOneShotAtSource(bool value)
        {
            if (value) PlayOneShotAtSource();
            else if (m_offAudioClipIdentifier != 0) PlayOneShotAtSource(m_offAudioClipIdentifier);
        }

        private void Reset()
        {
            m_toggle = GetComponent<UIToggle>();
        }
    }
}
