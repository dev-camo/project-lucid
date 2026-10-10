using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MetaGameUnlockMissionGroup : MetaGameUnlockBase
    {
        // Original06002aa8/aa9: two genuine private readonly auto-properties, in original order.
        private GameplayLevelDefinition m_levelDefinition { get; }
        private MissionGroup m_missionGroup { get; }

        // Original06002aaa: assignment order is group first, then level.
        public MetaGameUnlockMissionGroup(GameplayLevelDefinition levelDefinition, MissionGroup missionGroup)
        {
            m_missionGroup = missionGroup;
            m_levelDefinition = levelDefinition;
        }

        // Original06002aab/aac/aad: MissionGroup has its own GUID property; key0x736a2e3e.
        public override string GetId() => m_missionGroup.GUID;
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.MissionGroup;
        public override UIWidgetProgression GetWidget() => m_missionGroup.ProgressionUnlockWidget;

        // Original06002aae plus natural06002ab2/ab3: authentic parameter spelling image;
        // null callback returns before reading image.texture, while the loader still runs.
        public override void GetCustomTexture(Action<Texture> onLoaded)
        {
            m_levelDefinition.LoadLevelImage(image => onLoaded?.Invoke(image.texture));
        }

        // Original06002aaf.
        public override void ReleaseAssets() => m_levelDefinition.UnloadLevelImage();

        // Original06002ab0: the saved level is obtained before reading the group GUID.
        public override void Save(SaveManager saveManager)
        {
            saveManager.CurrentSave.GetOrCreateLevelData(m_levelDefinition.GetGUID()).AddMissionGroupUnlockSeen(m_missionGroup.GUID);
            saveManager.RequestSave();
        }

        // Original06002ab1: nonvirtual direct return.
        public GameplayLevelDefinition GetLevel() => m_levelDefinition;
    }
}
