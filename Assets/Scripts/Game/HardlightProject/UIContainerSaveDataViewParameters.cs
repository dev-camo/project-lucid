using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009dc; bodies translated from the 1.10.1 x86-64 binary.
    // What one save slot toggle shows: the slot's save and the progression totals cached for it.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSaveDataViewParameters : UIWidgetToggleParameters
    {
        public SaveDataGame SaveData { get; }

        public ProgressionCollectableSave Progress { get; }

        // 0x060038c2 (0x6A57D0): the base constructor takes the string label overload.
        public UIContainerSaveDataViewParameters(SaveDataGame saveData, ProgressionCollectableSave progress, string label, Action<bool> callback)
            : base(label, callback)
        {
            SaveData = saveData;
            Progress = progress;
        }
    }
}
