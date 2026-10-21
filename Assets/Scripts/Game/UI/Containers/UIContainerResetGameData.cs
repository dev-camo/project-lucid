using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200091d; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerResetGameData : UIContainer
    {
        [SerializeField] private UIModernEvent m_onConfirmDeleteUIEvent;

        // 0x06003493 (0x678970): deletes the open save and marks the game as not loaded. The confirm event is sent
        // whether or not a save was open.
        public void Action_DeleteCurrentSave()
        {
            App app = ProcessManager.GetSystem<App>(null, true);
            SaveManager saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            if (saveManager.IsAnySaveOpen)
            {
                int saveIndex = saveManager.CurrentSaveIndex;
                saveManager.CloseSave();
                saveManager.DeleteSave(saveIndex);
                app.Storage.SetValue<bool>(AppFSMKeys.GameSaveLoaded, false);
            }

            app.AddUniqueUIModernEvent(m_onConfirmDeleteUIEvent);
        }

        // 0x06003494 (0x678B20)
        public UIContainerResetGameData()
        {
        }
    }
}
