using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.DreamPowerStoreScenes;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The equip and purchase flow of UIContainerDreamPowerStore (Action_DoAction 0x616460, StartEquip 0x616650, Action_FinishEquip
    // 0x616520, Purchase 0x616750) over the dream power store scene, three slot buttons and a real UINavigationManager. The
    // navigation manager is built as its constructor does, over an owned UI runtime configuration whose input bridge probe asks
    // for selection while the manager is constructed (so it is active) and records the objects it selects instead of reaching an
    // event system; it asks for none afterwards, so no highlight is spawned. Every power lives in the data manager and in the
    // store's widget table, and a power is chosen through its widget's selection callback. Each selection of the store, direct or
    // through that callback, queues a next-frame callback that the inactive coroutine host refuses with a logged error. Awake and
    // the scroll position of the next-frame callback are not covered.
    public sealed class UIContainerDreamPowerStoreEquipTests
    {
        private const Strings Equippable = (Strings)9411, PowerName = (Strings)9412;
        private const int PowerCost = 10;

        private static readonly System.Text.RegularExpressions.Regex RefusedCoroutine = new System.Text.RegularExpressions.Regex("Coroutine couldn't be started");

        private sealed class Rig
        {
            public World World;
            public UIContainerDreamPowerStore Store;
            public UINavigationManager Navigation;
            public DreamPowerStoreSelectionBridgeProbe Bridge;
            public ButtonSlot[] Slots;
            public UIWidgetNavigation[] SlotNavigation;
            public Animation Animation;
            public AnimationClip BrowseClip, SlotClip;
            public UIContainerDreamPowerStoreBlueCoinCost Cost;
            public readonly List<DreamPowerDefinition> Powers = new List<DreamPowerDefinition>();
            public readonly List<PowerWidget> Widgets = new List<PowerWidget>();
        }

        // A navigation manager made as UINavigationManager's constructor expects: the navigation configuration in the system
        // configuration and a UI runtime configuration holding the input bridge. Each registration, the shutdown subscription and the
        // static input-type listener the constructor adds are released with the case.
        private static void AddNavigation(Rig rig)
        {
            Owned owned = rig.World.Scene.Owned;
            FastAction<UIInputType> priorListeners = InputBridge.OnUpdateLastInputType;
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => InputBridge.OnUpdateLastInputType = priorListeners));
            owned.LeaseActions(SystemAction.Shutdown);
            var systemConfiguration = owned.Register(new SystemConfiguration());
            var configuration = owned.Track(ScriptableObject.CreateInstance<UINavigationConfiguration>());
            // The override SystemConfiguration.AddConfig makes, into this case's own configuration system.
            systemConfiguration.StackableData.AddOverride(HLCRC32.GenerateInt(nameof(UINavigationConfiguration)), configuration);
            rig.Bridge = owned.Track(ScriptableObject.CreateInstance<DreamPowerStoreSelectionBridgeProbe>());
            rig.Bridge.RequiresSelection = true;
            var runtimeConfiguration = owned.Host("Lucid dream power store ui configuration", false).AddComponent<UIRuntimeConfiguration>();
            Set(runtimeConfiguration, "m_inputBridge", rig.Bridge); // Reflection: serialized reference the editor assigns
            owned.Register(runtimeConfiguration);
            rig.Navigation = new UINavigationManager();
            owned.Register(rig.Navigation);
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() =>
            {
                ProcessManager.ProcessSystemAction(rig.Navigation, SystemAction.Shutdown);
                ProcessManager.UnsubscribeFromAllActions(rig.Navigation);
            }));
            Assert.That(rig.Navigation.IsActive, Is.True, "the bridge asked for selection while the manager was constructed");
            rig.Bridge.RequiresSelection = false;
        }

        // The store after its own OnEnable (so its slots are built), with three slot buttons that each hold a navigation component,
        // the browse and slot animations, a cost container and the blue coins to buy any power. No power exists yet.
        private static void Run(Action<Rig> body) =>
            OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.FORMAT_X_OF_Y, "{0} of {1}" } }, () =>
                DreamPowerStoreScenes.Run(world =>
                {
                    Owned owned = world.Scene.Owned;
                    var rig = new Rig { World = world };
                    AddBands(world, out _, (DreamPowerStoreBandType.None, (Strings)9901));
                    AddNavigation(rig);
                    rig.Store = NewStore<UIContainerDreamPowerStore>(world);
                    Set(rig.Store, "m_uiNavigationManager", rig.Navigation); // Reflection: the reference Awake takes from the process manager
                    Set(rig.Store, "m_actionEquippableString", Equippable); // Reflection: authored serialized field
                    Set(rig.Store, "m_collectableCounter", AddBlueCoinCounter(world, out _)); // Reflection: serialized reference the editor assigns
                    rig.Slots = new ButtonSlot[3];
                    rig.SlotNavigation = new UIWidgetNavigation[3];
                    for (int i = 0; i < rig.Slots.Length; i++)
                    {
                        rig.Slots[i] = NewButtonSlot(owned, true, i == 0);
                        rig.SlotNavigation[i] = owned.Host("Lucid dream power slot navigation " + i, true).AddComponent<UIWidgetNavigation>();
                        Set(rig.Slots[i].Widget, "m_uiWidgetNavigation", rig.SlotNavigation[i]); // Reflection: serialized reference the editor assigns
                    }
                    Set(rig.Store, "m_slotButtons", rig.Slots.Select(slot => slot.Widget).ToArray()); // Reflection: serialized references the editor assigns
                    Set(rig.Store, "m_dreamPowerRows", new RectTransform[0]); // Reflection: serialized references the editor assigns
                    rig.Animation = owned.Host("Lucid dream power rect", true).AddComponent<Animation>();
                    rig.Animation.playAutomatically = false;
                    rig.BrowseClip = AddClip(owned, rig.Animation, "browse");
                    rig.SlotClip = AddClip(owned, rig.Animation, "slot");
                    Set(rig.Store, "m_dreamPowerRectAnimation", rig.Animation); // Reflection: serialized reference the editor assigns
                    Set(rig.Store, "m_dreamPowerRectBrowseClip", rig.BrowseClip); // Reflection: authored serialized field
                    Set(rig.Store, "m_dreamPowerRectSlotClip", rig.SlotClip); // Reflection: authored serialized field
                    rig.Cost = owned.Host("Lucid dream power store blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();
                    Set(rig.Store, "m_blueCoinCost", rig.Cost); // Reflection: serialized reference the editor assigns
                    world.Totals[CollectableType.BlueCoin] = new ProgressionCollectable(CollectableType.BlueCoin, 100, 100);
                    // The list App.Awake creates, which Edit Mode never sends.
                    world.Scene.App.Storage.SetValue(AppFSMKeys.MetaGameUnlocks, new List<IMetaGameUnlock>());

                    Call(rig.Store, "OnEnable");

                    body(rig);
                }));

        private static void Refused(int count)
        {
            for (int i = 0; i < count; i++)
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
        }

        // A power in the data manager and the store's widget table, set up with its current item state. The data manager is
        // filled after OnEnable, which would deal the powers over rows this rig does not have.
        private static int AddPower(Rig rig, bool bought)
        {
            World world = rig.World;
            int index = rig.Powers.Count;
            DreamPowerDefinition power = NewDefinition(world.Scene.Owned, "lucid-equip-power-" + index, PowerName, NewSprite(world.Scene.Owned));
            Set(power, "m_purchaseCost", PowerCost); // Reflection: authored serialized field
            world.Scene.Data.DreamPowerDefinitions[power.GetGUID()] = power;
            if (bought) Buy(world, power);
            PowerWidget widget = NewPowerWidget(world);
            Get<Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower>>(rig.Store, "m_widgetByDefinition")[power] = widget.Widget; // Reflection: filled by SetupDreamPowers
            widget.Widget.Setup((UIWidgetToggleDreamPowerSlotParameters)Call(rig.Store, "CreateWidgetParameters", power, 100));
            rig.Powers.Add(power);
            rig.Widgets.Add(widget);
            return index;
        }

        // The power's widget reports its selection to the store (one refused next-frame callback).
        private static void Choose(Rig rig, int power)
        {
            Refused(1);
            rig.Widgets[power].Widget.Action_OnToggleIsOn(true);
        }

        // Chooses the power, puts the store in slot selection and finishes the equip in the slot. The finish refreshes the powers,
        // which selects the chosen power again through its widget: two more refused callbacks.
        private static void EquipInSlot(Rig rig, int power, int slot)
        {
            Choose(rig, power);
            rig.Store.Browsing.Value = false;
            Refused(2);
            rig.Store.Action_FinishEquip(slot);
        }

        private static int SlotOf(Rig rig, int power) =>
            rig.World.Scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(CharacterArchetype.None).EquippedSlot(rig.Powers[power]);

        private static bool[] EquippedSlots(Rig rig) => rig.Slots.Select(slot => slot.Widget.IsEquipped).ToArray();

        // Action_FinishEquip 0x616520 while choosing a slot (Browsing false) equips the chosen power there (UIDreamPowerSlots.Equip,
        // DreamPowerStoreManager::TryEquip 0x615A20), sets Browsing, plays the browse clip and turns the action on.
        [Test]
        public void FinishingWhileChoosingASlotEquipsThePowerThereAndResumesBrowsing()
        {
            Run(rig =>
            {
                int power = AddPower(rig, true);
                Choose(rig, power);
                rig.Store.Browsing.Value = false;
                rig.Store.ActionAvailable.Value = false;
                Assert.That(rig.Animation.IsPlaying(rig.BrowseClip.name), Is.False);
                Refused(2);

                rig.Store.Action_FinishEquip(2);

                Assert.That(EquippedSlots(rig), Is.EqualTo(new[] { false, false, true }));
                Assert.That(SlotOf(rig, power), Is.EqualTo(2));
                Assert.That(rig.Store.Browsing.Value, Is.True);
                Assert.That(rig.Animation.IsPlaying(rig.BrowseClip.name), Is.True);
                Assert.That(rig.Store.ActionAvailable.Value, Is.True);
            });
        }

        // Action_FinishEquip while browsing is the slot toggle (UIDreamPowerSlots.Toggle, inlined at 0x616553..0x6165A6): an occupied
        // slot is unequipped (DreamPowerStoreManager::Unequip 0x566340) with no change of Browsing and no animation, and an empty one
        // takes the chosen power.
        [Test]
        public void FinishingWhileBrowsingTogglesTheSlotWithoutAnimation()
        {
            Run(rig =>
            {
                int power = AddPower(rig, true);
                EquipInSlot(rig, power, 1);
                Assert.That(EquippedSlots(rig), Is.EqualTo(new[] { false, true, false }), "set up through the equip flow");
                rig.Animation.Stop();
                Refused(2);

                rig.Store.Action_FinishEquip(1);

                Assert.That(EquippedSlots(rig), Is.EqualTo(new[] { false, false, false }));
                Assert.That(SlotOf(rig, power), Is.EqualTo(-1));
                Assert.That(rig.Store.Browsing.Value, Is.True);
                Assert.That(rig.Animation.IsPlaying(rig.BrowseClip.name), Is.False);
                Assert.That(rig.Animation.IsPlaying(rig.SlotClip.name), Is.False);

                Refused(2);
                rig.Store.Action_FinishEquip(2);

                Assert.That(EquippedSlots(rig), Is.EqualTo(new[] { false, false, true }), "an empty slot takes the power");
                Assert.That(SlotOf(rig, power), Is.EqualTo(2));
                Assert.That(rig.Animation.IsPlaying(rig.BrowseClip.name), Is.False);
            });
        }

        // StartEquip 0x616650 leaves browsing for the slot clip, turns the action off, selects the navigation of the first slot whose
        // IsEquipped (+0x98) is false (0x6166B6..0x6166E8), falling back to the first slot when every slot is equipped, and makes
        // every slot interactable.
        [TestCase("0", 1, TestName = "StartingTheEquipSelectsTheFirstEmptySlotAfterAnEquippedFirstSlot")]
        [TestCase("1", 0, TestName = "StartingTheEquipSelectsTheFirstSlotWhenOnlyTheSecondIsEquipped")]
        [TestCase("0,1", 2, TestName = "StartingTheEquipSelectsTheOnlyEmptySlot")]
        [TestCase("0,1,2", 0, TestName = "StartingTheEquipFallsBackToTheFirstSlotWhenEveryOneIsEquipped")]
        public void StartingTheEquipSelectsTheFirstEmptySlot(string equippedSlots, int expectedSlot)
        {
            Run(rig =>
            {
                int[] slots = equippedSlots.Split(',').Select(int.Parse).ToArray();
                foreach (int slot in slots)
                    EquipInSlot(rig, AddPower(rig, true), slot);
                Assert.That(rig.Bridge.Selected, Is.Empty, "nothing selected a slot yet");
                foreach (ButtonSlot slot in rig.Slots)
                    slot.Widget.SetInteractable(false);

                Call(rig.Store, "StartEquip");

                Assert.That(rig.Store.Browsing.Value, Is.False);
                Assert.That(rig.Animation.IsPlaying(rig.SlotClip.name), Is.True);
                Assert.That(rig.Store.ActionAvailable.Value, Is.False);
                Assert.That(rig.Navigation.GetCurrentlySelected(), Is.SameAs(rig.SlotNavigation[expectedSlot]));
                Assert.That(rig.Bridge.Selected.Select(selected => selected.GetComponent<UIWidgetNavigation>()).ToArray(),
                    Is.EqualTo(new[] { rig.SlotNavigation[expectedSlot] }));
                Assert.That(rig.Slots.Select(slot => slot.Widget.Interactable).ToArray(), Is.EqualTo(new[] { true, true, true }));
            });
        }

        // Action_DoAction 0x616460 on a bought power that sits in a slot finishes the equip with that slot (tail call 0x6164F6): with
        // the store browsing that unequips it and stays in browsing.
        [Test]
        public void TheActionOnAnEquippedPowerUnequipsItsSlot()
        {
            Run(rig =>
            {
                int power = AddPower(rig, true);
                EquipInSlot(rig, power, 1);
                Refused(2);

                rig.Store.Action_DoAction();

                Assert.That(EquippedSlots(rig), Is.EqualTo(new[] { false, false, false }));
                Assert.That(SlotOf(rig, power), Is.EqualTo(-1));
                Assert.That(rig.Store.Browsing.Value, Is.True);
            });
        }

        // Action_DoAction on a bought power that sits in no slot starts the slot selection (0x616517).
        [Test]
        public void TheActionOnABoughtPowerInNoSlotStartsTheSlotSelection()
        {
            Run(rig =>
            {
                int power = AddPower(rig, true);
                Choose(rig, power);

                rig.Store.Action_DoAction();

                Assert.That(rig.Store.Browsing.Value, Is.False);
                Assert.That(rig.Navigation.GetCurrentlySelected(), Is.SameAs(rig.SlotNavigation[0]));
                Assert.That(EquippedSlots(rig), Is.EqualTo(new[] { false, false, false }));
            });
        }

        // Action_DoAction on an unbought power buys it (Purchase 0x616750 then RefreshDreamPowers, 0x6164FE and 0x61650C): the power
        // is bought and charged in the save, a meta game unlock for it is queued under AppFSMKeys.MetaGameUnlocks, the counter counts
        // it, the cost container is set up in the Bought state (so it shows no cost) and the action offers the equip.
        [Test]
        public void TheActionOnAnUnboughtPowerPurchasesItAndQueuesItsUnlock()
        {
            Run(rig =>
            {
                int power = AddPower(rig, false);
                Choose(rig, power);
                Assert.That(rig.Store.Purchasable.Value, Is.True);
                var save = rig.World.Scene.Save.CurrentSave;
                // Purchase and the refresh after it each re-select the power, and so does the refresh of the action itself.
                Refused(4);

                rig.Store.Action_DoAction();

                Assert.That(save.DreamPowerStore.GetOrCreateDreamPowerData(rig.Powers[power].GetGUID()).Bought, Is.True);
                Assert.That(save.BlueCoins.Spent, Is.EqualTo(PowerCost));
                var unlocks = rig.World.Scene.App.Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks);
                Assert.That(unlocks.Count, Is.EqualTo(1));
                Assert.That(unlocks[0], Is.InstanceOf<MetaGameUnlockDreamPowerStoreItem>());
                Assert.That(((MetaGameUnlockDreamPowerStoreItem)unlocks[0]).Definition, Is.SameAs(rig.Powers[power]));
                Assert.That(rig.Store.BoughtAmount.Value, Is.EqualTo(1));
                Assert.That(rig.Cost.ShowCost.Value, Is.False);
                Assert.That(rig.Store.ActionLabel.Value, Is.EqualTo(Equippable));
                Assert.That(rig.Store.ActionAvailable.Value, Is.True);
                Assert.That(rig.Store.Purchasable.Value, Is.False);
            });
        }
    }
}
