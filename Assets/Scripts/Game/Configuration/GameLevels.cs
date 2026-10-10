using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
    // Private native-derived candidate. Its genuine gameplay field graph is still unresolved.
    [CreateAssetMenu(fileName = "GameLevelConfig", menuName = "HardlightProject/DefinitionData/Groups/Game Level Configuration")]
    public class GameLevels : SystemConfigurationAsset
    {
        public const string SourceFolder = "Scenes";
        public const string ExportFolder = "Scenes Export";
        public const string ExportPostfix = "_exported";

        [SerializeField] protected List<LevelDefinition> m_systemScenes = new List<LevelDefinition>();
        [SerializeField] protected List<LevelDefinition> m_uiScenes = new List<LevelDefinition>();
        [SerializeField] protected List<GameplayLevelUIEntry> m_gameplayEntries = new List<GameplayLevelUIEntry>();
        private IReadOnlyList<GameplayLevelCategory> m_allZones = Array.Empty<GameplayLevelCategory>();
        private IReadOnlyList<GameplayLevelDefinition> m_allLevels = Array.Empty<GameplayLevelDefinition>();
        private readonly Dictionary<GameplayLevelDefinition, GameplayLevelCategory> m_zoneForLevel = new Dictionary<GameplayLevelDefinition, GameplayLevelCategory>();
        private readonly Dictionary<GameplayLevelCategory, IReadOnlyList<GameplayLevelDefinition>> m_levelsPerZone = new Dictionary<GameplayLevelCategory, IReadOnlyList<GameplayLevelDefinition>>();
        [NonSerialized] private bool m_levelsCached;

        // Game.Runtime 0x06001808: the shipped runtime body is RET.
        public override void Validate() { }

        // Game.Runtime 0x06001809: retain earlier dictionary entries on a failed retry;
        // publish the snapshots and cached flag only after compilation and copying finish.
        public void CacheLevels()
        {
            if (m_levelsCached) return;
            var levelsByZone = new Dictionary<GameplayLevelCategory, List<GameplayLevelDefinition>>();
            CompileLevels(m_gameplayEntries, null, ref levelsByZone);
            foreach (var pair in levelsByZone)
            {
                var zone = pair.Key;
                var levels = pair.Value;
                m_levelsPerZone[zone] = levels;
                foreach (var level in levels) m_zoneForLevel.TryAdd(level, zone);
            }
            m_allZones = new List<GameplayLevelCategory>(m_levelsPerZone.Keys);
            m_allLevels = new List<GameplayLevelDefinition>(m_zoneForLevel.Keys);
            m_levelsCached = true;
        }

        // Game.Runtime 0x0600180a: every category supplies a new nearest category;
        // null/other entries are skipped, but a top-level level passes a null dictionary key.
        private static void CompileLevels(IReadOnlyList<GameplayLevelUIEntry> uiEntries,
            GameplayLevelCategory entryCategory,
            ref Dictionary<GameplayLevelCategory, List<GameplayLevelDefinition>> levelsByZone)
        {
            foreach (var entry in uiEntries)
            {
                if (entry is GameplayLevelCategory category)
                {
                    CompileLevels(category.GameplayEntries, category, ref levelsByZone);
                }
                else if (entry is GameplayLevelDefinition level)
                {
                    if (levelsByZone.TryGetValue(entryCategory, out var levels)) levels.AddUnique(level);
                    else levelsByZone[entryCategory] = new List<GameplayLevelDefinition> { level };
                }
            }
        }

        // Game.Runtime 0x0600180b: genuine RET in the supplied player.
        [ContextMenu("Apply build scenes")]
        public virtual void ApplyToBuildSettings() { }
        // Game.Runtime 0x0600180c: genuine RET in the supplied player.
        public virtual void ApplyAllToBuildSettings(bool useExportedScenes) { }
        // Game.Runtime 0x0600180d: genuine RET, including a null supplied list.
        public static void GetScenePath(ILevelDefinition level, ref List<string> scenePaths, bool useExported) { }

        // Game.Runtime 0x0600180e: EndsWith uses the original current-culture overload.
        // Removing an exported postfix uses the definition's Unity object name.
        private static string GetLevelName(ILevelDefinition level, bool useExported)
        {
            string name = level.GetName();
            bool isExported = name.EndsWith(ExportPostfix);
            if (useExported && !isExported) return name + ExportPostfix;
            if (isExported && !useExported) return (level as GameplayLevelDefinition).name;
            return name;
        }

        // Game.Runtime 0x0600180f: genuine RET, without enumeration or list mutation.
        public static void GetScenePaths(IEnumerable<ILevelDefinition> levels, ref List<string> scenePaths, bool useExported) { }

        // Game.Runtime 0x06001810: the shipped player never inspects the GUID array.
        public static int GetSceneIndexFromGUIDs(string[] sceneGUIDs, ILevelDefinition level, bool useExported = false)
        {
            HLOutput.LogError("Couldn't find exact scene asset match for " + level.GetName(), null);
            return -1;
        }

        // Game.Runtime 0x06001811: exact string match on the first cached virtual GetName.
        public GameplayLevelDefinition GetLevel(string sceneName)
        {
            CacheLevels();
            foreach (var level in m_allLevels) if (level.GetName() == sceneName) return level;
            return null;
        }

        // Game.Runtime 0x06001812: return the cached list itself.
        public IReadOnlyList<GameplayLevelDefinition> GetLevels()
        {
            CacheLevels();
            return m_allLevels;
        }

        // Game.Runtime 0x06001813: null keys retain Dictionary's exception behavior.
        public IReadOnlyList<GameplayLevelDefinition> GetLevels(GameplayLevelCategory zone)
        {
            CacheLevels();
            return m_levelsPerZone.TryGetValue(zone, out var levels) ? levels : Array.Empty<GameplayLevelDefinition>();
        }

        // Game.Runtime 0x06001814.
        public IReadOnlyList<GameplayLevelCategory> GetZones()
        {
            CacheLevels();
            return m_allZones;
        }

        // Game.Runtime 0x06001815.
        public bool TryGetZoneForLevel(GameplayLevelDefinition levelDefinition, out GameplayLevelCategory zone)
        {
            CacheLevels();
            return m_zoneForLevel.TryGetValue(levelDefinition, out zone);
        }

        // Game.Runtime 0x06001816: previous spans the complete cached level order;
        // excluded levels do not replace it. Membership and identity use distinct original contracts.
        public bool TryGetPreviousLevel(GameplayLevelDefinition levelDefinition, out GameplayLevelDefinition previousLevel)
        {
            previousLevel = null;
            CacheLevels();
            if (levelDefinition.ExcludeFromLevelOrdering) return false;
            // Native-derived receiver reuse; original C# spelling and fault equivalence remain unresolved.
            var allLevels = m_allLevels;
            if (!allLevels.Contains(levelDefinition)) return false;
            GameplayLevelDefinition previous = null;
            foreach (var level in allLevels)
            {
                if (levelDefinition == level)
                {
                    if (previous == null) return false;
                    previousLevel = previous;
                    return true;
                }
                if (!level.ExcludeFromLevelOrdering) previous = level;
            }
            return false;
        }

        // Game.Runtime 0x06001817: first boss in that category's list.
        public bool TryGetBossLevelForZone(GameplayLevelCategory zone, out GameplayLevelDefinition bossLevel)
        {
            CacheLevels();
            if (m_levelsPerZone.TryGetValue(zone, out var levels))
            {
                foreach (var level in levels)
                {
                    if (level.IsBossLevel)
                    {
                        bossLevel = level;
                        return true;
                    }
                }
            }
            bossLevel = null;
            return false;
        }

        // Game.Runtime 0x06001818: compare each group's direct mission definitions
        // with original ScriptableObjectWithGuid equality, without sub-mission recursion.
        public GameplayLevelDefinition GetFirstLevelForMission(MissionDefinition missionDefinition)
        {
            CacheLevels();
            foreach (var level in m_allLevels)
            {
                var missionList = level.MissionList;
                if (missionList == null) continue;
                foreach (var group in missionList.Groups)
                    foreach (var mission in group.Definitions)
                        if (mission == missionDefinition) return level;
            }
            return null;
        }

        // Game.Runtime 0x06001819: initializers run in original order before the base ctor.
        public GameLevels() { }
    }
}
