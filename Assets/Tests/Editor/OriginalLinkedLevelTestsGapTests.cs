using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned in-memory assets. SceneName caches its configuration in a private
    // static; this fixture supplies an owned one and restores the prior value.
    public sealed class OriginalLinkedLevelTestsGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic;

        // GameLevelsWithLinkedTests.OnBeforeSerialize 0x0600181f, x86-64
        // 0x799b30: each LevelData.Scene goes through
        // ScriptableObjectWithGuid.op_Equality(scene, null) 0x799cf9, so a
        // destroyed scene counts as null and its entry keeps its Name. A live
        // scene's Name is the inlined SceneName getter (m_sceneName +0x48 plus
        // '_exported' when the cached configuration's +0x28 flag is set,
        // String.Concat 0x799dd3). A ReferenceEquals null test (renaming the
        // destroyed entry) or copying the raw scene field fails here.
        [Test]
        public void LiveScenesRenameEntriesThroughSceneNameAndDestroyedScenesKeepTheirNames()
        {
            FieldInfo cachedCore = typeof(GameplayLevelDefinition).GetField("m_coreGameConfiguration", AnyStatic);
            object priorCore = cachedCore.GetValue(null);
            var owned = new List<Object>();
            try
            {
                var core = Own(owned, ScriptableObject.CreateInstance<CoreGameConfiguration>());
                typeof(CoreGameConfiguration).GetField("m_useExportedScenes", AnyInstance).SetValue(core, true);
                cachedCore.SetValue(null, core);
                var config = Own(owned, ScriptableObject.CreateInstance<GameLevelsWithLinkedTests>());
                GameplayLevelDefinition live = Level(owned, "lucid-live");
                GameplayLevelDefinition gone = Level(owned, "lucid-gone");
                var liveEntry = new GameLevelsWithLinkedTests.LevelData { Name = "stale", Scene = live };
                var goneEntry = new GameLevelsWithLinkedTests.LevelData { Name = "kept", Scene = gone };
                var group = new GameLevelsWithLinkedTests.LinkedLevelsWithTests { TestType = AutoTestType.LoadLevel };
                group.LevelData.Add(liveEntry);
                group.LevelData.Add(goneEntry);
                config.GameScenesWithTests.Add(group);
                Object.DestroyImmediate(gone);
                Assert.That(ReferenceEquals(goneEntry.Scene, null), Is.False);

                config.OnBeforeSerialize();
                Assert.That(group.Name, Is.EqualTo("LoadLevel"));
                Assert.That(liveEntry.Name, Is.EqualTo("lucid-live_exported"));
                Assert.That(goneEntry.Name, Is.EqualTo("kept"));
            }
            finally
            {
                cachedCore.SetValue(null, priorCore);
                foreach (Object item in owned) if (item != null) Object.DestroyImmediate(item);
            }
        }

        private static T Own<T>(List<Object> owned, T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        private static GameplayLevelDefinition Level(List<Object> owned, string sceneName)
        {
            var level = Own(owned, ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            typeof(GameplayLevelDefinition).GetField("m_sceneName", AnyInstance).SetValue(level, sceneName);
            return level;
        }
    }
}
