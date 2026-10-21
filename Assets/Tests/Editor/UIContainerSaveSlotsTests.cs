using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSaveSlots (Game.Runtime 0x02000920, x86-64 0x678E10..0x6794B9) as a real component on an owned inactive host,
    // over the real SaveManager, App and a real ProgressionManager whose per-slot progress is cached empty (LevelRequirementWorld),
    // with three real UIWidgetToggleSaveData widgets. The UINavigationManager row is left empty, so the navigation step of
    // TryNavigateToWidget (0x679181) returns at the IsNull test (0x679178).
    // Action_SelectSave (0x6791C0) runs over an owned property store row and an unstarted cloud facade (not synchronised, so
    // SyncPropertyValueFromSave returns after its lookup).
    // Not covered: the navigation manager's Select, which needs a UINavigationManager with its highlighter and input bridge.
    public sealed class UIContainerSaveSlotsTests
    {
        private static readonly int[] OrbsPerSlot = { 4, 6, 9 };

        private sealed class Slots
        {
            public LevelRequirementWorld World;
            public UIContainerSaveSlots Container;
            public UIWidgetToggleSaveData[] Toggles;
        }

        private static void Run(Action<Owned, Slots> body)
        {
            CultureInfo priorCulture = CultureInfo.CurrentCulture;
            LevelRequirementWorld.Run((owned, world) =>
            {
                owned.Lease(new ReleaseAction(() => CultureInfo.CurrentCulture = priorCulture));
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.ACT_1, "Played {0}" } }, () =>
                {
                    world.AddProgression();
                    var slots = new Slots { World = world };
                    // The slots' progress is cached empty through the handler's own routine, then given orb totals.
                    var handler = Get<ProgressionCollectableHandler>(ProcessManager.GetSystem<ProgressionManager>(), "m_collectables");
                    for (int slot = 0; slot < OrbsPerSlot.Length; slot++)
                    {
                        handler.CacheEmptyProgress(slot);
                        // Reflection: the totals of a cached save are private and only filled from authored mission collectables.
                        Get<Dictionary<CollectableType, ProgressionCollectable>>(handler.ProgressPerSave[slot], "m_total")[CollectableType.Orb] =
                            new ProgressionCollectable(CollectableType.Orb, OrbsPerSlot[slot], 10);
                    }

                    // The container's field initialisers cache this reference from its row.
                    owned.LeaseRow<UINavigationManager>();
                    slots.Container = owned.Host("Lucid save slots", false).AddComponent<UIContainerSaveSlots>();
                    slots.Toggles = new[]
                    {
                        UIWidgetToggleSaveDataTests.NewSaveToggle(owned),
                        UIWidgetToggleSaveDataTests.NewSaveToggle(owned),
                        UIWidgetToggleSaveDataTests.NewSaveToggle(owned),
                    };
                    Set(slots.Container, "m_saveSlotToggles", slots.Toggles); // serialized in the original
                    body(owned, slots);
                });
            });
        }

        // The save flow sets the open slot (a private setter) while it opens a save.
        private static void OpenSlot(Slots slots, int index) => Set(slots.World.Save, "<CurrentSaveIndex>k__BackingField", index);

        private static string[] Saves(Slots slots) => slots.World.Save.RawSaves;

        // 0x678F3C..0x678F46 takes the open slot (+0x28 of the save manager) as the selected one; the constructor 0x679340
        // builds the loading flag.
        [Test]
        public void TheSelectedSlotStartsAsTheOpenSlot()
        {
            Run((owned, slots) =>
            {
                OpenSlot(slots, 1);

                Call(slots.Container, "OnSaveManagerValid", slots.World.Save);

                Assert.That(Get<int>(slots.Container, "m_selectedSlotIndex"), Is.EqualTo(1));
                Assert.That(slots.Container.ShowLoading.Value, Is.False);
            });
        }

        // 0x678FB8..0x67905D: each raw save (+0x68 of the save manager) is read with JsonUtility and set up on the toggle of
        // the same index with that slot's progress (GetSaveProgress 0x5DD890) and an empty label.
        [Test]
        public void EachStoredSaveIsSetUpOnTheToggleOfItsIndex()
        {
            Run((owned, slots) =>
            {
                Saves(slots)[0] = UIWidgetToggleSaveDataTests.SaveJson;
                Saves(slots)[1] = null;
                Saves(slots)[2] = "{\"m_lastLevelVisitedGUID\":\"lucid-level\",\"m_ornaments\":[{\"m_guid\":\"orn-1\"}]," +
                    "\"m_playerStats\":[{\"m_playerStatType\":0,\"m_playerStatCounter\":61000}]}";
                OpenSlot(slots, 1);

                Call(slots.Container, "OnSaveManagerValid", slots.World.Save);

                Assert.That(slots.Toggles[0].SlotEmpty.Value, Is.False);
                Assert.That(slots.Toggles[0].NumberOfOrnaments.Value, Is.EqualTo(3));
                Assert.That(slots.Toggles[0].NumberOfOrbs.Value, Is.EqualTo(4));
                Assert.That(slots.Toggles[0].PlayTimeTimestamp.Value, Is.EqualTo("Played 01:02:03"));
                Assert.That(slots.Toggles[1].SlotEmpty.Value, Is.True, "no stored save");
                Assert.That(slots.Toggles[2].SlotEmpty.Value, Is.False);
                Assert.That(slots.Toggles[2].NumberOfOrnaments.Value, Is.EqualTo(1));
                Assert.That(slots.Toggles[2].NumberOfOrbs.Value, Is.EqualTo(9));
                Assert.That(slots.Toggles[2].PlayTimeTimestamp.Value, Is.EqualTo("Played 00:01:01"));
                foreach (UIWidgetToggleSaveData toggle in slots.Toggles)
                    Assert.That(toggle.Label.Value, Is.EqualTo(string.Empty));
            });
        }

        // 0x67907B..0x67908C: only the toggle whose index is the open slot is selected (the compare at 0x67907E is against
        // the save manager's open slot, not the container's field).
        [Test]
        public void OnlyTheOpenSlotsToggleIsSelected()
        {
            Run((owned, slots) =>
            {
                OpenSlot(slots, 2);

                Call(slots.Container, "OnSaveManagerValid", slots.World.Save);

                Assert.That(Array.ConvertAll(slots.Toggles, toggle => toggle.IsSelected()), Is.EqualTo(new[] { false, false, true }));
            });
        }

        // With no save open the index is -1, which no slot matches (the 32-bit value is compared as unsigned).
        [Test]
        public void WithNoOpenSlotNoToggleIsSelected()
        {
            Run((owned, slots) =>
            {
                OpenSlot(slots, -1);

                Call(slots.Container, "OnSaveManagerValid", slots.World.Save);

                Assert.That(Array.ConvertAll(slots.Toggles, toggle => toggle.IsSelected()), Is.EqualTo(new[] { false, false, false }));
                Assert.That(Get<int>(slots.Container, "m_selectedSlotIndex"), Is.EqualTo(-1));
            });
        }

        // The callback each toggle gets (<OnSaveManagerValid>b__0, 0x6794A0) makes its own slot the selected one when the
        // toggle turns on and does nothing when it turns off. Each toggle has a closure of its own (0x678F84 allocates one per
        // slot), so the slots 0, 1 and 2 stay distinct.
        [Test]
        public void TurningAToggleOnSelectsItsOwnSlot()
        {
            Run((owned, slots) =>
            {
                OpenSlot(slots, 2);
                Call(slots.Container, "OnSaveManagerValid", slots.World.Save);

                slots.Toggles[0].Action_OnValueChanged(true);
                Assert.That(Get<int>(slots.Container, "m_selectedSlotIndex"), Is.EqualTo(0));
                slots.Toggles[1].Action_OnValueChanged(false);
                Assert.That(Get<int>(slots.Container, "m_selectedSlotIndex"), Is.EqualTo(0), "turning off selects nothing");
                slots.Toggles[1].Action_OnValueChanged(true);
                Assert.That(Get<int>(slots.Container, "m_selectedSlotIndex"), Is.EqualTo(1));
                slots.Toggles[2].Action_OnValueChanged(true);
                Assert.That(Get<int>(slots.Container, "m_selectedSlotIndex"), Is.EqualTo(2));
            });
        }

        // Awake 0x678E10 clears the loading flag and hands OnSaveManagerValid to SystemRef.InvokeOnValid, which calls it
        // at once for a SaveManager that is already registered.
        [Test]
        public void AwakeClearsTheLoadingFlagAndSetsUpTheSlotsForTheRegisteredSaveManager()
        {
            Run((owned, slots) =>
            {
                Saves(slots)[0] = UIWidgetToggleSaveDataTests.SaveJson;
                OpenSlot(slots, 0);
                slots.Container.ShowLoading.Value = true;

                Call(slots.Container, "Awake");

                Assert.That(slots.Container.ShowLoading.Value, Is.False);
                Assert.That(slots.Toggles[0].SlotEmpty.Value, Is.False);
                Assert.That(slots.Toggles[1].SlotEmpty.Value, Is.True);
                Assert.That(slots.Toggles[0].IsSelected(), Is.True);
            });
        }

        // SetActiveSaveSlotIndex 0x6791B0 stores the index (+0x70).
        [Test]
        public void SettingTheActiveSlotStoresTheIndex()
        {
            Run((owned, slots) =>
            {
                Call(slots.Container, "SetActiveSaveSlotIndex", 2);

                Assert.That(Get<int>(slots.Container, "m_selectedSlotIndex"), Is.EqualTo(2));
            });
        }

        // TryNavigateToWidget 0x679130 returns at once while the navigation manager reference is null.
        [Test]
        public void NavigatingWithoutANavigationManagerDoesNothing()
        {
            Run((owned, slots) =>
                Assert.DoesNotThrow(() => Call(slots.Container, "TryNavigateToWidget", slots.Toggles[0])));
        }

        // Action_SelectSave 0x6791C0: ShowLoading is set (0x67921F), StartIsReady is written false (0x6792F3), the toggle's
        // chosen slot (+0x70, not the open slot) is made the last used one with the property store write on (edx = 1,
        // 0x67930E..0x679314), and the select-save event is queued (0x679320..0x679338). SetLastSaveIndex writes the OLD
        // index to the store before assigning the new one.
        [Test]
        public void SelectingASaveStartsItAndRemembersTheChosenSlot()
        {
            Run((owned, slots) =>
            {
                FieldInfo instance = typeof(HLPropertyStore).GetField("s_internalInstance", BindingFlags.Static | BindingFlags.NonPublic);
                if (instance.GetValue(null) != null) throw new InvalidOperationException("Fixture requires no property-store singleton");
                // The store constructor publishes itself as the singleton; released after the store's row (taken later).
                owned.Lease(new ReleaseAction(() => instance.SetValue(null, null)));
                var store = owned.Register(new HLPropertyStore("fixture-only", 0, "save-slots-fixture")); // no file I/O
                var cloud = owned.Host("Lucid save slots cloud", false).AddComponent<HLCloud.Cloud>();
                Call(slots.World.Save, "OnCloudValid", cloud);

                App app = ProcessManager.GetSystem<App>();
                app.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
                app.Storage.SetValue(AppFSMKeys.StartIsReady, true);
                UIModernEvent selectSave = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                Set(slots.Container, "m_selectSaveUIEvent", selectSave); // serialized in the original

                OpenSlot(slots, 0);
                slots.World.Save.SetLastSaveIndex(0, false);
                Call(slots.Container, "OnSaveManagerValid", slots.World.Save);
                slots.Toggles[2].Action_OnValueChanged(true);

                slots.Container.Action_SelectSave();

                Assert.That(slots.Container.ShowLoading.Value, Is.True);
                Assert.That(app.Storage.GetValue<bool>(AppFSMKeys.StartIsReady), Is.False);
                Assert.That(Get<int>(slots.World.Save, "m_lastSaveIndex"), Is.EqualTo(2), "the toggle's slot, not the open slot 0");
                Assert.That(store.GetPropertyValue("GameSave_LastSaveIndex"), Is.EqualTo("0"), "the old index is written to the store");
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { selectSave }));
            });
        }
    }
}
