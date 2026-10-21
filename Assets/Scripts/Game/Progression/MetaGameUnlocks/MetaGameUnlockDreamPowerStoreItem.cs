using System;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000796, all thirteen authored APIs and one field. Unlike
    // its siblings it implements IMetaGameUnlock directly, not through MetaGameUnlockBase.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MetaGameUnlockDreamPowerStoreItem : IMetaGameUnlock
    {
        public readonly DreamPowerDefinition Definition;

        // 0x06002a92: object constructor first, then the definition.
        public MetaGameUnlockDreamPowerStoreItem(DreamPowerDefinition definition)
        {
            Definition = definition;
        }

        public string GetId() => Definition.GetGUID(); // 0x06002a93

        // 0x06002a94: original key 0x11e244f7.
        public PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.DreamPowerStoreItem;

        public UIWidgetProgression GetWidget() => null; // 0x06002a95
        public Strings GetTitle() => Definition.Name; // 0x06002a96
        public Strings GetBody() => Definition.Description; // 0x06002a97
        public HLAudioClipIdentifier GetAudio() => HLAudioClipIdentifier.None; // 0x06002a98

        // 0x06002a99: authentic empty body.
        public void Save(SaveManager saveManager) { }

        // 0x06002a9a: the icon is loaded synchronously and its texture passed on; the
        // callback has no null guard.
        public void GetCustomTexture(Action<Texture> onLoaded)
        {
            onLoaded(Definition.IconAsset.Load().texture);
        }

        // 0x06002a9b: the icon is loaded synchronously and passed on; no null guard.
        public void GetCustomSprite(Action<Sprite> onLoaded)
        {
            onLoaded(Definition.IconAsset.Load());
        }

        public void ReleaseAssets() => Definition.IconAsset.Unload(); // 0x06002a9c
        public bool CanExitToMenu() => false; // 0x06002a9d
        public bool UsesAlternativeExitToMenuEvent() => false; // 0x06002a9e
    }
}
