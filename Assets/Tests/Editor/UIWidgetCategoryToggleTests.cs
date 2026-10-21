using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetCategoryToggle (Game.Runtime 0x020009a6, x86-64 0x69BC10..0x69C1D0) as a real component on an inactive owned host over a
    // real OrnamentManager (built by its own constructor, registered into its absent row) whose ornament table the case fills through
    // the manager's own dictionary. The serialized sprites are authored data. The base toggle callback is only set by
    // UIWidgetToggle.Setup, which this widget does not call, so that one private field is written directly.
    public sealed class UIWidgetCategoryToggleTests
    {
        private sealed class Scene
        {
            public OrnamentManager Manager;
            public UIWidgetCategoryToggle Toggle;
            public Sprite SelectedBackground, UnselectedBackground, SelectedIcon, UnselectedIcon;
            public Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus> Story;
        }

        private static void Run(Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            // The manager constructor subscribes Initialise and Shutdown (unregistering removes those), and its MissionManager
            // reference is cached.
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<MissionManager>();
            var scene = new Scene { Manager = new OrnamentManager() };
            owned.Register(scene.Manager);
            scene.Story = new Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus>(HardlightProject.HardlightEnumComparers.OrnamentIdentifierComparer);
            ((Dictionary<OrnamentCategory, Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus>>)scene.Manager.OrnamentCollections)
                [OrnamentCategory.Story] = scene.Story;
            scene.Toggle = owned.Host("Lucid category toggle", false).AddComponent<UIWidgetCategoryToggle>();
            Set(scene.Toggle, "m_selectedBackground", scene.SelectedBackground = NewSprite(owned));
            Set(scene.Toggle, "m_unselectedBackground", scene.UnselectedBackground = NewSprite(owned));
            Set(scene.Toggle, "m_selectedIcon", scene.SelectedIcon = NewSprite(owned));
            Set(scene.Toggle, "m_unselectedIcon", scene.UnselectedIcon = NewSprite(owned));
            body(owned, scene);
        });

        private static UIWidgetCategoryToggleParameters Parameters(bool selected, int collected = 0, int total = 0) =>
            new UIWidgetCategoryToggleParameters(selected, collected, total, OrnamentCategory.Story);

        private static OrnamentManager.OrnamentStatus Status(bool unlocked, bool seen, bool collected) =>
            new OrnamentManager.OrnamentStatus(unlocked, seen, collected);

        // dump.cs TypeDefIndex 2469 and the DummyDll rows (fields 0x04002867..0x04002873).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetCategoryToggle), 0x00100001,
                new (string, int)[]
                {
                    ("m_selectedBackground", 0x0001), ("m_unselectedBackground", 0x0001), ("m_selectedIcon", 0x0001), ("m_unselectedIcon", 0x0001),
                    ("IsSelectedBackground", 0x0026), ("IsSelectedIcon", 0x0026), ("IsComplete", 0x0026), ("HasNewUnlock", 0x0026),
                    ("Collected", 0x0026), ("Total", 0x0026), ("m_hasAnyNew", 0x0001), ("m_category", 0x0001), ("m_ornamentManager", 0x0001),
                },
                new (string, int)[] { ("Setup", 0x00c6), ("Action_OnValueChanged", 0x00c6), ("HasAnyNew", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetCategoryToggle), 2, 1);
            Assert.That(typeof(UIWidgetCategoryToggle).BaseType, Is.EqualTo(typeof(UIWidgetToggle)));
            foreach (string field in new[] { "m_selectedBackground", "m_unselectedBackground", "m_selectedIcon", "m_unselectedIcon" })
                Assert.That(AttributeTypes(typeof(UIWidgetCategoryToggle).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
        }

        // Setup 0x69BC10 (the UIWidgetToggle setup is not called): the selected flag (+0x10) picks the art pair; the counts are published
        // and IsComplete is Collected == Total (sete, 0x69BCEC), which holds for 0 of 0.
        [TestCase(true, 2, 5, false, TestName = "ASelectedIncompleteTabShowsTheSelectedArt")]
        [TestCase(false, 5, 5, true, TestName = "AnUnselectedCompleteTabShowsTheUnselectedArt")]
        [TestCase(true, 0, 0, true, TestName = "AnEmptyCategoryCountsAsComplete")]
        public void SetupPublishesTheArtAndTheCounts(bool selected, int collected, int total, bool complete)
        {
            Run((owned, scene) =>
            {
                scene.Toggle.Setup(Parameters(selected, collected, total));

                Assert.That(scene.Toggle.IsSelectedBackground.Value, Is.SameAs(selected ? scene.SelectedBackground : scene.UnselectedBackground));
                Assert.That(scene.Toggle.IsSelectedIcon.Value, Is.SameAs(selected ? scene.SelectedIcon : scene.UnselectedIcon));
                Assert.That(scene.Toggle.Collected.Value, Is.EqualTo(collected));
                Assert.That(scene.Toggle.Total.Value, Is.EqualTo(total));
                Assert.That(scene.Toggle.IsComplete.Value, Is.EqualTo(complete));
            });
        }

        // HasAnyNew 0x69BD70 (called by Setup with the tab's category) looks for an ornament that is unlocked and collected but not seen
        // (0x69BF0F: unlocked, 0x69BF15: collected, 0x69BF1D: not seen). Rows (statuses, new unlock) separate that from an unlock test
        // that ignores collection, one that ignores the seen flag, and an any/all difference.
        [Test]
        public void SetupFlagsANewUnlockOnlyForAnAvailableUnseenOrnament()
        {
            var rows = new (OrnamentManager.OrnamentStatus[] statuses, bool expected, string label)[]
            {
                (new OrnamentManager.OrnamentStatus[0], false, "no ornaments"),
                (new[] { Status(true, false, true) }, true, "unlocked, unseen, collected"),
                (new[] { Status(true, true, true) }, false, "seen"),
                (new[] { Status(false, false, true) }, false, "locked"),
                (new[] { Status(true, false, false) }, false, "not collected"),
                (new[] { Status(true, true, true), Status(true, false, true) }, true, "one new among seen ones"),
                (new[] { Status(true, false, true), Status(false, false, false) }, true, "a new one before a locked one"),
            };
            foreach (var row in rows)
            {
                Run((owned, scene) =>
                {
                    var ids = (OrnamentIdentifier[])Enum.GetValues(typeof(OrnamentIdentifier));
                    for (int i = 0; i < row.statuses.Length; i++)
                        scene.Story[ids[i + 1]] = row.statuses[i];

                    scene.Toggle.Setup(Parameters(true));

                    Assert.That(scene.Toggle.HasNewUnlock.Value, Is.EqualTo(row.expected), row.label);
                });
            }
        }

        // 0x69BE03..0x69BE17: a category other than the tab's, or a tab whose flag is already cleared, returns without rescanning.
        // Setup arms the flag; the first matching call rescans and clears it when nothing is new, after which later calls do nothing.
        [Test]
        public void HasAnyNewRescansOnlyForItsCategoryWhileTheFlagIsSet()
        {
            Run((owned, scene) =>
            {
                OrnamentIdentifier ornament = ((OrnamentIdentifier[])Enum.GetValues(typeof(OrnamentIdentifier)))[1];
                scene.Story[ornament] = Status(true, false, true);
                scene.Toggle.Setup(Parameters(false));
                Assert.That(scene.Toggle.HasNewUnlock.Value, Is.True);

                scene.Story[ornament] = Status(true, true, true);
                scene.Toggle.HasAnyNew(OrnamentCategory.Challenge);
                Assert.That(scene.Toggle.HasNewUnlock.Value, Is.True, "another category is ignored");

                scene.Toggle.HasAnyNew(OrnamentCategory.Story);
                Assert.That(scene.Toggle.HasNewUnlock.Value, Is.False, "the rescan found nothing new");

                scene.Story[ornament] = Status(true, false, true);
                scene.Toggle.HasAnyNew(OrnamentCategory.Story);
                Assert.That(scene.Toggle.HasNewUnlock.Value, Is.False, "the cleared flag stops further rescans");
            });
        }

        // Action_OnValueChanged 0x69BFD0 first runs the base (the callback Setup would have stored), then swaps the art pair.
        [Test]
        public void ChangingTheValueRunsTheBaseCallbackAndSwapsTheArt()
        {
            Run((owned, scene) =>
            {
                var received = new List<bool>();
                Set(scene.Toggle, "m_callback", new Action<bool>(received.Add)); // the field UIWidgetToggle.Setup would fill

                scene.Toggle.Action_OnValueChanged(true);
                Assert.That(scene.Toggle.IsSelectedBackground.Value, Is.SameAs(scene.SelectedBackground));
                Assert.That(scene.Toggle.IsSelectedIcon.Value, Is.SameAs(scene.SelectedIcon));

                scene.Toggle.Action_OnValueChanged(false);
                Assert.That(scene.Toggle.IsSelectedBackground.Value, Is.SameAs(scene.UnselectedBackground));
                Assert.That(scene.Toggle.IsSelectedIcon.Value, Is.SameAs(scene.UnselectedIcon));
                Assert.That(received, Is.EqualTo(new[] { true, false }));
            });
        }
    }
}
