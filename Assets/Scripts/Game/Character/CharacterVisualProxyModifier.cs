using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 020009ed. Event-callable visual proxy switches for the current
    // character; the shared helpers live in ActorVisualProxyModifier.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class CharacterVisualProxyModifier : ActorVisualProxyModifier
    {
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);

        // 06003912
        public void Action_ShowVisualProxy() => ShowVisualProxy(m_characterManagerRef.Get().GetCurrentCharacterUnsafe());

        // 06003913
        public void Action_HideVisualProxy() => HideVisualProxy(m_characterManagerRef.Get().GetCurrentCharacterUnsafe());

        // 06003914
        public void Action_DisableVisualProxy() => DisableVisualProxy(m_characterManagerRef.Get().GetCurrentCharacterUnsafe());

        // 06003915
        public void Action_EnableVisualProxy() => EnableVisualProxy(m_characterManagerRef.Get().GetCurrentCharacterUnsafe());

        // 06003916
        public CharacterVisualProxyModifier() { }
    }
}
