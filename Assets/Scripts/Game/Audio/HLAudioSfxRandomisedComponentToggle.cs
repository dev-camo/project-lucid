using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001bf. Only switching on plays a clip.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioSfxRandomisedComponentToggle : HLAudioSfxRandomisedComponent
    {
        [SerializeField] private UIToggle m_toggle;

        private void OnEnable()
        {
            m_toggle.OnValueChanged.AddListener(PlayOneShotAtSource);
        }

        private void OnDisable()
        {
            m_toggle.OnValueChanged.RemoveListener(PlayOneShotAtSource);
        }

        private void PlayOneShotAtSource(bool value)
        {
            if (value) PlayOneShotAtSource();
        }

        private void Reset()
        {
            m_toggle = GetComponent<UIToggle>();
        }
    }
}
