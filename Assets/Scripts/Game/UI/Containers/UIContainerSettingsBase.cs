using System;
using Hardlight;
using Hardlight.Analytics;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200092a; bodies translated from the 1.10.1 x86-64 binary.
    // The shared part of the settings pages: every edit runs a callback on the live settings, saves them, lets the
    // page react, then refreshes its bindings.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UIContainerSettingsBase : UIContainer
    {
        protected readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);

        // 0x060034be: shared generic (RVA -1); the bool, Int32Enum and fully shared bodies are 0x9DB980, 0x9DBA90 and
        // 0x9DBBC0. Compares through the boxed values' Equals and reports only a real change.
        protected void SendAnalyticsEventOnSettingChanged<TSettingType>(string settingName, TSettingType currentSetting, TSettingType newSetting)
        {
            if (!currentSetting.Equals(newSetting))
                AnalyticsEventCollector.SettingChangedEvent(settingName, currentSetting.ToString(), newSetting.ToString());
        }

        // 0x060034bf (0x679940): refreshing the bindings is the last step whether or not the settings were saved.
        protected void SetSaveData(Action<SaveDataSettings> callback, bool saveImmediately = true)
        {
            SaveManager saveManager = m_saveManagerRef.Get();
            callback(saveManager.GetSaveDataSettings());
            if (saveImmediately)
            {
                saveManager.RequestSaveSettings();
                DoOnAfterSave();
            }

            RefreshBinders();
        }

        // 0x060034c0 (0x67A760): the page resets the live settings first; the save path then refreshes the bindings
        // once from its callback and once more at its end.
        public void Action_ResetToDefaults()
        {
            ResetToDefaults(m_saveManagerRef.Get().GetSaveDataSettings());
            SetSaveData(_ => RefreshBinders());
        }

        // 0x060034c1, 0x060034c2: abstract slots 7 and 8.
        protected abstract void ResetToDefaults(SaveDataSettings saveDataSettings);

        protected abstract void RefreshBinders();

        // 0x060034c3 (0x67A1D0): slot 9, empty.
        protected virtual void DoOnAfterSave() { }

        // 0x060034c4 (0x67A4A0)
        protected UIContainerSettingsBase() { }
    }
}
