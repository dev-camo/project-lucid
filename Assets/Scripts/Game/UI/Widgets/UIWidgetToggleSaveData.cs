using System;
using System.Collections.Generic;
using System.Globalization;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009db; bodies translated from the 1.10.1 x86-64 binary.
    // One save slot on the save slot screen: orb and ornament counts, last save time and play time.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetToggleSaveData : UIWidgetToggle
    {
        [SerializeField]
        [HashEnum(typeof(Strings))]
        private Strings m_playTimeStringId;

        public readonly Bindable<int> NumberOfOrbs = new Bindable<int>();
        public readonly Bindable<int> NumberOfOrnaments = new Bindable<int>();
        public readonly Bindable<string> LastedUpdatedTimestamp = new Bindable<string>();
        public readonly Bindable<string> PlayTimeTimestamp = new Bindable<string>();
        public readonly Bindable<bool> SlotEmpty = new Bindable<bool>();

        // 0x060038bd (0x6A5380): a slot without a save is only marked empty. Otherwise the saved time is the stored
        // value read as Unix milliseconds in local time, and the play time is the total play time statistic formatted
        // into the localised play time string.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            UIContainerSaveDataViewParameters data = parameters.GetAs<UIContainerSaveDataViewParameters>();
            SaveDataGame saveData = data.SaveData;
            if (saveData == null || saveData.IsSaveEmpty())
            {
                SlotEmpty.Value = true;
                return;
            }

            SlotEmpty.Value = false;
            NumberOfOrbs.Value = data.Progress.Total.GetValueOrDefault(CollectableType.Orb).Collected;
            NumberOfOrnaments.Value = saveData.CollectedOrnamentsCount();
            DateTime lastSaved = TimeUtils.FromUnixTime(saveData.LastSavedTimestampMs);
            LastedUpdatedTimestamp.Value = lastSaved.ToLocalTime().ToString(CultureInfo.CurrentCulture);
            string playTimeFormat = StringTable.GetString(m_playTimeStringId);
            TimeSpan playTime = TimeSpan.FromMilliseconds(saveData.GetOrCreatePlayerStatData(SaveDataPlayerStat.Type.TotalPlayTimeMS).PlayerStatCounter);
            PlayTimeTimestamp.Value = string.Format(playTimeFormat, playTime.ToString(@"hh\:mm\:ss"));
        }

        // 0x060038be (0x6A5620)
        public void Select()
        {
            m_toggle.IsOn = true;
        }

        // 0x060038bf (0x6A5640)
        public UIWidgetToggleSaveData() { }
    }
}
