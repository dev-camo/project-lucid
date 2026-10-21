using System;
using System.Collections.Generic;
using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only settings page: the abstract UIContainerSettingsBase has no instantiable original, so this page
    // logs the three hooks the base calls and hands its protected edit helpers to the cases. Each hook entry also
    // records whether the live settings still had unsaved changes at that moment.
    public sealed class SettingsContainerBaseProbe : UIContainerSettingsBase
    {
        public readonly List<string> Log = new List<string>();
        public readonly List<SaveDataSettings> ResetArguments = new List<SaveDataSettings>();
        // What the reset hook does to the settings it is given.
        public Action<SaveDataSettings> OnReset = _ => { };

        public void Edit(Action<SaveDataSettings> callback) => SetSaveData(callback);

        public void Edit(Action<SaveDataSettings> callback, bool saveImmediately) => SetSaveData(callback, saveImmediately);

        public void Report<T>(string settingName, T currentSetting, T newSetting) =>
            SendAnalyticsEventOnSettingChanged(settingName, currentSetting, newSetting);

        public void BaseDoOnAfterSave() => base.DoOnAfterSave();

        protected override void ResetToDefaults(SaveDataSettings saveDataSettings)
        {
            Log.Add("reset");
            ResetArguments.Add(saveDataSettings);
            OnReset(saveDataSettings);
        }

        protected override void RefreshBinders() => Log.Add("refresh");

        protected override void DoOnAfterSave() =>
            Log.Add("after-save:" + (m_saveManagerRef.Get().GetSaveDataSettings().HasChangesToSave() ? "unsaved" : "saved"));
    }
}
