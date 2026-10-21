using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetArchetypeImageSpawner (Game.Runtime 0x0200099b, x86-64 0x699A90..0x699DA0) as a real component on an inactive owned host,
    // spawning clones of a UIWidgetImageArchetype prefab (an inactive owned object) under an owned parent, over a real DataManager.
    // Edit Mode refuses Object.Destroy with a logged error and leaves the object alive; the clones are children of the owned parent
    // and go with it. The serialized parent and prefab are authored data.
    public sealed class UIWidgetArchetypeImageSpawnerTests
    {
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");

        private sealed class Scene
        {
            public DataManager Data;
            public GameObject Parent;
            public UIWidgetArchetypeImageSpawner Spawner;
            public CharacterArchetypeDefinition Dashing;
            public Sprite DashingIcon;
            public CharacterDefinition Sonic;
            public Sprite SonicRender;

            public UIWidgetImageArchetype Spawned => Get<UIWidgetImageArchetype>(Spawner, "m_spawnedInstance");
        }

        // The DataManager is registered before the spawner caches its reference.
        private static void Run(Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            var scene = new Scene();
            scene.Data = owned.Host("Lucid spawner data manager", false).AddComponent<DataManager>();
            owned.Register(scene.Data);
            scene.Dashing = NewArchetypeDefinition(owned, CharacterArchetype.Dashing, out scene.DashingIcon, out _, out _);
            scene.Sonic = NewCharacterDefinition(owned, CharacterId.Sonic, out _, out scene.SonicRender);
            scene.Data.CharacterArchetypes[CharacterArchetype.Dashing] = scene.Dashing;
            scene.Data.Characters[CharacterId.Sonic] = scene.Sonic;
            scene.Parent = owned.Host("Lucid archetype spawn parent", false);
            var prefab = owned.Host("Lucid archetype image prefab", false).AddComponent<UIWidgetImageArchetype>();
            scene.Spawner = owned.Host("Lucid archetype image spawner", false).AddComponent<UIWidgetArchetypeImageSpawner>();
            Set(scene.Spawner, "m_spawnParent", scene.Parent.transform);
            Set(scene.Spawner, "m_prefab", prefab);
            body(owned, scene);
        });

        // dump.cs TypeDefIndex 2458 and the DummyDll rows (fields 0x04002832..0x04002835; characterId defaults to CharacterId.None).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetArchetypeImageSpawner), 0x00100001,
                new (string, int)[]
                {
                    ("m_spawnParent", 0x0001), ("m_prefab", 0x0001), ("m_dataManagerRef", 0x0021), ("m_spawnedInstance", 0x0001),
                },
                new (string, int)[] { ("Spawn", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetArchetypeImageSpawner), 1, 2);
            Assert.That(typeof(UIWidgetArchetypeImageSpawner).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            foreach (string field in new[] { "m_spawnParent", "m_prefab" })
                Assert.That(AttributeTypes(typeof(UIWidgetArchetypeImageSpawner).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            var parameters = typeof(UIWidgetArchetypeImageSpawner).GetMethod("Spawn").GetParameters();
            Assert.That(parameters[1].HasDefaultValue, Is.True);
            Assert.That(Convert.ToInt32(parameters[1].DefaultValue), Is.EqualTo((int)CharacterId.None));
        }

        // The constructor 0x699D20 takes the DataManager reference (+0x30).
        [Test]
        public void ConstructorTakesTheRegistryDataManagerReference()
        {
            Run((owned, scene) =>
                Assert.That(Get<SystemRef<DataManager>>(scene.Spawner, "m_dataManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<DataManager>())));
        }

        // 0x699C38..0x699CB2: a known archetype shows the parent and spawns a clone under it, set up with the archetype definition
        // and no character definition.
        [Test]
        public void SpawningAnArchetypeShowsTheParentAndSetsUpAClone()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.Spawn(CharacterArchetype.Dashing);

                UIWidgetImageArchetype clone = scene.Spawned;
                Assert.That(scene.Parent.activeSelf, Is.True);
                Assert.That(clone, Is.Not.Null);
                Assert.That(clone.transform.parent, Is.SameAs(scene.Parent.transform));
                Assert.That(clone.Archetype, Is.EqualTo(CharacterArchetype.Dashing));
                Assert.That(clone.ArchetypeSprite.Value, Is.SameAs(scene.DashingIcon));
                var parameters = Get<UIWidgetImageArchetypeParameters>(clone, "m_parameters");
                Assert.That(parameters.CharacterArchetypeDefinition, Is.SameAs(scene.Dashing));
                Assert.That(parameters.CharacterDefinition, Is.Null);
            });
        }

        // A character id alone is looked up in the data manager's characters (0x699BE0..0x699BF6) and handed over with a null archetype.
        [Test]
        public void SpawningACharacterHandsOverItsDefinition()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.Spawn(CharacterArchetype.None, CharacterId.Sonic);

                UIWidgetImageArchetype clone = scene.Spawned;
                Assert.That(clone.CharacterSprite.Value, Is.SameAs(scene.SonicRender));
                var parameters = Get<UIWidgetImageArchetypeParameters>(clone, "m_parameters");
                Assert.That(parameters.CharacterArchetypeDefinition, Is.Null);
                Assert.That(parameters.CharacterDefinition, Is.SameAs(scene.Sonic));
            });
        }

        // Both given: both definitions are handed over (the widget itself decides that the archetype wins).
        [Test]
        public void SpawningAnArchetypeAndACharacterHandsOverBothDefinitions()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.Spawn(CharacterArchetype.Dashing, CharacterId.Sonic);

                var parameters = Get<UIWidgetImageArchetypeParameters>(scene.Spawned, "m_parameters");
                Assert.That(parameters.CharacterArchetypeDefinition, Is.SameAs(scene.Dashing));
                Assert.That(parameters.CharacterDefinition, Is.SameAs(scene.Sonic));
            });
        }

        // 0x699B8A..0x699B90: with neither an archetype nor a character the parent is hidden and nothing is spawned, and the
        // previous clone is dropped first (its Destroy is refused in Edit Mode).
        [Test]
        public void SpawningNothingHidesTheParentAndDropsThePreviousClone()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.Spawn(CharacterArchetype.Dashing);
                Assert.That(scene.Spawned, Is.Not.Null);

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                scene.Spawner.Spawn(CharacterArchetype.None);

                Assert.That(scene.Parent.activeSelf, Is.False);
                Assert.That(scene.Spawned, Is.Null);
            });
        }

        // A later spawn replaces the earlier clone with a new one.
        [Test]
        public void SpawningAgainReplacesTheClone()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.Spawn(CharacterArchetype.Dashing);
                UIWidgetImageArchetype first = scene.Spawned;

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                scene.Spawner.Spawn(CharacterArchetype.Dashing);

                Assert.That(scene.Spawned, Is.Not.Null.And.Not.SameAs(first));
                Assert.That(scene.Parent.activeSelf, Is.True);
            });
        }

        // 0x699BBD..0x699BD5: an archetype the data manager does not hold ends the spawn after the parent was hidden.
        [Test]
        public void AnUnknownArchetypeLeavesTheParentHiddenWithNothingSpawned()
        {
            Run((owned, scene) =>
            {
                scene.Parent.SetActive(true);

                scene.Spawner.Spawn(CharacterArchetype.Flying);

                Assert.That(scene.Parent.activeSelf, Is.False);
                Assert.That(scene.Spawned, Is.Null);
            });
        }

        // 0x699BF1: the character is read with the dictionary indexer, so an unknown id throws, again after the parent was hidden.
        [Test]
        public void AnUnknownCharacterThrowsAfterHidingTheParent()
        {
            Run((owned, scene) =>
            {
                scene.Parent.SetActive(true);

                Assert.Throws<KeyNotFoundException>(() => scene.Spawner.Spawn(CharacterArchetype.None, CharacterId.Tails));

                Assert.That(scene.Parent.activeSelf, Is.False);
                Assert.That(scene.Spawned, Is.Null);
            });
        }
    }
}
