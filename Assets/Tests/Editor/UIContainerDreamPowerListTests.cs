using System;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.DreamPowerStoreScenes;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerDreamPowerList (Game.Runtime 0x020008b4, x86-64 0x62F7B0..0x62FC90) as a real component on an owned inactive host
    // over the dream power store scene: a registered DreamPowerStoreManager (available, as it has no unlock requirement), the open
    // save, the DataManager's dream powers and three real power description widgets.
    public sealed class UIContainerDreamPowerListTests
    {
        private const Strings FirstName = (Strings)9301, SecondName = (Strings)9302, ThirdName = (Strings)9303;

        private sealed class Rig
        {
            public World World;
            public UIContainerDreamPowerList List;
            public UIWidgetDreamPowerDescription[] Widgets;
            public DreamPowerDefinition[] Powers;
        }

        private static void Run(Action<Rig> body) => DreamPowerStoreScenes.Run(world =>
        {
            Owned owned = world.Scene.Owned;
            var rig = new Rig { World = world, Widgets = new UIWidgetDreamPowerDescription[3], Powers = new DreamPowerDefinition[3] };
            rig.List = owned.Host("Lucid dream power list", false).AddComponent<UIContainerDreamPowerList>();
            string[] fields = { "m_powerDescription1", "m_powerDescription2", "m_powerDescription3" };
            Strings[] names = { FirstName, SecondName, ThirdName };
            for (int i = 0; i < 3; i++)
            {
                rig.Widgets[i] = owned.Host("Lucid dream power description " + i, false).AddComponent<UIWidgetDreamPowerDescription>();
                Set(rig.List, fields[i], rig.Widgets[i]); // Reflection: serialized reference the editor assigns
                rig.Powers[i] = NewDefinition(owned, "lucid-list-power-" + i, names[i], NewSprite(owned));
                world.Scene.Data.DreamPowerDefinitions[rig.Powers[i].GetGUID()] = rig.Powers[i];
            }
            body(rig);
        });

        private static void Save(Rig rig, CharacterArchetype archetype, int slot, string guid) =>
            rig.World.Scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(archetype).Set(slot, guid);

        // Start 0x62F7B0 takes the DataManager, SaveManager, MissionManager and DreamPowerStoreManager from the registry
        // (0x62F82E, 0x62F854, 0x62F87A, 0x62F8A0) and runs LoadDreamPowerSlots. Without an active mission the loadout is the one of
        // the default archetype: each of the three widgets gets the power saved in its slot (PopulateSlot 0x62FAB0 is called with
        // slots 0, 1, 2 for the widgets at +0x38, +0x40, +0x48), and a slot that is empty or names an unknown power gets an empty
        // widget (a null definition) instead of faulting.
        [Test]
        public void StartShowsTheSavedLoadoutOfTheDefaultArchetypeInThreeWidgets()
        {
            Run(rig =>
            {
                Save(rig, CharacterArchetype.None, 0, rig.Powers[2].GetGUID());
                Save(rig, CharacterArchetype.None, 2, "lucid-unknown-power");

                Call(rig.List, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(Get<DataManager>(rig.List, "m_dataManager"), Is.SameAs(rig.World.Scene.Data));
                Assert.That(Get<SaveManager>(rig.List, "m_saveManager"), Is.SameAs(rig.World.Scene.Save));
                Assert.That(Get<MissionManager>(rig.List, "m_missionManager"), Is.SameAs(rig.World.Scene.Missions));
                Assert.That(Get<DreamPowerStoreManager>(rig.List, "m_dreamPowerStoreManager"), Is.SameAs(rig.World.Store));
                Assert.That(rig.List.Visible.Value, Is.True);
                Assert.That(rig.Widgets[0].Empty.Value, Is.False);
                Assert.That(rig.Widgets[0].Name.Value, Is.EqualTo(ThirdName), "slot 0 holds the third power");
                Assert.That(rig.Widgets[1].Empty.Value, Is.True, "slot 1 is empty");
                Assert.That(rig.Widgets[2].Empty.Value, Is.True, "slot 2 names a power that no longer exists");
            });
        }

        // With an active mission that allows dream powers the loadout is the one of the mission's archetype (+0x98 of the definition,
        // 0x62F9A7); the default archetype's loadout is not shown.
        [Test]
        public void ADreamPowerMissionShowsTheLoadoutOfItsArchetype()
        {
            Run(rig =>
            {
                ActivateMission(rig.World.Scene, CharacterArchetype.Climbing, true);
                Save(rig, CharacterArchetype.None, 0, rig.Powers[0].GetGUID());
                Save(rig, CharacterArchetype.Climbing, 1, rig.Powers[1].GetGUID());

                Call(rig.List, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(rig.List.Visible.Value, Is.True);
                Assert.That(rig.Widgets[0].Empty.Value, Is.True, "the climbing loadout has nothing in slot 0");
                Assert.That(rig.Widgets[1].Empty.Value, Is.False);
                Assert.That(rig.Widgets[1].Name.Value, Is.EqualTo(SecondName));
            });
        }

        // A mission without dream powers (EnableDreamPowers false at 0x62F9A5) hides the list before any loadout is read: the
        // widgets are left as they were.
        [Test]
        public void AMissionWithoutDreamPowersHidesTheList()
        {
            Run(rig =>
            {
                ActivateMission(rig.World.Scene, CharacterArchetype.Climbing, false);
                Save(rig, CharacterArchetype.Climbing, 0, rig.Powers[0].GetGUID());
                rig.List.Visible.Value = true;

                Call(rig.List, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(rig.List.Visible.Value, Is.False);
                Assert.That(rig.Widgets[0].Empty.Value, Is.False, "the widget was not set up");
                Assert.That(rig.Widgets[0].Name.Value, Is.EqualTo(default(Strings)));
            });
        }

        // An unavailable store (IsAvailable false at 0x62F915) hides the list without reading a loadout. The store is available when
        // the authored requirements hold or a power is already bought.
        [Test]
        public void AnUnavailableStoreHidesTheList()
        {
            Run(rig =>
            {
                var requirement = rig.World.Scene.Owned.Track(ScriptableObject.CreateInstance<DreamPowerStoreFailingRequirement>());
                Set(rig.World.Store, "m_unlockRequirements", new RequirementGameplayLevelBase[] { requirement }); // Reflection: authored serialized field
                Save(rig, CharacterArchetype.None, 0, rig.Powers[0].GetGUID());
                rig.List.Visible.Value = true;

                Call(rig.List, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(rig.List.Visible.Value, Is.False);
                Assert.That(rig.Widgets[0].Name.Value, Is.EqualTo(default(Strings)));

                Buy(rig.World, rig.Powers[1]);
                WriteStoreItems(rig.World);
                Call(rig.List, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(rig.List.Visible.Value, Is.True, "a bought power opens the store");
                Assert.That(rig.Widgets[0].Name.Value, Is.EqualTo(FirstName));
            });
        }

        // A loadout without any slot (Count 0 at 0x62FA5E) hides the list as well.
        [Test]
        public void AnEmptyLoadoutHidesTheList()
        {
            Run(rig =>
            {
                rig.World.Scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(CharacterArchetype.None);
                rig.List.Visible.Value = true;

                Call(rig.List, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(rig.List.Visible.Value, Is.False);
                Assert.That(rig.Widgets[0].Empty.Value, Is.False, "the widgets were not set up");
            });
        }
    }
}
