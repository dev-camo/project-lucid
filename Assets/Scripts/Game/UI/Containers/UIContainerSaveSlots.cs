using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000920; bodies translated from the 1.10.1 x86-64 binary.
    // The save slot screen: one toggle per stored save, the last used slot preselected.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSaveSlots : UIContainer
    {
        [SerializeField] private UIModernEvent m_selectSaveUIEvent;
        [SerializeField] private UIWidgetToggleSaveData[] m_saveSlotToggles;
        public readonly Bindable<bool> ShowLoading = new Bindable<bool>();
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private readonly SystemRef<UINavigationManager> m_navigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>();
        private readonly SystemRef<ProgressionManager> m_progressionManagerRef = ProcessManager.GetSystemRef<ProgressionManager>();
        private int m_selectedSlotIndex;

        // 0x06003499 (0x678E10)
        private void Awake()
        {
            ShowLoading.Value = false;
            m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
        }

        // 0x0600349a (0x678EC0): every stored save is read from its raw text and set up on the toggle of the same
        // index; the toggle of the slot that is open is selected and navigated to. Choosing a toggle later makes its
        // slot the selected one.
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            m_selectedSlotIndex = saveManager.CurrentSaveIndex;
            ProgressionManager progressionManager = m_progressionManagerRef.Get();
            for (int i = 0; i < saveManager.RawSaves.Length; i++)
            {
                SaveDataGame saveData = JsonUtility.FromJson<SaveDataGame>(saveManager.RawSaves[i]);
                UIWidgetToggleSaveData toggle = m_saveSlotToggles[i];
                int slotIndex = i;
                ProgressionCollectableSave progress = progressionManager.GetSaveProgress(i);
                toggle.Setup(new UIContainerSaveDataViewParameters(saveData, progress, string.Empty, isOn =>
                {
                    if (isOn)
                        SetActiveSaveSlotIndex(slotIndex);
                }));
                if (i == saveManager.CurrentSaveIndex)
                {
                    toggle.Select();
                    TryNavigateToWidget(toggle);
                }
            }
        }

        // 0x0600349b (0x6791B0): the toggle callback's closure body (0x6794A0) is this setter inlined.
        private void SetActiveSaveSlotIndex(int index)
        {
            m_selectedSlotIndex = index;
        }

        // 0x0600349c (0x6791C0): the application is told its start is no longer ready, the chosen slot becomes the
        // last used one, and the select-save event is queued for the UI.
        public void Action_SelectSave()
        {
            ShowLoading.Value = true;
            App app = m_appRef.Get();
            app.Storage.SetValue(AppFSMKeys.StartIsReady, false);
            m_saveManagerRef.Get().SetLastSaveIndex(m_selectedSlotIndex, true);
            app.AddUniqueUIModernEvent(m_selectSaveUIEvent);
        }

        // 0x0600349d (0x679130)
        private void TryNavigateToWidget(UIWidgetToggleSaveData saveDataWidget)
        {
            if (m_navigationManagerRef.IsNull())
                return;
            m_navigationManagerRef.Get().Select(saveDataWidget.WidgetNavigation);
        }

        // 0x0600349e (0x679340)
        public UIContainerSaveSlots() { }
    }
}
