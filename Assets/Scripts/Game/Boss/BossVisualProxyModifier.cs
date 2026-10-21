using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020009e8; 0x6A6E90..0x6A7090. Each action resolves the boss
    // for the authored definition and applies one inherited modifier.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class BossVisualProxyModifier : ActorVisualProxyModifier
    {
        [SerializeField] private BossDefinition m_bossDefinition;
        private readonly SystemRef<BossManager> m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);

        public void Action_ShowVisualProxy() => ShowVisualProxy(m_bossManagerRef.Get().GetBossByDefinition(m_bossDefinition)); // 060038ef
        public void Action_HideVisualProxy() => HideVisualProxy(m_bossManagerRef.Get().GetBossByDefinition(m_bossDefinition)); // 060038f0
        public void Action_DisableVisualProxy() => DisableVisualProxy(m_bossManagerRef.Get().GetBossByDefinition(m_bossDefinition)); // 060038f1
        public void Action_EnableVisualProxy() => EnableVisualProxy(m_bossManagerRef.Get().GetBossByDefinition(m_bossDefinition)); // 060038f2
    }
}
