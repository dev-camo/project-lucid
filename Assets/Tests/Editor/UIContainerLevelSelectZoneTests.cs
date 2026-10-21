using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.LevelSelectZoneTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerLevelSelectZone (Game.Runtime 0x020008c9; AddValueChangeListener 0x639030 with its closures 0x020008ca/0x020008cb,
    // UpdateDisabledLevelWidgets 0x638FD0, ToggleOn 0x639190, TrySelectBestLevel 0x639210, constructor 0x6397C0, the property accessors
    // 0x638EE0/0x638EF0) over real level widgets (see LevelSelectZoneTestSupport). Not covered: Setup, OnProgressionManagerValid,
    // OnSaveManagerValid and SetupLevels, which follow the progression and save managers (a deferred callback would stay in the
    // progression manager's row) and set up every widget from the progression totals, the save record of each level and the
    // level requirements.
    public sealed class UIContainerLevelSelectZoneTests
    {
        // dump.cs TypeDefIndex 2250/2248/2249 and the DummyDll rows (fields 0x04002327..0x04002330). The closure classes carry the
        // method ordinal 12 that follows from the member order (seven fields, the property and its accessors, Setup and
        // UpdateDisabledLevelWidgets precede AddValueChangeListener).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerLevelSelectZone), 0x00100101,
                new (string, int)[]
                {
                    ("m_widgetForGameplayLevelDefinitions", 0x0001), ("m_levelNumericalNameOffset", 0x0001), ("<HasAnyNewContent>k__BackingField", 0x0001),
                    ("m_parameters", 0x0001), ("m_progressionManager", 0x0001), ("m_saveManager", 0x0001), ("m_progressionManagerRef", 0x0021),
                },
                new (string, int)[]
                {
                    ("get_HasAnyNewContent", 0x0886), ("set_HasAnyNewContent", 0x0881), ("Setup", 0x00C6), ("UpdateDisabledLevelWidgets", 0x0086),
                    ("AddValueChangeListener", 0x0086), ("ToggleOn", 0x0086), ("TrySelectBestLevel", 0x0081), ("OnProgressionManagerValid", 0x0081),
                    ("OnSaveManagerValid", 0x0081), ("SetupLevels", 0x0081), (".ctor", 0x1886),
                });
            const BindingFlags nonPublic = BindingFlags.NonPublic;
            AssertMembers(typeof(UIContainerLevelSelectZone).GetNestedType("<>c__DisplayClass12_0", nonPublic), 0x00100103,
                new (string, int)[] { ("onValueChanged", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) });
            AssertMembers(typeof(UIContainerLevelSelectZone).GetNestedType("<>c__DisplayClass12_1", nonPublic), 0x00100103,
                new (string, int)[] { ("widgetForLevel", 0x0006), ("CS$<>8__locals1", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<AddValueChangeListener>b__0", 0x0083) });
        }

        // The constructor 0x6397C0 stores an empty widget array (Array.Empty, 0x639856), the numerical name offset 1 (0x639863) and
        // the progression manager reference; HasAnyNewContent starts false.
        [Test]
        public void ConstructionKeepsTheAuthoredDefaults()
        {
            Owned.Run(owned =>
            {
                LeaseRows(owned);
                owned.LeaseRow<DataManager>(); // the widgets' field initialiser
                var page = owned.Host("Lucid level select zone", false).AddComponent<UIContainerLevelSelectZone>();

                Assert.That(Get<UIWidgetForGameplayLevelDefinition[]>(page, "m_widgetForGameplayLevelDefinitions"), Is.Empty);
                Assert.That(Get<int>(page, "m_levelNumericalNameOffset"), Is.EqualTo(1));
                Assert.That(page.HasAnyNewContent, Is.False);
            });
        }

        // AddValueChangeListener 0x639030: every level widget's toggle reports to the listener with that widget's own level (the
        // closure over the foreach variable, 0x639910 passes [widgetForLevel + 0x10]), with the toggle's new value.
        [Test]
        public void EachWidgetReportsItsToggleWithItsOwnLevel()
        {
            Owned.Run(owned =>
            {
                LeaseRows(owned);
                owned.LeaseRow<DataManager>(); // the widgets' field initialiser
                LevelWidget first = NewLevelWidget(owned, "lucid-zone-first", false);
                LevelWidget second = NewLevelWidget(owned, "lucid-zone-second", false);
                UIContainerLevelSelectZone page = NewZonePage(owned, null, first, second);
                var log = new List<(bool value, string level)>();
                page.AddValueChangeListener((value, level) => log.Add((value, level.GetGUID())));

                second.Widget.Select(false);
                first.Widget.Select(false);
                second.Toggle.IsOn = false;

                Assert.That(log, Is.EqualTo(new[] { (true, "lucid-zone-second"), (true, "lucid-zone-first"), (false, "lucid-zone-second") }));
            });
        }

        // UpdateDisabledLevelWidgets 0x638FD0: every widget whose level is not the selected one (op_Equality at 0x63900B) is
        // deselected without notifying; the selected one is left alone, and no selected level deselects all.
        [Test]
        public void EveryWidgetButTheSelectedOneIsDeselectedQuietly()
        {
            Owned.Run(owned =>
            {
                LeaseRows(owned);
                owned.LeaseRow<DataManager>(); // the widgets' field initialiser
                LevelWidget[] widgets =
                {
                    NewLevelWidget(owned, "lucid-zone-a", false), NewLevelWidget(owned, "lucid-zone-b", false), NewLevelWidget(owned, "lucid-zone-c", false),
                };
                UIContainerLevelSelectZone page = NewZonePage(owned, null, widgets);
                foreach (LevelWidget widget in widgets) widget.Widget.Select(false);
                var log = new List<(bool value, string level)>();
                page.AddValueChangeListener((value, level) => log.Add((value, level.GetGUID())));

                page.UpdateDisabledLevelWidgets(widgets[1].Level);

                Assert.That(widgets.Select(widget => widget.Toggle.IsOn), Is.EqualTo(new[] { false, true, false }));
                Assert.That(log, Is.Empty);

                page.UpdateDisabledLevelWidgets(null);

                Assert.That(widgets.Select(widget => widget.Toggle.IsOn), Is.EqualTo(new[] { false, false, false }));
            });
        }

        private static bool[] Selected(params LevelWidget[] widgets) => widgets.Select(widget => widget.Toggle.IsOn).ToArray();

        // TrySelectBestLevel 0x639210: locked widgets are skipped before anything is compared (0x639290 jne); the first unlocked
        // widget whose level is the preselected one is selected at once (0x6392AA jne to the select) and the loop ends;
        // otherwise the last unlocked widget is selected.
        [Test]
        public void ThePreselectedUnlockedLevelWinsOtherwiseTheLastUnlockedLevel()
        {
            // (preselected index or -1, locked flags) -> selected flags
            foreach ((int preselected, bool[] locked, bool[] expected) in new[]
            {
                (1, new[] { false, false, false, false }, new[] { false, true, false, false }),
                (0, new[] { false, false, false, false }, new[] { true, false, false, false }),
                (-1, new[] { false, false, false, false }, new[] { false, false, false, true }),
                (1, new[] { false, true, false, false }, new[] { false, false, false, true }),
                (-1, new[] { false, false, true, true }, new[] { false, true, false, false }),
                (-1, new[] { true, true, true, true }, new[] { false, false, false, false }),
            })
            {
                Owned.Run(owned =>
                {
                    LeaseRows(owned);
                owned.LeaseRow<DataManager>(); // the widgets' field initialiser
                    LevelWidget[] widgets = locked.Select((isLocked, i) => NewLevelWidget(owned, "lucid-zone-" + i, isLocked)).ToArray();
                    UIContainerLevelSelectZone page = NewZonePage(owned, preselected < 0 ? null : widgets[preselected].Level, widgets);
                    string row = "preselected " + preselected + ", locked " + string.Join(",", locked);

                    Call(page, "TrySelectBestLevel");

                    Assert.That(Selected(widgets), Is.EqualTo(expected), row);
                });
            }
        }

        // ToggleOn 0x639190: every widget's navigation follows the flag unless the widget is locked (ToggleWidgetNavigation), and
        // only turning it on goes on to select the best level (0x6391E0 test, jump to 0x639210).
        [Test]
        public void TurningThePageOnEnablesTheNavigationOfUnlockedLevelsAndSelectsOne()
        {
            Owned.Run(owned =>
            {
                LeaseRows(owned);
                owned.LeaseRow<DataManager>(); // the widgets' field initialiser
                LevelWidget[] widgets =
                {
                    NewLevelWidget(owned, "lucid-zone-a", false), NewLevelWidget(owned, "lucid-zone-b", true), NewLevelWidget(owned, "lucid-zone-c", false),
                };
                UIContainerLevelSelectZone page = NewZonePage(owned, null, widgets);

                page.ToggleOn(true);

                Assert.That(widgets.Select(widget => widget.Navigation.enabled), Is.EqualTo(new[] { true, false, true }));
                Assert.That(Selected(widgets), Is.EqualTo(new[] { false, false, true }), "the last unlocked level");

                widgets[2].Toggle.IsOn = false;
                page.ToggleOn(false);

                Assert.That(widgets.Select(widget => widget.Navigation.enabled), Is.EqualTo(new[] { false, false, false }));
                Assert.That(Selected(widgets), Is.EqualTo(new[] { false, false, false }), "turning off selects nothing");
            });
        }
    }
}
