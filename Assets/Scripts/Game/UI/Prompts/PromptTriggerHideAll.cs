using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200098d.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PromptTriggerHideAll : MonoBehaviour
    {
        private readonly SystemRef<PromptManager> m_promptManagerRef =
            ProcessManager.GetSystemRef<PromptManager>(null, true);

        // 0x06003741: validity is tested before the manager is fetched; the
        // native body inlines PromptManager.HideAllActivePrompts.
        public void Action_HideAllActivePrompts()
        {
            if (m_promptManagerRef.IsValid())
                m_promptManagerRef.Get().HideAllActivePrompts();
        }

        // 0x06003742.
        public PromptTriggerHideAll() { }
    }
}
