using System;
using System.Collections.Generic;
using System.Linq;
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
    // UIWidgetCharacterHeadsSpawner (Game.Runtime 0x020009ae, x86-64 0x69CAC0..0x69D680) as a real component on an inactive owned host,
    // spawning clones of a UIWidgetButtonImageCharacter prefab (an inactive owned object with a swap icon child) under an owned parent,
    // over a real DataManager holding three owned character definitions (Amy, Cream, Sonic, in that order). Edit Mode refuses
    // Object.Destroy with a logged error and leaves the object alive; the clones are children of the owned parent and go with it.
    // The serialized parent, prefab and flags are authored data.
    public sealed class UIWidgetCharacterHeadsSpawnerTests
    {
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");

        private sealed class Scene
        {
            public UIWidgetCharacterHeadsSpawner Spawner;
            public CharacterDefinition Amy, Cream, Sonic;

            public List<UIWidgetButtonImageCharacter> Heads => Get<List<UIWidgetButtonImageCharacter>>(Spawner, "m_characterHeadsInstances");

            public CharacterId[] HeadIds => Heads.Select(head => head.CharacterId).ToArray();
        }

        // The DataManager is registered before the spawner exists; Awake takes its reference.
        private static void Run(Action<Owned, Scene> body, bool showNoneIfAll = false, bool showSwapIcon = false) => Owned.Run(owned =>
        {
            var scene = new Scene();
            var data = owned.Host("Lucid heads data manager", false).AddComponent<DataManager>();
            owned.Register(data);
            scene.Amy = NewCharacterDefinition(owned, CharacterId.Amy, out _, out _);
            scene.Cream = NewCharacterDefinition(owned, CharacterId.Cream, out _, out _);
            scene.Sonic = NewCharacterDefinition(owned, CharacterId.Sonic, out _, out _);
            data.Characters[CharacterId.Amy] = scene.Amy;
            data.Characters[CharacterId.Cream] = scene.Cream;
            data.Characters[CharacterId.Sonic] = scene.Sonic;
            GameObject parent = owned.Host("Lucid heads parent", false);
            GameObject prefabHost = owned.Host("Lucid head prefab", false);
            var prefab = prefabHost.AddComponent<UIWidgetButtonImageCharacter>();
            Set(prefab, "m_swapIcon", owned.Host("Lucid head swap icon", true, prefabHost.transform)); // serialized
            scene.Spawner = owned.Host("Lucid heads spawner", false).AddComponent<UIWidgetCharacterHeadsSpawner>();
            Set(scene.Spawner, "m_spawnParent", parent.transform);
            Set(scene.Spawner, "m_characterHeadPrefab", prefab);
            Set(scene.Spawner, "m_showNoneIfAll", showNoneIfAll);
            Set(scene.Spawner, "m_showSwapIcon", showSwapIcon);
            scene.Spawner.Awake();
            body(owned, scene);
        });

        private static void ExpectRefusedDestroys(int count)
        {
            for (int i = 0; i < count; i++) LogAssert.Expect(LogType.Error, RefusedDestroy);
        }

        // dump.cs TypeDefIndex 2477 and the DummyDll rows (fields 0x0400288d..0x04002892).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetCharacterHeadsSpawner), 0x00100001,
                new (string, int)[]
                {
                    ("m_spawnParent", 0x0001), ("m_characterHeadPrefab", 0x0001), ("m_showNoneIfAll", 0x0001), ("m_showSwapIcon", 0x0001),
                    ("m_dataManagerRef", 0x0001), ("m_characterHeadsInstances", 0x0021),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0086), ("UpdateCharacterHeads", 0x0086), ("CheckCharacterIdsMatch", 0x0081),
                    ("ClearAllCharacterHeads", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetCharacterHeadsSpawner), 2, 1);
            Assert.That(typeof(UIWidgetCharacterHeadsSpawner).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeTypes(typeof(UIWidgetCharacterHeadsSpawner).GetField("m_showNoneIfAll", Declared)),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(typeof(UIWidgetCharacterHeadsSpawner).GetField("m_showNoneIfAll", Declared)),
                Is.EqualTo("If ticked, if character heads are requested to be shown for all existent characters, instead show none."));
        }

        // The constructor 0x69D600 allocates the instance list; the DataManager reference is only taken by Awake (0x69CAC0).
        [Test]
        public void AwakeTakesTheDataManagerReferenceAndTheListStartsEmpty()
        {
            Owned.Run(owned =>
            {
                owned.Register(owned.Host("Lucid heads data manager", false).AddComponent<DataManager>());
                var spawner = owned.Host("Lucid heads spawner", false).AddComponent<UIWidgetCharacterHeadsSpawner>();
                Assert.That(Get<List<UIWidgetButtonImageCharacter>>(spawner, "m_characterHeadsInstances"), Is.Empty);
                Assert.That(Get<SystemRef<DataManager>>(spawner, "m_dataManagerRef"), Is.Null);

                spawner.Awake();

                Assert.That(Get<SystemRef<DataManager>>(spawner, "m_dataManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<DataManager>()));
            });
        }

        // 0x69CD69..0x69CF38: the heads follow the data manager's character order, not the requested order, and only listed characters
        // get one; each head is set up with its character definition and the spawner's swap icon flag.
        [TestCase(false, TestName = "HeadsAreSpawnedInCharacterOrderWithoutTheSwapIcon")]
        [TestCase(true, TestName = "HeadsAreSpawnedInCharacterOrderWithTheSwapIcon")]
        public void HeadsAreSpawnedInTheDataManagersOrder(bool showSwapIcon)
        {
            Run((owned, scene) =>
            {
                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Sonic, CharacterId.Amy });

                Assert.That(scene.HeadIds, Is.EqualTo(new[] { CharacterId.Amy, CharacterId.Sonic }));
                Assert.That(scene.Heads.Select(head => Get<CharacterDefinition>(head, "m_character")).ToArray(),
                    Is.EqualTo(new[] { scene.Amy, scene.Sonic }));
                foreach (UIWidgetButtonImageCharacter head in scene.Heads)
                {
                    Assert.That(head.transform.parent, Is.SameAs(Get<Transform>(scene.Spawner, "m_spawnParent")));
                    Assert.That(Get<GameObject>(head, "m_swapIcon").activeSelf, Is.EqualTo(showSwapIcon));
                }
            }, showSwapIcon: showSwapIcon);
        }

        // 0x69CC22..0x69CC2C: a request that already matches the spawned heads changes nothing (the same objects stay).
        [Test]
        public void AMatchingRequestKeepsTheExistingHeads()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Amy, CharacterId.Sonic });
                UIWidgetButtonImageCharacter[] before = scene.Heads.ToArray();

                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Sonic, CharacterId.Amy });

                Assert.That(scene.Heads.ToArray(), Is.EqualTo(before));
            });
        }

        // A different request clears the old heads (each Destroy is refused in Edit Mode) and spawns the new set.
        [Test]
        public void ADifferentRequestRebuildsTheHeads()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Amy, CharacterId.Sonic });
                UIWidgetButtonImageCharacter[] before = scene.Heads.ToArray();

                ExpectRefusedDestroys(2);
                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Cream, CharacterId.Sonic });

                Assert.That(scene.HeadIds, Is.EqualTo(new[] { CharacterId.Cream, CharacterId.Sonic }));
                Assert.That(scene.Heads.Intersect(before), Is.Empty);
            });
        }

        // 0x69CC51..0x69CCE5 and 0x69CD42: with m_showNoneIfAll a request for at least as many ids as there are characters shows
        // nothing (the old heads are still cleared); a smaller one is shown. An empty request never shows anything. Without the flag a
        // full request shows every character. Truth table (flag, requested, heads shown).
        [TestCase(true, 3, 0, TestName = "ARequestForEveryCharacterShowsNoneWhenAsked")]
        [TestCase(true, 2, 2, TestName = "ARequestForSomeCharactersShowsThemWhenNoneIfAllIsSet")]
        [TestCase(false, 3, 3, TestName = "ARequestForEveryCharacterShowsAllWithoutTheFlag")]
        [TestCase(true, 0, 0, TestName = "AnEmptyRequestShowsNoneWithTheFlag")]
        [TestCase(false, 0, 0, TestName = "AnEmptyRequestShowsNoneWithoutTheFlag")]
        public void ShowNoneIfAllDecidesWhetherAFullRequestIsShown(bool showNoneIfAll, int requested, int shown)
        {
            Run((owned, scene) =>
            {
                CharacterId[] all = { CharacterId.Amy, CharacterId.Cream, CharacterId.Sonic };

                scene.Spawner.UpdateCharacterHeads(all.Take(requested).ToArray());

                Assert.That(scene.Heads.Count, Is.EqualTo(shown));
            }, showNoneIfAll);
        }

        // 0x69CDF8..0x69CE17: an id the data manager does not know matches no character, so no head is spawned for it.
        [Test]
        public void UnknownIdsGetNoHead()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Tails, CharacterId.Cream });

                Assert.That(scene.HeadIds, Is.EqualTo(new[] { CharacterId.Cream }));
            });
        }

        // CheckCharacterIdsMatch 0x69CFD0: the counts must be equal and every requested id must have a head, in any order. An empty
        // request (equal counts of zero) never matches. Truth table over a spawner showing Amy and Sonic.
        [Test]
        public void ARequestMatchesOnlyWhenEveryIdHasAHeadAndTheCountsAreEqual()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Amy, CharacterId.Sonic });
                var rows = new (CharacterId[] ids, bool expected)[]
                {
                    (new[] { CharacterId.Amy, CharacterId.Sonic }, true),
                    (new[] { CharacterId.Sonic, CharacterId.Amy }, true),
                    (new[] { CharacterId.Amy }, false),
                    (new[] { CharacterId.Amy, CharacterId.Sonic, CharacterId.Cream }, false),
                    (new[] { CharacterId.Amy, CharacterId.Cream }, false),
                    (new[] { CharacterId.Amy, CharacterId.Amy }, true),
                    (new CharacterId[0], false),
                };
                foreach (var row in rows)
                    Assert.That(Call(scene.Spawner, "CheckCharacterIdsMatch", (object)row.ids), Is.EqualTo(row.expected), string.Join(",", row.ids));
            });
            Run((owned, scene) =>
                Assert.That(Call(scene.Spawner, "CheckCharacterIdsMatch", (object)new CharacterId[0]), Is.False, "nothing spawned and nothing requested"));
        }

        // 0x69D460: every head's object is destroyed (refused in Edit Mode) and the list is emptied.
        [Test]
        public void ClearingEmptiesTheListAndDestroysEveryHead()
        {
            Run((owned, scene) =>
            {
                scene.Spawner.UpdateCharacterHeads(new[] { CharacterId.Amy, CharacterId.Cream, CharacterId.Sonic });
                Assert.That(scene.Heads.Count, Is.EqualTo(3));

                ExpectRefusedDestroys(3);
                scene.Spawner.ClearAllCharacterHeads();

                Assert.That(scene.Heads, Is.Empty);
            });
        }
    }
}
