using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.LevelSelectZoneTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerLevelSelectZones (Game.Runtime 0x020008cd; UpdateZone 0x63A430, GetZones 0x63A4E0, HideLockedZones 0x63A660,
    // SelectZone 0x63A920, SelectLevel 0x63AB90) and its nested types (0x020008ce..0x020008d2) over real zone pages and level
    // widgets (see LevelSelectZoneTestSupport). Not covered: Setup, OnLevelManagerValid, OnSaveManagerValid, SetupZones and the
    // closures it makes (SetupZones calls Setup on every zone page, which leaves a deferred callback in the progression manager's
    // row, and on a pagination toggle widget, and finds the meta-game level through the level manager's tables), AnimatePagination
    // (Animation.Play on an inactive host).
    public sealed class UIContainerLevelSelectZonesTests
    {
        private const BindingFlags NonPublic = BindingFlags.NonPublic;
        private static readonly Type ContainerForZone = typeof(UIContainerLevelSelectZones).GetNestedType("UIContainerForZone", NonPublic);
        private static readonly Type ToggleForZone = typeof(UIContainerLevelSelectZones).GetNestedType("UIToggleForZone", NonPublic);
        private static readonly Type LevelSelectZone = typeof(UIContainerLevelSelectZones).GetNestedType("LevelSelectZone", NonPublic);

        private sealed class Page
        {
            internal GameplayLevelCategory Zone;
            internal UIContainerLevelSelectZone Container;
            internal LevelWidget[] Widgets;
        }

        private sealed class Rig
        {
            internal Owned Owned;
            internal UIContainerLevelSelectZones Zones;
            internal UIContainerOrbCollectableCounter Counter;
            internal CollectableDefinition CounterReward;
            internal Page[] Pages;
            internal readonly List<GameplayLevelDefinition> LevelsSelected = new List<GameplayLevelDefinition>();
            internal readonly List<GameplayLevelCategory> ZonesSelected = new List<GameplayLevelCategory>();
        }

        // A zone container over pages of the given widgets. A locked page's zone fails the (always failing) requirement
        // RequirementCategoryAlwaysHidden.
        private static void Run(bool[] lockedZones, int widgetsPerPage, Action<Rig> body) => MissionTrackerTestSupport.WithWorld(world =>
        {
            Owned owned = world.Owned;
            LeaseRows(owned);
            // The zones' reward type comes from the level manager's (empty) game levels, which its Awake would fetch.
            typeof(LevelManager).GetProperty("GameLevels").GetSetMethod(true).Invoke(world.Levels, new object[] { owned.Track(ScriptableObject.CreateInstance<GameLevels>()) });
            DataManager data = owned.Register(owned.Host("Lucid data manager", false).AddComponent<DataManager>()); // the widgets' and counter's field initialiser
            var rig = new Rig { Owned = owned, Zones = owned.Host("Lucid level select zones", false).AddComponent<UIContainerLevelSelectZones>() };
            rig.CounterReward = owned.Track(ScriptableObject.CreateInstance<CollectableDefinition>());
            data.CollectableDefinitions[CollectableType.Orb] = rig.CounterReward;
            rig.Counter = owned.Host("Lucid collectable counter", false).AddComponent<UIContainerOrbCollectableCounter>();
            Set(rig.Counter, "m_rewardType", CollectableType.Orb); // Reflection: serialized option
            Set(rig.CounterReward, "m_refCount", 3); // Reflection: the icon references earlier loads of the counter hold
            Set(rig.Zones, "m_collectableCounter", rig.Counter); // Reflection: serialized reference
            var requirement = owned.Track(ScriptableObject.CreateInstance<RequirementCategoryAlwaysHidden>());
            rig.Pages = new Page[lockedZones.Length];
            Array entries = Array.CreateInstance(ContainerForZone, lockedZones.Length);
            for (int i = 0; i < lockedZones.Length; i++)
            {
                var zone = owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>());
                Set(zone, "m_guid", "lucid-zone-" + i); // Reflection: authored GUID of a serialized definition
                Set(zone, "m_requirements", lockedZones[i] ? new List<RequirementLevelCategoryBase> { requirement } : new List<RequirementLevelCategoryBase>());
                LevelWidget[] widgets = Enumerable.Range(0, widgetsPerPage).Select(w => NewLevelWidget(owned, "lucid-level-" + i + "-" + w, false)).ToArray();
                UIContainerLevelSelectZone page = NewZonePage(owned, null, widgets);
                page.gameObject.SetActive(true);
                rig.Pages[i] = new Page { Zone = zone, Container = page, Widgets = widgets };
                object entry = Activator.CreateInstance(ContainerForZone, true);
                Set(entry, "m_zone", zone); // Reflection: serialized fields of the nested serializable class
                Set(entry, "m_container", page);
                entries.SetValue(entry, i);
            }
            Set(rig.Zones, "m_zoneContainers", entries); // Reflection: serialized array
            Set(rig.Zones, "m_parameters", new UIContainerLevelSelectZonesParameters(level => rig.LevelsSelected.Add(level), zone => rig.ZonesSelected.Add(zone))); // Reflection: stored by Setup
            body(rig);
        });

        private static T Prop<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);

        // dump.cs TypeDefIndex 2257, 2252..2256 and the DummyDll rows (fields 0x04002333..0x04002349).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerLevelSelectZones), 0x00100101,
                new (string, int)[]
                {
                    ("m_zoneContainers", 0x0001), ("m_zoneToggles", 0x0001), ("m_zoneToggleGroup", 0x0001), ("m_paginationAnimation", 0x0001),
                    ("m_paginationAnimations", 0x0001), ("m_hiddenRequirement", 0x0001), ("m_collectableCounter", 0x0001), ("CanSelectPrevious", 0x0026),
                    ("CanSelectNext", 0x0026), ("m_parameters", 0x0001), ("m_levelManager", 0x0001), ("m_saveManager", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00C6), ("OnLevelManagerValid", 0x0081), ("OnSaveManagerValid", 0x0081), ("UpdateZone", 0x0086), ("SetupZones", 0x0081),
                    ("GetZones", 0x0081), ("HideLockedZones", 0x0081), ("SelectZone", 0x0081), ("SelectLevel", 0x0081), ("AnimatePagination", 0x0081),
                    ("OnZoneToggleGroupChanged", 0x0081), (".ctor", 0x1886),
                });
            AssertMembers(ToggleForZone, 0x00102103, new (string, int)[] { ("m_zone", 0x0001), ("m_toggle", 0x0001) },
                new (string, int)[] { ("get_Zone", 0x0886), ("get_Toggle", 0x0886), (".ctor", 0x1886) });
            AssertMembers(ContainerForZone, 0x00102103, new (string, int)[] { ("m_zone", 0x0001), ("m_container", 0x0001) },
                new (string, int)[] { ("get_Zone", 0x0886), ("get_Container", 0x0886), (".ctor", 0x1886) });
            AssertMembers(LevelSelectZone, 0x00100103,
                new (string, int)[] { ("<Zone>k__BackingField", 0x0021), ("<Container>k__BackingField", 0x0021), ("<Locked>k__BackingField", 0x0021) },
                new (string, int)[] { ("get_Zone", 0x0886), ("get_Container", 0x0886), ("get_Locked", 0x0886), (".ctor", 0x1886) });
            AssertMembers(typeof(UIContainerLevelSelectZones).GetNestedType("PaginationAnimation", NonPublic), 0x00102103,
                new (string, int)[] { ("m_zone", 0x0001), ("m_animationClip", 0x0001) },
                new (string, int)[] { ("get_Zone", 0x0886), ("get_AnimationClip", 0x0886), (".ctor", 0x1886) });
            AssertMembers(typeof(UIContainerLevelSelectZones).GetNestedType("<>c__DisplayClass16_0", NonPublic), 0x00100103,
                new (string, int)[] { ("zone", 0x0006), ("<>4__this", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<SetupZones>b__0", 0x0083), ("<SetupZones>b__1", 0x0083) });
        }

        // GetZones 0x63A4E0 lists the zone pages in order with their zones, and a zone is locked when it does not meet all of its
        // requirements (xor 1 of MeetsAllRequirements at 0x63A5B1). HideLockedZones 0x63A660 deactivates the page of each locked zone.
        [Test]
        public void LockedZonesAreListedAsLockedAndTheirPagesHidden()
        {
            Run(new[] { false, true, false }, 1, rig =>
            {
                var zones = (IList)Call(rig.Zones, "GetZones");

                Assert.That(zones.Count, Is.EqualTo(3));
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(Prop<GameplayLevelCategory>(zones[i], "Zone"), Is.SameAs(rig.Pages[i].Zone), "zone " + i);
                    Assert.That(Prop<UIContainerLevelSelectZone>(zones[i], "Container"), Is.SameAs(rig.Pages[i].Container), "container " + i);
                    Assert.That(Prop<bool>(zones[i], "Locked"), Is.EqualTo(i == 1), "locked " + i);
                }

                Call(rig.Zones, "HideLockedZones", zones);

                Assert.That(rig.Pages.Select(page => page.Container.gameObject.activeSelf), Is.EqualTo(new[] { true, false, true }));
            });
        }

        // UpdateZone 0x63A430 turns each page on only when its zone is the selected one (op_Equality at 0x63A483): its level
        // navigation is enabled and a level selected; every other page's navigation is disabled.
        [Test]
        public void UpdatingTheZoneTurnsOnlyThatPageOn()
        {
            Run(new[] { false, false, false }, 2, rig =>
            {
                foreach (Page page in rig.Pages)
                    foreach (LevelWidget widget in page.Widgets) widget.Navigation.enabled = true;

                rig.Zones.UpdateZone(rig.Pages[1].Zone);

                Assert.That(rig.Pages.Select(page => page.Widgets.All(widget => widget.Navigation.enabled)), Is.EqualTo(new[] { false, true, false }));
                Assert.That(rig.Pages.Select(page => page.Widgets.Any(widget => widget.Navigation.enabled)), Is.EqualTo(new[] { false, true, false }));
                Assert.That(rig.Pages[1].Widgets.Select(widget => widget.Toggle.IsOn), Is.EqualTo(new[] { false, true }), "the last unlocked level");
                Assert.That(rig.Pages.Where((page, i) => i != 1).SelectMany(page => page.Widgets).Any(widget => widget.Toggle.IsOn), Is.False);
            });
        }

        // SelectLevel 0x63AB90: a deselection is ignored; a selection is announced to the owner's callback and every other level of
        // every page is deselected quietly.
        [Test]
        public void ASelectedLevelIsAnnouncedAndEveryOtherLevelDeselected()
        {
            Run(new[] { false, false }, 2, rig =>
            {
                foreach (LevelWidget widget in rig.Pages.SelectMany(page => page.Widgets)) widget.Widget.Select(false);
                GameplayLevelDefinition level = rig.Pages[1].Widgets[0].Level;

                Call(rig.Zones, "SelectLevel", false, level);

                Assert.That(rig.LevelsSelected, Is.Empty);
                Assert.That(rig.Pages.SelectMany(page => page.Widgets).All(widget => widget.Toggle.IsOn), Is.True);

                Call(rig.Zones, "SelectLevel", true, level);

                Assert.That(rig.LevelsSelected, Is.EqualTo(new[] { level }));
                Assert.That(rig.Pages.SelectMany(page => page.Widgets).Select(widget => widget.Toggle.IsOn), Is.EqualTo(new[] { false, false, true, false }));
            });
        }

        private static UIWidgetTogglePagination NewPaginationToggle(Owned owned, out UIToggle toggle)
        {
            GameObject host = owned.Host("Lucid pagination toggle", false);
            var widget = host.AddComponent<UIWidgetTogglePagination>();
            toggle = host.GetComponent<UIToggle>() ?? host.AddComponent<UIToggle>();
            var transition = host.AddComponent<UITransitionAnimation>();
            Set(transition, "m_settings", new List<UITransitionAnimationSetting>()); // Reflection: authored (empty) transition settings
            Set(toggle, "m_transition", transition); // Reflection: serialized references
            Set(widget, "m_toggle", toggle);
            return widget;
        }

        private static void SetToggles(Rig rig, params (GameplayLevelCategory zone, UIWidgetTogglePagination toggle)[] toggles)
        {
            Array entries = Array.CreateInstance(ToggleForZone, toggles.Length);
            for (int i = 0; i < toggles.Length; i++)
            {
                object entry = Activator.CreateInstance(ToggleForZone, true);
                Set(entry, "m_zone", toggles[i].zone); // Reflection: serialized fields of the nested serializable class
                Set(entry, "m_toggle", toggles[i].toggle);
                entries.SetValue(entry, i);
            }
            Set(rig.Zones, "m_zoneToggles", entries); // Reflection: serialized array
        }

        // SelectZone 0x63A920: a deselection, or a zone that no pagination toggle shows, does nothing. For every toggle of the
        // selected zone (the loop has no early exit, 0x63A97A) the pages are turned on or off by zone, the toggle is selected and
        // the owner is told the zone, so two toggles for one zone announce it twice.
        [Test]
        public void SelectingAZoneSelectsItsToggleTurnsItsPageOnAndAnnouncesIt()
        {
            Run(new[] { false, false, false }, 2, rig =>
            {
                UIWidgetTogglePagination first = NewPaginationToggle(rig.Owned, out UIToggle firstToggle);
                UIWidgetTogglePagination second = NewPaginationToggle(rig.Owned, out UIToggle secondToggle);
                UIWidgetTogglePagination other = NewPaginationToggle(rig.Owned, out UIToggle otherToggle);
                SetToggles(rig, (rig.Pages[1].Zone, first), (rig.Pages[0].Zone, other), (rig.Pages[1].Zone, second));
                foreach (Page page in rig.Pages)
                    foreach (LevelWidget widget in page.Widgets) widget.Navigation.enabled = true;

                Call(rig.Zones, "SelectZone", false, rig.Pages[1].Zone);
                Call(rig.Zones, "SelectZone", true, rig.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelCategory>()));

                Assert.That(rig.ZonesSelected, Is.Empty);
                Assert.That(new[] { firstToggle, otherToggle, secondToggle }.Select(toggle => toggle.IsOn), Is.EqualTo(new[] { false, false, false }));
                Assert.That(rig.Pages.All(page => page.Widgets.All(widget => widget.Navigation.enabled)), Is.True, "no page was touched");
                Assert.That(Get<CollectableType>(rig.Counter, "m_rewardType"), Is.EqualTo(CollectableType.Orb), "the counter was not touched");

                Call(rig.Zones, "SelectZone", true, rig.Pages[1].Zone);

                Assert.That(new[] { firstToggle, otherToggle, secondToggle }.Select(toggle => toggle.IsOn), Is.EqualTo(new[] { true, false, true }));
                Assert.That(rig.ZonesSelected, Is.EqualTo(new[] { rig.Pages[1].Zone, rig.Pages[1].Zone }));
                Assert.That(rig.Pages.Select(page => page.Widgets.All(widget => widget.Navigation.enabled)), Is.EqualTo(new[] { false, true, false }));
                Assert.That(rig.Pages[1].Widgets.Select(widget => widget.Toggle.IsOn), Is.EqualTo(new[] { false, true }), "a level of the selected page is chosen");
                Assert.That(Get<CollectableType>(rig.Counter, "m_rewardType"), Is.EqualTo(CollectableType.None), "the counter takes the zone's reward type (none here)");
                Assert.That(Get<int>(rig.CounterReward, "m_refCount"), Is.EqualTo(2), "and released the icon of the type it had");
            });
        }

        // OnZoneToggleGroupChanged 0x639CA0: the group's first active toggle index and toggle count give the two neighbour flags,
        // (index != 0) & (count >= 2) and (index != count - 1) & (count >= 2). With no toggle on the index is -1, which differs from
        // both ends, so both flags hold whenever there are two toggles or more.
        [Test]
        public void TheNeighbourFlagsFollowTheActiveToggleAndTheToggleCount()
        {
            // (toggle count, active index or -1) -> (can select previous, can select next)
            foreach ((int count, int active, bool previous, bool next) in new[]
            {
                (0, -1, false, false),
                (1, 0, false, false),
                (2, 0, false, true),
                (2, 1, true, false),
                (3, 1, true, true),
                (3, 2, true, false),
                (3, -1, true, true),
            })
            {
                Run(new bool[0], 0, rig =>
                {
                    var group = rig.Owned.Host("Lucid zone toggle group", false).AddComponent<UIToggleGroup>();
                    for (int i = 0; i < count; i++)
                    {
                        GameObject host = rig.Owned.Host("Lucid zone toggle " + i, false);
                        var element = host.AddComponent<UIToggleGroupElement>();
                        var toggle = host.GetComponent<UIToggle>();
                        var transition = host.AddComponent<UITransitionAnimation>();
                        Set(transition, "m_settings", new List<UITransitionAnimationSetting>()); // Reflection: authored (empty) transition settings
                        Set(toggle, "m_transition", transition); // Reflection: serialized references
                        Set(element, "m_toggle", toggle);
                        toggle.SetIsOnWithoutNotify(i == active);
                        group.RegisterToggle(element);
                    }
                    Set(rig.Zones, "m_zoneToggleGroup", group); // Reflection: serialized reference
                    string row = count + " toggles, active " + active;

                    Call(rig.Zones, "OnZoneToggleGroupChanged");

                    Assert.That(rig.Zones.CanSelectPrevious.Value, Is.EqualTo(previous), row);
                    Assert.That(rig.Zones.CanSelectNext.Value, Is.EqualTo(next), row);
                });
            }
        }

        // The constructor 0x63AF30 only creates the two bindings (both start false).
        [Test]
        public void TheBindingsStartFalse()
        {
            Run(new bool[0], 0, rig =>
            {
                Assert.That(rig.Zones.CanSelectPrevious.Value, Is.False);
                Assert.That(rig.Zones.CanSelectNext.Value, Is.False);
            });
        }
    }
}
