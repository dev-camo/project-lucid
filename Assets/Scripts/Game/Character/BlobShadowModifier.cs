using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009e6: event targets that toggle the current
    // character's blob shadow without a validity check.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class BlobShadowModifier : MonoBehaviour
    {
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        public void Action_ShowBlobShadow()
        {
            m_characterManagerRef.Get().GetCurrentCharacterUnsafe().ToggleBlobShadow(true);
        }

        public void Action_HideBlobShadow()
        {
            m_characterManagerRef.Get().GetCurrentCharacterUnsafe().ToggleBlobShadow(false);
        }
    }
}
