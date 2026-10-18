using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 020009ec. Event-callable switches for the current character's UI.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class CharacterUiModifier : MonoBehaviour
    {
        private SystemRef<CharacterManager> m_characterManagerRef;

        // 0600390e
        private void Start() => m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);

        // 0600390f
        public void Action_ShowUI() => m_characterManagerRef.Get().GetCurrentCharacterUnsafe().ToggleUI(true);

        // 06003910
        public void Action_HideUI() => m_characterManagerRef.Get().GetCurrentCharacterUnsafe().ToggleUI(false);

        // 06003911
        public CharacterUiModifier() { }
    }
}
