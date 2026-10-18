using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006b4; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class AutoRegisterHashedComponentGroup : HashedComponentGroup
    {
        private readonly SystemRef<GameplayIslandManager> m_gameplayIslandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>();
        private List<GameplaySection> m_registeredGameplaySections;

        // 0x060023fe
        private void OnEnable()
        {
            m_gameplayIslandManagerRef.InvokeOnValid(OnGameplayIslandValid);
        }

        // 0x060023ff: the section list is kept, so a later enable replaces it.
        private void OnDisable()
        {
            if (m_registeredGameplaySections == null) return;
            foreach (GameplaySection gameplaySection in m_registeredGameplaySections)
                gameplaySection.UnRegisterComponentGroup(this);
        }

        // 0x06002400: the lookup's result is ignored; its out list is always iterated.
        private void OnGameplayIslandValid(GameplayIslandManager gameplayIslandManager)
        {
            gameplayIslandManager.TryGetAllGameplaySections(transform.position, out m_registeredGameplaySections);
            foreach (GameplaySection gameplaySection in m_registeredGameplaySections)
                gameplaySection.RegisterComponentGroup(this);
        }
    }
}
