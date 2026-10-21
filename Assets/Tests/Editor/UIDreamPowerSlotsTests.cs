using System;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIDreamPowerSlots and its private Slot (Game.Runtime 02000872 / 02000873, x86-64 0x614540..0x617740) over
    // real slot buttons and a real DreamPowerStoreManager that reads an open save. Each save request the manager
    // makes starts a coroutine on the fixture's inactive host, which refuses it with one engine error.
    public sealed class UIDreamPowerSlotsTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class World
        {
            public Owned Owned;
            public SaveDataGame Game;
            public ButtonSlot[] Buttons;
            public DreamPowerDefinition Bought, OtherBought, NotBought;
            public Sprite BoughtIcon, OtherIcon;

            public UIDreamPowerSlots Slots(CharacterArchetype archetype) =>
                new UIDreamPowerSlots(archetype, Array.ConvertAll(Buttons, button => button.Widget));

            public string Guid(int index) => Game.GetOrCreateDreamPowerLoadout(CharacterArchetype.None).SlotsByIndex.TryGetValue(index, out string guid) ? guid : null;
        }

        // Each call makes the manager request a save, which the fixture's coroutine host refuses once.
        private static void ExpectSaveRequests(int count)
        {
            for (int i = 0; i < count; i++) LogAssert.Expect(LogType.Error, RefusedCoroutine);
        }

        private static void Run(int slotCount, Action<World> body)
        {
            using (var characters = new OwnedCharacterManagers())
            {
                Owned.Run(owned =>
                {
                    owned.LeaseRow<ChallengeManager>(); // The store's field initialiser caches this row
                    owned.LeaseRow<AudioManager>(); // The slot buttons' audio components cache this row
                    owned.LeaseRow<DreamPowerStoreManager>();
                    var store = owned.Host("Lucid dream power slots store", false).AddComponent<DreamPowerStoreManager>();
                    SaveManager save = ProcessManager.GetSystem<SaveManager>();
                    Set(store, "m_saveManager", save); // Reflection: the reference the store's start-up chain takes from the SaveManager row
                    string row = ProcessManager.GetDefaultName<DreamPowerStoreManager>();
                    ProcessManager.RegisterSystem(store, row);
                    // The store unregisters itself when destroyed; releasing the row twice is harmless.
                    owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnregisterSystem(row)));

                    var world = new World { Owned = owned, Game = save.CurrentSave, Buttons = new ButtonSlot[slotCount] };
                    for (int i = 0; i < slotCount; i++) world.Buttons[i] = NewButtonSlot(owned, true, false);
                    world.BoughtIcon = NewSprite(owned);
                    world.OtherIcon = NewSprite(owned);
                    world.Bought = NewDefinition(owned, "lucid-slots-bought", Strings.MISSING, world.BoughtIcon);
                    world.OtherBought = NewDefinition(owned, "lucid-slots-other", Strings.NONE, world.OtherIcon);
                    world.NotBought = NewDefinition(owned, "lucid-slots-not-bought", Strings.MISSING, world.BoughtIcon);
                    world.Game.DreamPowerStore.GetOrCreateDreamPowerData("lucid-slots-bought").Bought = true;
                    world.Game.DreamPowerStore.GetOrCreateDreamPowerData("lucid-slots-other").Bought = true;
                    body(world);
                });
            }
        }

        // TryEquip 0x5661B0 accepts a bought power for an unrestricted archetype and saves it in the slot; the slot
        // then becomes interactable (0x615A6D), equipped (0x615A72) and shows the icon (0x615A82).
        [Test]
        public void EquippingABoughtPowerFillsTheSlotAndTheSavedLoadout()
        {
            Run(3, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.None);
                world.Buttons[1].Widget.Interactable = false;
                ExpectSaveRequests(1);

                slots.Equip(1, world.Bought);

                Assert.That(world.Buttons[1].Widget.IsEquipped, Is.True);
                Assert.That(world.Buttons[1].Widget.Icon.Value, Is.SameAs(world.BoughtIcon));
                Assert.That(world.Buttons[1].Widget.Interactable, Is.True);
                Assert.That(world.Guid(1), Is.EqualTo("lucid-slots-bought"));
                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.False);
                Assert.That(world.Buttons[2].Widget.IsEquipped, Is.False);
            });
        }

        // A refused equip (not bought, 0x566340 family) leaves the slot and the save alone and does not request a save.
        [Test]
        public void APowerThatIsNotBoughtIsNotEquipped()
        {
            Run(2, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.None);

                slots.Equip(0, world.NotBought);

                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.False);
                Assert.That(world.Buttons[0].Widget.Icon.Value, Is.Null);
                Assert.That(world.Guid(0), Is.Null);
            });
        }

        // ArchetypeAllowsPower 0x6662B0 only lets None through unrestricted; a restricted archetype with no collated powers refuses.
        [Test]
        public void ARestrictedArchetypeThatDoesNotListThePowerRefusesIt()
        {
            Run(2, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.Climbing);

                slots.Equip(0, world.Bought);

                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.False);
                Assert.That(world.Game.GetOrCreateDreamPowerLoadout(CharacterArchetype.Climbing).SlotsByIndex, Is.Empty);
            });
        }

        // After equipping, every other slot holding the same power is emptied (0x615AD6..0x615B27) and its saved
        // entry removed; the slot being equipped is skipped by the index test (0x615ACC), so re-equipping it keeps it.
        [Test]
        public void EquippingAPowerClearsItFromEveryOtherSlot()
        {
            Run(3, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.None);
                ExpectSaveRequests(1);
                slots.Equip(0, world.Bought);
                ExpectSaveRequests(1);
                slots.Equip(0, world.Bought);
                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.True, "the slot being equipped is never cleared");
                ExpectSaveRequests(2);
                slots.Equip(1, world.Bought);
                ExpectSaveRequests(1);
                slots.Equip(2, world.OtherBought);

                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.False);
                Assert.That(world.Buttons[0].Widget.Icon.Value, Is.Null);
                Assert.That(world.Buttons[1].Widget.IsEquipped, Is.True);
                Assert.That(world.Buttons[2].Widget.Icon.Value, Is.SameAs(world.OtherIcon), "a different power elsewhere is kept");
                Assert.That(world.Guid(0), Is.Null);
                Assert.That(world.Guid(1), Is.EqualTo("lucid-slots-bought"));
                Assert.That(world.Guid(2), Is.EqualTo("lucid-slots-other"));
            });
        }

        // Toggle 0x616ED0 tests the slot's own flag (0x616EEB): an equipped slot is emptied whatever power is passed
        // (0x616EF2..0x616F2E) and an empty slot is equipped through Equip (0x616F42).
        [Test]
        public void ToggleEmptiesAnEquippedSlotAndFillsAnEmptyOne()
        {
            Run(2, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.None);
                ExpectSaveRequests(1);
                slots.Toggle(0, world.Bought);
                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.True);
                Assert.That(world.Guid(0), Is.EqualTo("lucid-slots-bought"));

                ExpectSaveRequests(1);
                slots.Toggle(0, world.OtherBought);

                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.False);
                Assert.That(world.Buttons[0].Widget.Icon.Value, Is.Null);
                Assert.That(world.Guid(0), Is.Null);
            });
        }

        // A toggle on an empty slot goes through the refusable Equip, so a power that is not bought changes nothing.
        [Test]
        public void ToggleOfAnEmptySlotWithAPowerThatIsNotBoughtChangesNothing()
        {
            Run(1, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.None);

                slots.Toggle(0, world.NotBought);

                Assert.That(world.Buttons[0].Widget.IsEquipped, Is.False);
                Assert.That(world.Guid(0), Is.Null);
            });
        }

        // The private Unequip 0x617620 empties the slot and removes its saved entry.
        [Test]
        public void UnequipEmptiesTheSlotAndRemovesItsSavedEntry()
        {
            Run(2, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.None);
                ExpectSaveRequests(1);
                slots.Equip(1, world.Bought);
                ExpectSaveRequests(1);

                Call(slots, "Unequip", 1);

                Assert.That(world.Buttons[1].Widget.IsEquipped, Is.False);
                Assert.That(world.Buttons[1].Widget.Icon.Value, Is.Null);
                Assert.That(world.Guid(1), Is.Null);
            });
        }

        // The constructor 0x614540 builds one slot record per widget, all unequipped.
        [Test]
        public void ConstructionBuildsOneUnequippedRecordPerWidget()
        {
            Run(4, world =>
            {
                UIDreamPowerSlots slots = world.Slots(CharacterArchetype.None);

                Array records = Get<Array>(slots, "m_slots");
                Assert.That(records.Length, Is.EqualTo(4));
                foreach (object record in records)
                {
                    Assert.That((bool)record.GetType().GetProperty("IsEquipped").GetValue(record), Is.False);
                    Assert.That(record.GetType().GetProperty("Definition").GetValue(record), Is.Null);
                }
                Assert.That(Get<DreamPowerStoreManager>(slots, "m_dreamPowerStoreManager"), Is.SameAs(ProcessManager.GetSystem<DreamPowerStoreManager>()));
            });
        }
    }
}
