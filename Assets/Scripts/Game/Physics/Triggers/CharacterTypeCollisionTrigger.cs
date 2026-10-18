using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000763. An event trigger that only reacts to the current
    // character's collision collider, optionally restricted to a list of character ids.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterTypeCollisionTrigger : EventCollisionTrigger
    {
        [SerializeField]
        private bool m_anyCharacter;
        [SerializeField]
        private List<CharacterId> m_characters = new List<CharacterId>();
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);

        // 06002952; the manager is only consulted when it is already cached, so no manager
        // means no match.
        protected override bool FilterCollision(Collider other)
        {
            CharacterManager manager = m_characterManagerRef.GetSafe();
            if (manager == null || !manager.TryGetCurrentCharacter(out Character character))
                return false;
            if (other != character.ColliderCollision)
                return false;
            return m_anyCharacter || m_characters.Contains(character.IdType);
        }

        // 06002953; the native body only queries CanValidate and keeps no result.
        private void OnValidate() => gameObject.CanValidate();

        // 06002954
        public CharacterTypeCollisionTrigger() { }
    }
}
