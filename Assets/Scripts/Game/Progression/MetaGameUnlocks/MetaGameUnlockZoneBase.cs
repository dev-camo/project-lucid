using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class MetaGameUnlockZoneBase : MetaGameUnlockBase
    {
        // Original06002adb: genuine private readonly auto-property and generated getter marker.
        private GameplayLevelCategory Zone { get; }

        // Original06002adc: genuine protected constructor.
        protected MetaGameUnlockZoneBase(GameplayLevelCategory zone)
        {
            Zone = zone;
        }

        // Original06002add/ade: authentic inherited GUID API and direct widget.
        public override string GetId() => Zone.GetGUID();
        public override UIWidgetProgression GetWidget() => Zone.ProgressionUnlockWidget;

        // Original06002adf plus natural06002ae2/ae3: direct invocation deliberately evaluates
        // sprite.texture and faults for a null callback; it differs from Level/MissionGroup.
        public override void GetCustomTexture(Action<Texture> onLoaded)
        {
            Zone.ZoneImageAsset.LoadAsync(sprite => onLoaded(sprite.texture));
        }

        // Original06002ae0.
        public override void ReleaseAssets() => Zone.ZoneImageAsset.Unload();

        // Original06002ae1: request belongs to the captured save game, not SaveManager.
        public override void Save(SaveManager saveManager)
        {
            var saveGame = saveManager.CurrentSave;
            saveGame.AddZoneUnlockSeenGuid(Zone.GetGUID());
            saveGame.RequestSave();
        }
    }
}
