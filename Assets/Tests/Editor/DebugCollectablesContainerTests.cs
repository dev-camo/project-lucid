using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The debug collectables list and the frame counter container (Game.Runtime 020008af UIContainerDebugCollectableModifier,
    // 020008b1/020008b2 UIContainerDebugCollectables and its closure, 020008ba UIContainerFPSDebug; x86-64 0x62EDA0..0x62F6F7 and
    // 0x631730..0x6317DF). The list runs over a real CollectableManager and DataManager registered by the mission world fixture, the
    // rows are clones of a real row component on an owned inactive host, and the string table is an owned one.
    // Not covered: the closure and row objects are only reached through the rows' own actions, as in the original (OnChange has no
    // other caller); the Start/InvokeOnValid path for a manager that is not yet registered (it queues a deferred call on the shared
    // registry row).
    public sealed class DebugCollectablesContainerTests
    {
        private const string FormatXOfY = "x of y";

        private sealed class Rig
        {
            public UIContainerDebugCollectables Container;
            public Transform Root;
            public CollectableManager Manager;

            public UIContainerDebugCollectableModifier[] Rows =>
                Root.GetComponentsInChildren<UIContainerDebugCollectableModifier>(true);

            public UIContainerDebugCollectableModifier Row(string name) => Rows.Single(row => row.Name.Value == name);

            public CollectableState State(CollectableType type)
            {
                Assert.That(Manager.TryGetCollectableState(type, out CollectableState state), Is.True);
                return state;
            }
        }

        // The manager holds the Orb (ten in all, three collected) and the BlueCoin (two in all, one collected); the Orb definition
        // has a total as the case chooses and the BlueCoin one has a total. Types without a registered state have no row.
        private static void WithRig(bool orbHasTotal, Action<Rig> body) => WithWorld(world => OwnedStringTable.Run(
            new Dictionary<Strings, string> { { Strings.FORMAT_X_OF_Y, FormatXOfY } }, () =>
            {
                Owned owned = world.Owned;
                DataManager data = owned.Register(owned.Host("Lucid debug collectables data manager", false).AddComponent<DataManager>());
                foreach ((CollectableType type, bool hasTotal) in new[] { (CollectableType.Orb, orbHasTotal), (CollectableType.BlueCoin, true) })
                {
                    var definition = owned.Track(ScriptableObject.CreateInstance<CollectableDefinition>());
                    Set(definition, "m_hasTotal", hasTotal); // Reflection: authored definition data
                    data.CollectableDefinitions[type] = definition;
                }
                world.Collectables.RegisterCollectable(CollectableType.Orb, 10);
                world.Collectables.RegisterCollectable(CollectableType.BlueCoin, 2);
                var rig = new Rig { Manager = world.Collectables, Root = owned.Host("Lucid debug collectables list", false).transform };
                rig.State(CollectableType.Orb).AdjustCollected(3, true);
                rig.State(CollectableType.BlueCoin).AdjustCollected(1, true);
                var prefab = owned.Host("Lucid debug collectable row prefab", false).AddComponent<UIContainerDebugCollectableModifier>();
                rig.Container = owned.Host("Lucid debug collectables", false).AddComponent<UIContainerDebugCollectables>();
                Set(rig.Container, "m_listRoot", rig.Root); // Reflection: serialized references
                Set(rig.Container, "m_debugCollectableModifierPrefab", prefab);
                body(rig);
            }));

        // dump.cs TypeDefIndex 2222, 2225 and 2224 and the DummyDll rows (fields 0x04002281..0x04002293).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerDebugCollectableModifier), 0x00100101,
                new (string, int)[]
                {
                    ("FormatXOfY", 0x0026), ("Name", 0x0026), ("Collected", 0x0026), ("Total", 0x0026),
                    ("m_onIncrease", 0x0001), ("m_onDecrease", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6), ("Action_OnIncrease", 0x0086), ("Action_OnDecrease", 0x0086), ("UpdateCollected", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerDebugCollectableModifier), 2, 1);
            Assert.That(typeof(UIContainerDebugCollectableModifier).BaseType, Is.EqualTo(typeof(UIContainer)));

            AssertMembers(typeof(UIContainerDebugCollectables), 0x00100101,
                new (string, int)[]
                {
                    ("m_debugCollectablesContainerIdentifier", 0x0001), ("m_listRoot", 0x0001), ("m_debugCollectableModifierPrefab", 0x0001),
                    ("m_changeValueBy", 0x0001), ("m_collectableManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("Action_Close", 0x0086), ("OnCollectableManagerValid", 0x0081), ("PopulateCollectables", 0x0081),
                    ("OnChange", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerDebugCollectables), 1, 2);
            Assert.That(typeof(UIContainerDebugCollectables).BaseType, Is.EqualTo(typeof(UIContainer)));
            foreach (string name in new[] { "m_debugCollectablesContainerIdentifier", "m_listRoot", "m_debugCollectableModifierPrefab", "m_changeValueBy" })
                Assert.That(AttributeTypes(Field(typeof(UIContainerDebugCollectables), name)), Is.EqualTo(new[] { typeof(SerializeField) }), name);

            Type closure = typeof(UIContainerDebugCollectables).GetNestedType("<>c__DisplayClass8_0", BindingFlags.NonPublic);
            Assert.That(closure, Is.Not.Null, "the closure keeps the original compiler name");
            AssertMembers(closure, 0x00100103,
                new (string, int)[] { ("collectableType", 0x0006), ("debugCollectableModifier", 0x0006), ("<>4__this", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<PopulateCollectables>b__0", 0x0083), ("<PopulateCollectables>b__1", 0x0083) });
        }

        [Test]
        public void TheFrameCounterDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerFPSDebug), 0x00100101,
                new (string, int)[] { ("FPSCount", 0x0026) },
                new (string, int)[] { ("Update", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerFPSDebug), 2, 1);
            Assert.That(typeof(UIContainerFPSDebug).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // 0x62EDA0: the base setup, the X-of-Y format string from the table (0xEBF0870B is FORMAT_X_OF_Y), then the name, the collected
        // count at +8 and the total at +12 of the parameters and the two actions, each into its own field.
        [Test]
        public void ARowPublishesItsParametersAndKeepsBothActions()
        {
            Owned.Run(owned => OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.FORMAT_X_OF_Y, FormatXOfY } }, () =>
            {
                var row = owned.Host("Lucid debug collectable row", false).AddComponent<UIContainerDebugCollectableModifier>();
                int increases = 0, decreases = 0;

                row.Setup(new UIContainerDebugCollectableModifierParameters("Rings", 7, 20, () => increases++, () => decreases++));

                Assert.That(row.FormatXOfY.Value, Is.EqualTo(FormatXOfY));
                Assert.That(row.Name.Value, Is.EqualTo("Rings"));
                Assert.That(row.Collected.Value, Is.EqualTo(7));
                Assert.That(row.Total.Value, Is.EqualTo(20));
                row.Action_OnIncrease();
                row.Action_OnIncrease();
                row.Action_OnDecrease();
                Assert.That((increases, decreases), Is.EqualTo((2, 1)), "each action runs its own callback");
            }));
        }

        // 0x62EED0 and 0x62EEF0 test the field for null before the invocation, so a row without actions (or one that was never
        // set up) ignores both.
        [Test]
        public void ARowWithoutActionsIgnoresBothButtons()
        {
            Owned.Run(owned => OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.FORMAT_X_OF_Y, FormatXOfY } }, () =>
            {
                var row = owned.Host("Lucid debug collectable row", false).AddComponent<UIContainerDebugCollectableModifier>();
                Assert.DoesNotThrow(row.Action_OnIncrease);
                Assert.DoesNotThrow(row.Action_OnDecrease);

                row.Setup(new UIContainerDebugCollectableModifierParameters("Rings", 1, 2, null, null));
                Assert.DoesNotThrow(row.Action_OnIncrease);
                Assert.DoesNotThrow(row.Action_OnDecrease);
            }));
        }

        // 0x62EF10: the collected bindable alone takes the amount.
        [Test]
        public void UpdatingTheCollectedAmountLeavesTheRestAlone()
        {
            Owned.Run(owned =>
            {
                var row = owned.Host("Lucid debug collectable row", false).AddComponent<UIContainerDebugCollectableModifier>();
                row.Name.Value = "Rings";
                row.Total.Value = 9;
                row.UpdateCollected(4);
                Assert.That(row.Collected.Value, Is.EqualTo(4));
                Assert.That(row.Total.Value, Is.EqualTo(9));
                Assert.That(row.Name.Value, Is.EqualTo("Rings"));
            });
        }

        // 0x62F5E4 stores 5 at +0x50 before the base constructor call.
        [Test]
        public void TheListChangesCollectablesByFiveByDefault()
        {
            Owned.Run(owned =>
                Assert.That(Get<int>(owned.Host("Lucid debug collectables", false).AddComponent<UIContainerDebugCollectables>(), "m_changeValueBy"), Is.EqualTo(5)));
        }

        // 0x62F100..0x62F290: Start asks the registry for the collectable manager and the valid-callback fills the list. 0x62F290: one
        // row per collectable type that has a registered state, with the name from the type's string (HardlightEnumExtensions.GetString),
        // the collected count (state +0x14) and the total (state +0x10) - the Orb is three of ten, the BlueCoin one of two, so a swap of
        // the two counts shows.
        [Test]
        public void StartListsOneRowPerRegisteredCollectable()
        {
            WithRig(false, rig =>
            {
                Call(rig.Container, "Start");

                Assert.That(Get<CollectableManager>(rig.Container, "m_collectableManager"), Is.SameAs(rig.Manager));
                Assert.That(rig.Rows.Select(row => row.Name.Value).OrderBy(name => name).ToArray(), Is.EqualTo(new[] { "BlueCoin", "Orb" }));
                UIContainerDebugCollectableModifier orb = rig.Row("Orb");
                Assert.That((orb.Collected.Value, orb.Total.Value), Is.EqualTo((3, 10)));
                Assert.That(orb.FormatXOfY.Value, Is.EqualTo(FormatXOfY));
                UIContainerDebugCollectableModifier coin = rig.Row("BlueCoin");
                Assert.That((coin.Collected.Value, coin.Total.Value), Is.EqualTo((1, 2)));
            });
        }

        // 0x62F600 and 0x62F680 (the two closures) and 0x62F560 (OnChange): the increase action adds m_changeValueBy of its row's
        // type to the manager and the row then shows the state's collected count; the decrease action subtracts it. The other row is
        // not touched. The Orb has no total here, so it only stops at zero.
        [Test]
        public void TheRowActionsAddAndRemoveTheChangeValueOfTheirOwnType()
        {
            WithRig(false, rig =>
            {
                Call(rig.Container, "Start");
                UIContainerDebugCollectableModifier orb = rig.Row("Orb");

                orb.Action_OnIncrease();
                Assert.That(rig.State(CollectableType.Orb).Collected, Is.EqualTo(8));
                Assert.That(orb.Collected.Value, Is.EqualTo(8));
                orb.Action_OnDecrease();
                Assert.That((rig.State(CollectableType.Orb).Collected, orb.Collected.Value), Is.EqualTo((3, 3)));
                orb.Action_OnDecrease();
                Assert.That((rig.State(CollectableType.Orb).Collected, orb.Collected.Value), Is.EqualTo((0, 0)), "the count stops at zero");
                Assert.That(rig.State(CollectableType.BlueCoin).Collected, Is.EqualTo(1));
                Assert.That(rig.Row("BlueCoin").Collected.Value, Is.EqualTo(1));
            });
        }

        // The amount comes from the authored field (0x62F626 loads [this+0x50]), not from a constant.
        [Test]
        public void TheAuthoredChangeValueSetsTheStep()
        {
            WithRig(false, rig =>
            {
                Set(rig.Container, "m_changeValueBy", 2); // Reflection: serialized field
                Call(rig.Container, "Start");
                UIContainerDebugCollectableModifier orb = rig.Row("Orb");

                orb.Action_OnIncrease();
                Assert.That(orb.Collected.Value, Is.EqualTo(5));
                orb.Action_OnDecrease();
                orb.Action_OnDecrease();
                Assert.That(orb.Collected.Value, Is.EqualTo(1));
            });
        }

        // A definition with a total caps the count at it, and the row still shows the state's count after a capped increase.
        [Test]
        public void ARowShowsTheCappedCountWhenTheDefinitionHasATotal()
        {
            WithRig(true, rig =>
            {
                Call(rig.Container, "Start");
                UIContainerDebugCollectableModifier orb = rig.Row("Orb");

                orb.Action_OnIncrease();
                orb.Action_OnIncrease();
                Assert.That(orb.Collected.Value, Is.EqualTo(10));
                orb.Action_OnIncrease();
                Assert.That((rig.State(CollectableType.Orb).Collected, orb.Collected.Value), Is.EqualTo((10, 10)));
            });
        }

        // 0x62F5A0..0x62F5B0: a type without a registered state changes nothing and the row's display is not written.
        [Test]
        public void ChangingATypeWithoutAStateLeavesTheRowAlone()
        {
            WithRig(false, rig =>
            {
                Call(rig.Container, "Start");
                UIContainerDebugCollectableModifier orb = rig.Row("Orb");

                Call(rig.Container, "OnChange", orb, CollectableType.Ring, true);

                Assert.That(orb.Collected.Value, Is.EqualTo(3));
                Assert.That(rig.Manager.TryGetCollectableState(CollectableType.Ring, out _), Is.False);
            });
        }

        // 0x62F1E0: the container closes through the UI manager with its own identifier.
        [Test]
        public void ClosingAsksTheUiManagerToCloseTheIdentifier()
        {
            Owned.Run(owned =>
            {
                UIManager manager = owned.Register(owned.Host("Lucid debug collectables ui manager", false).AddComponent<UIManager>());
                var identifier = owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                var container = owned.Host("Lucid debug collectables", false).AddComponent<UIContainerDebugCollectables>();
                Set(container, "m_debugCollectablesContainerIdentifier", identifier); // Reflection: serialized reference
                var closed = new List<UIContainerIdentifier>();
                manager.OnContainerClosed += closed.Add;

                container.Action_Close();

                Assert.That(closed, Is.EqualTo(new[] { identifier }));
            });
        }

        // 0x631730: every update publishes VisualQualityManager_SDT.FramesPerSecond (the shared "N0" text of the static count). The
        // count is a private static the manager's own update fills, so the case sets it and puts it back.
        [Test]
        public void TheFrameCounterPublishesTheStaticFrameRateEachUpdate()
        {
            FieldInfo frames = typeof(VisualQualityManager_SDT).GetField("s_framesPerSecond", BindingFlags.Static | BindingFlags.NonPublic);
            float prior = (float)frames.GetValue(null);
            try
            {
                Owned.Run(owned =>
                {
                    var counter = owned.Host("Lucid frame counter", false).AddComponent<UIContainerFPSDebug>();
                    frames.SetValue(null, 59.6f);
                    Call(counter, "Update");
                    Assert.That(counter.FPSCount.Value, Is.EqualTo("60"));
                    frames.SetValue(null, 144.4f);
                    Call(counter, "Update");
                    Assert.That(counter.FPSCount.Value, Is.EqualTo("144"));
                });
            }
            finally
            {
                frames.SetValue(null, prior);
            }
        }
    }
}
