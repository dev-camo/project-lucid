using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001bc.
    [RequireComponent(typeof(Button))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioSfxComponentButton : HLAudioSfxComponent
    {
        [SerializeField] private Button m_button;

        private void OnEnable()
        {
            m_button.onClick.AddListener(PlayOneShotAtSource);
        }

        private void OnDisable()
        {
            m_button.onClick.RemoveListener(PlayOneShotAtSource);
        }

        private void Reset()
        {
            m_button = GetComponent<Button>();
        }
    }
}
