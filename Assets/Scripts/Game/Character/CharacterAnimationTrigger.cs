using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200076b. Plays an actor animation for characters entering the volume.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAnimationTrigger : ActorAnimationTrigger<Character>
    {
        [Tooltip("If true, ignores any actor collisions that aren't the current main character.")]
        [SerializeField]
        private bool m_currentCharacterOnly;
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);

        // 060029b9; with the filter on, the actor must be the manager's current character
        // (compared with Unity's overloaded equality).
        protected override bool FilterActor(Character actor)
        {
            if (!m_currentCharacterOnly)
                return true;
            if (!m_characterManagerRef.IsValid())
                return false;
            return m_characterManagerRef.Get().TryGetCurrentCharacter(out Character current) && current == actor;
        }

        // 060029ba
        public CharacterAnimationTrigger() { }
    }
}
