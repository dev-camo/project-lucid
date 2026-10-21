using Hardlight;
using Hardlight.Analytics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020007d4.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveGameUIActions : MonoBehaviour
    {
        // 0x06002d0a (x86-64 0x5ece80): the open save is closed, every slot is
        // replaced (SaveManager.DeleteAllSaves, inlined) and save slot 0 is then
        // reopened on this component (constant argument 0 in the iterator at
        // +0x28), whichever slot was open before. The reopen is started whether or not
        // the delete guard held.
        public void Action_DeleteAllSaves()
        {
            SaveManager saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            bool wasSaveOpen = saveManager.IsAnySaveOpen;
            if (wasSaveOpen) saveManager.CloseSave();
            saveManager.DeleteAllSaves();
            if (wasSaveOpen) StartCoroutine(saveManager.OpenSave(0));
        }

        // 0x06002d0b (x86-64 0x5ed410): analytics events are cleared first; the
        // analytics save is rebuilt (SaveManager.DeleteSaveAnalytics, inlined)
        // only when it exists, and both consumers then reload from it.
        public void Action_ResetAnalyticsData()
        {
            Analytics.ClearAnalyticsEventsCached();
            SaveManager saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            if (!saveManager.HasSaveDataAnalytics()) return;
            saveManager.DeleteSaveAnalytics();
            ProcessManager.GetSystem<AnalyticsConsentManager>(null, true).LoadFromSaveData(saveManager);
            ProcessManager.GetSystem<AnalyticsSessionManager>(null, true).LoadFromSaveData(saveManager);
        }

        // 0x06002d0c.
        public SaveGameUIActions() { }
    }
}
