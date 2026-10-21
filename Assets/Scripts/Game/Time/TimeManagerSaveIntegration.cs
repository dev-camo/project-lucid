using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeManagerSaveIntegration (TypeDef 0x020007f3, x86-64 0x5F8010..0x5F844F):
    // feeds the saved game-speed setting to the TimeManager once the save loads and after every save.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimeManagerSaveIntegration : MonoBehaviour
    {
        [SerializeField] private TimeManager m_timeManager;
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();

        // 06002e04, with the closures <Start>b__2_0 (06002e08) and <Start>b__2_1 (06002e09).
        private void Start()
        {
            m_saveManagerRef.InvokeOnValid(saveManager => saveManager.OnLoadCompleted += OnSaveGameLoaded);
            m_saveManagerRef.InvokeOnValid(saveManager => saveManager.OnSaveCompleted += OnGameSaved);
        }

        // 06002e05: a one-shot, so the handler removes itself before applying the setting.
        private void OnSaveGameLoaded()
        {
            SaveManager saveManager = m_saveManagerRef.Get();
            saveManager.OnLoadCompleted -= OnSaveGameLoaded;
            m_timeManager.InitialiseGameSpeedOverride();
            m_timeManager.UpdateGameSpeedOverride(saveManager.GetSaveDataSettings().GameSpeed);
        }

        // 06002e06
        private void OnGameSaved()
        {
            m_timeManager.UpdateGameSpeedOverride(m_saveManagerRef.Get().GetSaveDataSettings().GameSpeed);
        }

        public TimeManagerSaveIntegration() { } // 06002e07
    }
}
