using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001a9.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioChangeMusicOnCharacterTrigger : HLAudioChangeMusic
    {
        [SerializeField] private CharacterEventTrigger m_characterEventTrigger;

        // The trigger subscription precedes the inherited AudioManager wait.
        protected override void Awake()
        {
            m_characterEventTrigger.OnTriggerEnter += OnCharacterTrigger;
            base.Awake();
        }

        private void OnCharacterTrigger(CharacterEventTrigger _)
        {
            PlayClip();
        }
    }
}
