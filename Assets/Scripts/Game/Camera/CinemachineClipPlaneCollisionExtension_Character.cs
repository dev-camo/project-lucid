using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000272.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineClipPlaneCollisionExtension_Character : CinemachineClipPlaneCollisionExtension
    {
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        // 0x06000db7: the current character, else the virtual camera's own look-at target.
        protected override Transform GetLookAtTransform()
        {
            if (m_characterManagerRef.IsNull()) return VirtualCamera.LookAt;
            if (m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                return character.transform;
            return VirtualCamera.LookAt;
        }

        // 0x06000db8
        public CinemachineClipPlaneCollisionExtension_Character() { }
    }
}
