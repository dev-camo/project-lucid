using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200094b; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class UIContainerUtilities
    {
        private static readonly SystemRef<LevelManager> s_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private static readonly SystemRef<MissionManager> s_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private static readonly SystemRef<App> s_appRef = ProcessManager.GetSystemRef<App>(null, true);

        // 06003575 (1.10.1 RVA 0x681C70): the meta game's selected level and the zone that holds it.
        public static bool TryGetSelectedLevelAndZone(out GameplayLevelDefinition levelDefinition, out GameplayLevelCategory zone)
        {
            levelDefinition = null;
            zone = null;
            if (!s_appRef.Get().Storage.TryGetValue<MetaGameState>(AppFSMKeys.MetaGameState, out MetaGameState metaGameState))
                return false;
            GameLevels gameLevels = s_levelManagerRef.Get().GameLevels;
            levelDefinition = metaGameState.LevelDefinition;
            return gameLevels.TryGetZoneForLevel(levelDefinition, out zone);
        }

        // 06003576 (1.10.1 RVA 0x681DF0): the zone's colours, adjusted by the selected level's theme
        // override and then by the mission's (the active mission's when none is passed).
        public static void SetBackgroundGradientBinders(Bindable<Color> startColour, Bindable<Color> endColour, MissionDefinition missionDefinition = null)
        {
            if (!TryGetSelectedLevelAndZone(out GameplayLevelDefinition levelDefinition, out GameplayLevelCategory zone))
                return;
            Color backgroundStart = zone.BackgroundStartColour;
            Color backgroundEnd = zone.BackgroundEndColour;
            // The theme overrides are tested for plain reference nullness, not Unity liveness.
            levelDefinition.ZoneThemeOverride?.Apply(ref backgroundStart, ref backgroundEnd);
            if (missionDefinition == null)
            {
                IMissionContext missionContext = s_missionManagerRef.Get().ActiveMissionContext;
                if (missionContext != null)
                    missionDefinition = missionContext.MissionDefinition;
            }
            missionDefinition?.ZoneThemeOverride?.Apply(ref backgroundStart, ref backgroundEnd);
            startColour.Value = backgroundStart;
            endColour.Value = backgroundEnd;
        }

        // 06003577 (1.10.1 RVA 0x681FD0): the zone's accent image, replaced by the level's and then
        // the mission's theme override. A missing level or zone is taken from the meta game selection,
        // and without one there is no accent.
        public static ManagedAddressableAsset<Sprite> GetZoneAccent(MissionDefinition missionDefinition = null, GameplayLevelCategory zone = null, GameplayLevelDefinition levelDefinition = null)
        {
            if (levelDefinition == null || zone == null)
            {
                if (!TryGetSelectedLevelAndZone(out GameplayLevelDefinition selectedLevel, out GameplayLevelCategory selectedZone))
                    return null;
                levelDefinition = levelDefinition ?? selectedLevel;
                zone = zone ?? selectedZone;
            }
            ManagedAddressableAsset<Sprite> zoneAccent = zone.ZoneAccent;
            levelDefinition.ZoneThemeOverride?.GetAccentImage(ref zoneAccent);
            if (missionDefinition == null)
            {
                IMissionContext missionContext = s_missionManagerRef.Get().ActiveMissionContext;
                if (missionContext != null)
                    missionDefinition = missionContext.MissionDefinition;
            }
            missionDefinition?.ZoneThemeOverride?.GetAccentImage(ref zoneAccent);
            return zoneAccent;
        }
    }
}
