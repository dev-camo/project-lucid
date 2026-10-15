using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned in-memory level graphs over the genuine GameLevels and
    // GameLevelsWithLinkedTests bodies. Categories acquire a LevelManager
    // SystemRef in their field initializer, so that registry row is owned.
    public sealed class OriginalGameLevelsGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // GameLevels.CacheLevels 0x06001809, x86-64 0x796fb0. Its flag test at
        // +0x60 0x797105 precedes CompileLevels 0x79714b; per-zone set_Item on
        // +0x58 0x797233 precedes TryAdd on +0x50 0x7972a2 for each level; only
        // after both loops are m_allZones (+0x40) and m_allLevels (+0x48) copied
        // from the dictionary keys and the flag set 0x797405. CompileLevels
        // 0x7974b0 calls TryGetValue 0x797798 with the passed category, which is
        // null for a top-level level. An eager flag, a null-key skip or a fresh
        // dictionary per attempt fails here. The publication fault uses a null
        // GUID hash; it is the rebuilt managed NullReferenceException, not a
        // claim about the shipped player's null-check-free fault domain.
        [Test]
        public void CacheLevelsPublishesOnlyAfterCompileAndKeepsEntriesFromAFailedPass()
        {
            using (var owned = new OwnedLevels())
            {
                GameLevels topLevel = owned.Add(ScriptableObject.CreateInstance<GameLevels>());
                GameplayLevelDefinition loose = owned.Level("lucid-loose", "lucid-guid-loose");
                Entries(topLevel).Add(loose);
                Assert.Throws<ArgumentNullException>(topLevel.CacheLevels, "a top-level level is keyed by a null category");
                Assert.That(Read<bool>(topLevel, "m_levelsCached"), Is.False);
                Assert.That(Read<IReadOnlyList<GameplayLevelDefinition>>(topLevel, "m_allLevels"), Is.Empty);
                Assert.That(ZoneForLevel(topLevel), Is.Empty);
                Assert.Throws<ArgumentNullException>(() => topLevel.GetLevels(), "the failed pass is retried");

                GameLevels levels = owned.Add(ScriptableObject.CreateInstance<GameLevels>());
                GameplayLevelCategory zone = owned.Category("lucid-guid-zone");
                GameplayLevelDefinition first = owned.Level("lucid-first", "lucid-guid-first");
                GameplayLevelDefinition unhashable = owned.Level("lucid-second", null);
                zone.GameplayEntries.Add(first);
                zone.GameplayEntries.Add(unhashable);
                Entries(levels).Add(zone);
                Assert.Throws<NullReferenceException>(levels.CacheLevels);
                Assert.That(Read<bool>(levels, "m_levelsCached"), Is.False);
                Assert.That(Read<IReadOnlyList<GameplayLevelDefinition>>(levels, "m_allLevels"), Is.Empty);
                Assert.That(Read<IReadOnlyList<GameplayLevelCategory>>(levels, "m_allZones"), Is.Empty);
                Assert.That(LevelsPerZone(levels)[zone], Is.EqualTo(new[] { first, unhashable }));
                Assert.That(ZoneForLevel(levels).Keys, Is.EqualTo(new[] { first }));

                // Retry with a different authored graph: the earlier pass's first
                // level remains published although it is no longer authored.
                zone.GameplayEntries.Remove(first);
                SetGuid(unhashable, "lucid-guid-second");
                levels.CacheLevels();
                Assert.That(Read<bool>(levels, "m_levelsCached"), Is.True);
                Assert.That(levels.GetLevels(), Is.EqualTo(new[] { first, unhashable }));
                Assert.That(levels.GetZones(), Is.EqualTo(new[] { zone }));
                Assert.That(levels.GetLevels(zone), Is.EqualTo(new[] { unhashable }));
                Assert.That(levels.TryGetZoneForLevel(first, out GameplayLevelCategory retained), Is.True);
                Assert.That(retained, Is.SameAs(zone));
            }
        }

        // GameLevelsWithLinkedTests.GetLevel 0x0600181d, x86-64 0x7994d0.
        // CycleLevels (0xcfdd2cfd) tail-jumps to GameLevels.GetLevel 0x799588;
        // other types walk only matching authored groups, compare the inlined
        // SceneName (with '_exported' when configured) and return the captured
        // Scene. Searching groups for CycleLevels, or falling back to the base
        // list for other types, fails here.
        [Test]
        public void GetLevelDelegatesOnlyCycleLevelsAndOtherwiseReturnsTheLinkedScene()
        {
            using (var owned = new OwnedLevels())
            {
                var config = owned.Add(ScriptableObject.CreateInstance<GameLevelsWithLinkedTests>());
                GameplayLevelCategory zone = owned.Category("lucid-guid-zone");
                GameplayLevelDefinition based = owned.Level("lucid-base", "lucid-guid-base");
                GameplayLevelDefinition linked = owned.Level("lucid-linked", "lucid-guid-linked");
                zone.GameplayEntries.Add(based);
                Entries(config).Add(zone);
                config.GameScenesWithTests.Add(Group(AutoTestType.CycleLevels, linked));
                config.GameScenesWithTests.Add(Group(AutoTestType.LoadLevel, linked));

                owned.UseExportedScenes(false);
                Assert.That(config.GetLevel("lucid-base", AutoTestType.CycleLevels), Is.SameAs(based));
                Assert.That(config.GetLevel("lucid-linked", AutoTestType.CycleLevels), Is.Null);
                Assert.That(config.GetLevel("lucid-linked", AutoTestType.LoadLevel), Is.SameAs(linked));
                Assert.That(config.GetLevel("lucid-base", AutoTestType.LoadLevel), Is.Null);
                Assert.That(config.GetLevel("lucid-linked", AutoTestType.InputReachDestination), Is.Null);

                owned.UseExportedScenes(true);
                Assert.That(config.GetLevel("lucid-linked", AutoTestType.LoadLevel), Is.Null);
                Assert.That(config.GetLevel("lucid-linked_exported", AutoTestType.LoadLevel), Is.SameAs(linked));
            }
        }

        // GameLevelsWithLinkedTests.GetLevels 0x0600181e, x86-64 0x799970.
        // CacheLevels 0x799a2f, a List copy of m_allLevels 0x799a5a, then
        // List.Find 0x799a9c with the TestType predicate whatever the copy's
        // length. Only a non-empty copy enters the descending loop, which rereads
        // ExcludedLevels (+0x28) and uses List.Contains 0x799aeb (GUID equality)
        // before RemoveAt 0x799b04. Skipping Find for an empty copy, a null-group
        // fallback or identity-only exclusion fails here.
        [Test]
        public void GetLevelsRunsFindFirstAndRemovesGuidMatchesFromANewCopy()
        {
            using (var owned = new OwnedLevels())
            {
                var empty = owned.Add(ScriptableObject.CreateInstance<GameLevelsWithLinkedTests>());
                Assert.That(empty.GetLevels(AutoTestType.LoadLevel), Is.Empty, "an absent group is not read for an empty copy");
                empty.GameScenesWithTests.Add(null);
                Assert.Throws<NullReferenceException>(() => empty.GetLevels(AutoTestType.LoadLevel),
                    "Find evaluates its predicate even when no level is copied");

                var config = owned.Add(ScriptableObject.CreateInstance<GameLevelsWithLinkedTests>());
                GameplayLevelCategory zone = owned.Category("lucid-guid-zone");
                GameplayLevelDefinition a = owned.Level("lucid-a", "lucid-guid-a");
                GameplayLevelDefinition b = owned.Level("lucid-b", "lucid-guid-b");
                GameplayLevelDefinition c = owned.Level("lucid-c", "lucid-guid-c");
                GameplayLevelDefinition twinOfC = owned.Level("lucid-other-name", "lucid-guid-c");
                zone.GameplayEntries.AddRange(new[] { a, b, c });
                Entries(config).Add(zone);
                var other = new GameLevelsWithLinkedTests.LinkedLevelsWithTests { TestType = AutoTestType.InputReachDestination };
                other.ExcludedLevels.Add(b);
                var load = new GameLevelsWithLinkedTests.LinkedLevelsWithTests { TestType = AutoTestType.LoadLevel };
                load.ExcludedLevels.Add(twinOfC);
                load.ExcludedLevels.Add(a);
                config.GameScenesWithTests.Add(other);
                config.GameScenesWithTests.Add(load);

                List<GameplayLevelDefinition> result = config.GetLevels(AutoTestType.LoadLevel);
                Assert.That(result, Is.EqualTo(new[] { b }));
                Assert.That(config.GetLevels(), Is.EqualTo(new[] { a, b, c }), "the cached base list is copied, not edited");
                Assert.That(result, Is.Not.SameAs(config.GetLevels()));
                Assert.Throws<NullReferenceException>(() => config.GetLevels(AutoTestType.CycleLevels),
                    "a non-empty copy dereferences the missing group");
            }
        }

        private static GameLevelsWithLinkedTests.LinkedLevelsWithTests Group(AutoTestType type, GameplayLevelDefinition scene)
        {
            var group = new GameLevelsWithLinkedTests.LinkedLevelsWithTests { TestType = type };
            group.LevelData.Add(new GameLevelsWithLinkedTests.LevelData { Scene = scene });
            return group;
        }

        private static List<GameplayLevelUIEntry> Entries(GameLevels levels) =>
            (List<GameplayLevelUIEntry>)typeof(GameLevels).GetField("m_gameplayEntries", Instance).GetValue(levels);

        private static T Read<T>(GameLevels levels, string name) =>
            (T)typeof(GameLevels).GetField(name, Instance).GetValue(levels);

        private static Dictionary<GameplayLevelDefinition, GameplayLevelCategory> ZoneForLevel(GameLevels levels) =>
            Read<Dictionary<GameplayLevelDefinition, GameplayLevelCategory>>(levels, "m_zoneForLevel");

        private static Dictionary<GameplayLevelCategory, IReadOnlyList<GameplayLevelDefinition>> LevelsPerZone(GameLevels levels) =>
            Read<Dictionary<GameplayLevelCategory, IReadOnlyList<GameplayLevelDefinition>>>(levels, "m_levelsPerZone");

        private static void SetGuid(ScriptableObjectWithGuid value, string guid) =>
            typeof(ScriptableObjectWithGuid).GetField("m_guid", Instance).SetValue(value, guid);

        private sealed class OwnedLevels : IDisposable
        {
            private static readonly FieldInfo s_core = typeof(GameplayLevelDefinition).GetField("m_coreGameConfiguration", Static);
            private readonly List<Object> m_objects = new List<Object>();
            private readonly VerificationFixtureOwnership.RegistryRow m_levelManagerRow;
            private readonly object m_priorCore;
            private CoreGameConfiguration m_core;

            // A category's initializer only reads or creates the LevelManager
            // row and its typed reference; nothing is registered into it. An
            // absent row is acquired empty and removed afterwards, while a row
            // left by an earlier asset import is read as it is.
            public OwnedLevels()
            {
                m_priorCore = s_core.GetValue(null);
                string name = ProcessManager.GetDefaultName<LevelManager>();
                var registry = (System.Collections.IDictionary)typeof(ProcessManager)
                    .GetField("s_systemDictionary", Static).GetValue(null);
                if (registry.Contains(name)) return;
                m_levelManagerRow = new VerificationFixtureOwnership.RegistryRow(name);
                m_levelManagerRow.Register(null);
            }

            public T Add<T>(T value) where T : Object
            {
                m_objects.Add(value);
                return value;
            }

            public GameplayLevelDefinition Level(string sceneName, string guid)
            {
                var level = Add(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                typeof(GameplayLevelDefinition).GetField("m_sceneName", Instance).SetValue(level, sceneName);
                SetGuid(level, guid);
                return level;
            }

            public GameplayLevelCategory Category(string guid)
            {
                var category = Add(ScriptableObject.CreateInstance<GameplayLevelCategory>());
                FieldInfo entries = typeof(GameplayLevelCategory).GetField("m_gameplayEntries", Instance);
                if (entries.GetValue(category) == null) entries.SetValue(category, new List<GameplayLevelUIEntry>());
                SetGuid(category, guid);
                return category;
            }

            // SceneName caches its configuration in a private static; this
            // fixture supplies its own and restores the prior reference.
            public void UseExportedScenes(bool exported)
            {
                if (m_core == null) m_core = Add(ScriptableObject.CreateInstance<CoreGameConfiguration>());
                typeof(CoreGameConfiguration).GetField("m_useExportedScenes", Instance).SetValue(m_core, exported);
                s_core.SetValue(null, m_core);
            }

            public void Dispose()
            {
                try
                {
                    s_core.SetValue(null, m_priorCore);
                    m_levelManagerRow?.Dispose();
                }
                finally
                {
                    foreach (Object value in m_objects) if (value != null) Object.DestroyImmediate(value);
                }
            }
        }
    }
}
