using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000340. Hands the current character's transform
    // (rather than the colliding one) to the assign events, and always clears it on exit.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterProxyAssignTrigger : TransformAssignEventTrigger
    {
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);

        // 06001460
        protected override bool SetNullOnExit => true;

        // 06001461
        protected override Transform GetTargetTransform(Collider other)
        {
            if (m_characterManagerRef.TryGet(out CharacterManager characterManager) &&
                characterManager.TryGetCurrentCharacter(out Character character))
                return character.transform;
            return null;
        }

        // 06001462; the manager reference is resolved before the base constructor.
        public CharacterProxyAssignTrigger() { }
    }
}
