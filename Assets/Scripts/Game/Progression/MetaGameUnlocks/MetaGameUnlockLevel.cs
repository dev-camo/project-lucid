using System;
using Hardlight;
using Hardlight.Analytics;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MetaGameUnlockLevel : MetaGameUnlockBase
    {
        private readonly GameplayLevelDefinition m_levelDefinition;
        private readonly App m_app;

        // Original06002a9f: the App lookup precedes the level assignment, despite field order.
        public MetaGameUnlockLevel(GameplayLevelDefinition levelDefinition)
        {
            m_app = ProcessManager.GetSystem<App>(null, true);
            m_levelDefinition = levelDefinition;
        }

        // Original06002aa0/aa1/aa2: authentic GUID API, key0x9241f15c, and direct widget.
        public override string GetId() => m_levelDefinition.GetGUID();
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.Level;
        public override UIWidgetProgression GetWidget() => m_levelDefinition.ProgressionUnlockWidget;

        // Original06002aa3 plus natural06002aa6/aa7: the loader receives a real callback
        // even when onLoaded is null; the callback checks onLoaded before sprite.texture.
        public override void GetCustomTexture(Action<Texture> onLoaded)
        {
            m_levelDefinition.LoadLevelImage(sprite => onLoaded?.Invoke(sprite.texture));
        }

        // Original06002aa4.
        public override void ReleaseAssets() => m_levelDefinition.UnloadLevelImage();

        // Original06002aa5: retain captured save, each mutation prefix, record save request,
        // App state writes, then play-time lookup before rereading the level for the event.
        public override void Save(SaveManager saveManager)
        {
            var saveGame = saveManager.CurrentSave;
            var levelData = saveGame.GetOrCreateLevelData(m_levelDefinition.GetGUID());
            saveGame.SetLastLevelSelected(m_levelDefinition);
            levelData.UnlockSeen = true;
            levelData.RequestSave();
            m_app.SetLastLevelUISelectedGUID(m_levelDefinition, false);
            m_app.Storage.SetValue(AppFSMKeys.LevelUnlocked, true);
            long playTimeMs = saveGame.GetOrCreatePlayerStatData(SaveDataPlayerStat.Type.TotalPlayTimeMS).PlayerStatCounter;
            AnalyticsEventCollector.ZoneUnlockedEvent(m_levelDefinition, playTimeMs);
        }
    }
}
