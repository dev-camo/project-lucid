using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000794, all six authored APIs and one field.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MetaGameUnlockDreamPowerStore : MetaGameUnlockBase
    {
        private readonly DreamPowerStoreDefinition m_storeDefinition;

        // 0x06002a84: base first, then the definition.
        public MetaGameUnlockDreamPowerStore(DreamPowerStoreDefinition storeDefinition)
        {
            m_storeDefinition = storeDefinition;
        }

        public override string GetId() => m_storeDefinition.GetGUID(); // 0x06002a85

        // 0x06002a86: original key 0xc6fe4e40.
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.DreamPowerStore;

        public override UIWidgetProgression GetWidget() => m_storeDefinition.ProgressionUnlockWidget; // 0x06002a87

        // 0x06002a88: flag the store as new (value 0xb7834d4b) and request a save.
        public override void Save(SaveManager saveManager)
        {
            saveManager.CurrentSave.DreamPowerStore.IsNewState = DreamPowerStoreIsNewState.IsNew;
            saveManager.RequestSave();
        }

        // 0x06002a89: the callback is invoked at once with null, with no null guard and
        // without loading the store image.
        public override void GetCustomTexture(Action<Texture> onLoaded)
        {
            onLoaded(null);
        }
    }
}
