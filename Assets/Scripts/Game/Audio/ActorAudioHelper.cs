using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000050. No shipped body reads or writes m_lookup.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorAudioHelper : MonoBehaviour
    {
        [SerializeField] private ActorAnimationDefinition m_soundToTrigger;
#pragma warning disable CS0169
        private ActorAudioLookup m_lookup;
#pragma warning restore CS0169
        private CharacterManager m_characterManager;

        private void Start()
        {
            m_characterManager = ProcessManager.GetSystem<CharacterManager>();
        }

        public void Action_PlaySound()
        {
            if (m_characterManager.TryGetCurrentCharacter(out Character character))
                character.Audio.ProcessOnEnterActions(m_soundToTrigger);
        }
    }
}
