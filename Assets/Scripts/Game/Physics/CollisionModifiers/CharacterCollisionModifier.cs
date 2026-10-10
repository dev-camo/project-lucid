using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000742: complete three-method API.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterCollisionModifier : MonoBehaviour
    {
        // Both native constructors initialize this before MonoBehaviour's constructor.
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);

        // 0x060028db. Publish null before resolving the real manager.
        protected bool TryGetCharacter(out Character character)
        {
            character = null;
            return m_characterManagerRef.TryGet(out CharacterManager manager) &&
                manager.TryGetCurrentCharacter(out character);
        }

        // 0x060028dc. This overload publishes null after failed provider checks.
        protected bool TryGetCharacter(Collider other, out Character character)
        {
            if (m_characterManagerRef.TryGet(out CharacterManager manager) &&
                manager.IsCurrentCharacterColliderCollision(other))
                return manager.TryGetCurrentCharacter(out character);
            character = null;
            return false;
        }

        // 0x060028dd. The provider call remains a field initializer above.
        protected CharacterCollisionModifier() { }
    }
}
