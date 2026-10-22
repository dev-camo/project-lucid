using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.DreamPowerStoreScenes;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerDreamPowerStore (Game.Runtime 0x0200086b with its nested types 0x0200086c..0x0200086e, x86-64 0x6130F0..0x617110)
    // as a real component on an owned inactive host over the dream power store scene: a registered DreamPowerStoreManager (available,
    // since it has no unlock requirement), the open save, a ProgressionManager with authored totals, the DataManager's dream powers,
    // real power widgets, slot buttons and a collectable counter. Awake is not run: it asks the process manager for a
    // UINavigationManager, whose constructor reads the UI runtime configuration, so the services it would take are assigned to the
    // container's fields as Awake assigns them. The equip and purchase flow that selects through the navigation manager (StartEquip,
    // Action_FinishEquip, Action_DoAction on a bought or unbought power) is in UIContainerDreamPowerStoreEquipTests. Not covered:
    // Awake, the selection of the last selected power in SetupDreamPowers, the widget registration in SetAllToggles, Action_ExitEquip,
    // and the next frame callback of OnDreamPowerChanged, which the inactive coroutine host refuses; the progression lock text of
    // UpdateLockedStatus (the reward string picker keeps static service references).
    public sealed class UIContainerDreamPowerStoreTests
    {
        private const Strings Locked = (Strings)9401, Equippable = (Strings)9402, Purchasable = (Strings)9403, Insufficient = (Strings)9404,
            ByArchetype = (Strings)9405, PowerName = (Strings)9406, PowerDescription = (Strings)9407;

        private static readonly System.Text.RegularExpressions.Regex RefusedCoroutine = new System.Text.RegularExpressions.Regex("Coroutine couldn't be started");

        private static T NewStore<T>(World world) where T : UIContainerDreamPowerStore
        {
            T store = DreamPowerStoreScenes.NewStore<T>(world);
            Set(store, "m_actionLockedString", Locked); // Reflection: authored serialized field
            Set(store, "m_actionEquippableString", Equippable); // Reflection: authored serialized field
            Set(store, "m_actionPurchasableString", Purchasable); // Reflection: authored serialized field
            Set(store, "m_insufficientCurrencyString", Insufficient); // Reflection: authored serialized field
            Set(store, "m_lockedByArchetypeString", ByArchetype); // Reflection: authored serialized field
            return store;
        }

        private static UIContainerDreamPowerStore NewStore(World world) => NewStore<UIContainerDreamPowerStore>(world);

        private static HashSet<DreamPowerStoreBandType> Bands(UIContainerDreamPowerStore store) =>
            Get<HashSet<DreamPowerStoreBandType>>(store, "m_unlockedDreamPowerBands");

        private static void SetBlueCoins(World world, int collected, int spent)
        {
            world.Totals[CollectableType.BlueCoin] = new ProgressionCollectable(CollectableType.BlueCoin, collected, 40);
            world.Scene.Save.CurrentSave.BlueCoins.Spent = spent;
        }

        // OnEnable 0x6130F0: the blue coin counter is set up from the save (the collected coins less the spent ones, 0x6131C9), the
        // store is flagged available and browsing with its action on (0x6131F4..0x61322C), the archetype is read from the active
        // mission (0x6132D6), the slots are built for that archetype (UIDreamPowerSlots constructor 0x61330D), and then the unlocked
        // bands, the archetype title, the power grid, the saved slots and the counter are set up (0x61332B..0x613343).
        [Test]
        public void EnablingTheStoreSetsUpTheCounterTheArchetypeTheSlotsAndTheDisplays()
        {
            OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.FORMAT_X_OF_Y, "{0} of {1}" } }, () =>
                DreamPowerStoreScenes.Run(world =>
                {
                    UIContainerDreamPowerStore store = NewStore(world);
                    WireForEnable(world, store);
                    ActivateMission(world.Scene, CharacterArchetype.Climbing, true);
                    ArchetypeImages images = AddArchetype(world, CharacterArchetype.Climbing);
                    SetBlueCoins(world, 12, 5);
                    store.Available.Value = false;
                    store.Browsing.Value = false;
                    store.ActionAvailable.Value = false;

                    Call(store, "OnEnable");

                    var counter = Get<UIContainerCollectableCounter>(store, "m_collectableCounter");
                    Assert.That(counter.CollectedAmount.Value, Is.EqualTo(7));
                    Assert.That(counter.TotalAmount.Value, Is.EqualTo(40));
                    Assert.That(store.Available.Value, Is.True);
                    Assert.That(store.Browsing.Value, Is.True);
                    Assert.That(store.ActionAvailable.Value, Is.True);
                    Assert.That(Get<CharacterArchetype>(store, "m_characterArchetype"), Is.EqualTo(CharacterArchetype.Climbing));
                    Assert.That(Get<UIDreamPowerSlots>(store, "m_slots"), Is.Not.Null);
                    Assert.That(Bands(store), Is.EquivalentTo(new[] { DreamPowerStoreBandType.None }));
                    Assert.That(store.HasArchetype.Value, Is.True);
                    Assert.That(store.CharacterArchetypeImage.Value, Is.SameAs(images.Alternate));
                    Assert.That(store.CharacterArchetypeSilhouette.Value, Is.SameAs(images.Silhouette));
                    Assert.That(store.CounterFormatString.Value, Is.EqualTo("{0} of {1}"));
                    Assert.That(store.TotalAmount.Value, Is.Zero);
                    Assert.That(store.BoughtAmount.Value, Is.Zero);
                }));
        }

        // GetBlueCoinsAvailable 0x6154C0: the collected blue coins of the progression totals (the high half of the returned
        // struct, shr 0x20 at 0x6154DC) minus the spent coins of the open save (+0x20 of the save's blue coins, 0x6154F5).
        [Test]
        public void TheBlueCoinsAvailableAreTheCollectedCoinsLessTheSpentOnes()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);

                SetBlueCoins(world, 12, 5);
                Assert.That(Call(store, "GetBlueCoinsAvailable"), Is.EqualTo(7));

                SetBlueCoins(world, 12, 20);
                Assert.That(Call(store, "GetBlueCoinsAvailable"), Is.EqualTo(-8));
            });
        }

        // SetCharacterArchetype 0x614460: the archetype of the active mission's definition (+0x98, 0x61451B) when the mission manager
        // has an active mission (0x614495), otherwise the default archetype.
        [Test]
        public void TheArchetypeIsTheActiveMissionsOrTheDefault()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                Set(store, "m_characterArchetype", CharacterArchetype.Flying); // Reflection: state an earlier mission left in the container

                Call(store, "SetCharacterArchetype");
                Assert.That(Get<CharacterArchetype>(store, "m_characterArchetype"), Is.EqualTo(CharacterArchetype.None));

                ActivateMission(world.Scene, CharacterArchetype.Climbing, true);
                Call(store, "SetCharacterArchetype");
                Assert.That(Get<CharacterArchetype>(store, "m_characterArchetype"), Is.EqualTo(CharacterArchetype.Climbing));
            });
        }

        // SetUnlockedBands 0x6146E0 without the debug unlock: the first band (value 0, 0x6147FF) is always open, then every band of the
        // DataManager's band table whose reward is collected in the save (TryGetRewardDataByRewardGUID 0x6148A5, Collected at +0x28)
        // opens too; an uncollected record and a band without a record stay closed.
        [Test]
        public void TheFirstBandAndTheCollectedBandsAreUnlocked()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                DreamPowerStoreBandDefinition[] bands = AddBands(world, out _, (DreamPowerStoreBandType.Band1, (Strings)9501),
                    (DreamPowerStoreBandType.Band2, (Strings)9502), (DreamPowerStoreBandType.Band3, (Strings)9503));
                SaveDataGame save = world.Scene.Save.CurrentSave;
                save.CreateRewardTrackData(RewardTrackType.Adventure, "lucid-band-track-0", bands[0].GetGUID());
                save.GetRewardDataByTrackGUIDUnsafe("lucid-band-track-0").Collected = true;
                save.CreateRewardTrackData(RewardTrackType.Adventure, "lucid-band-track-1", bands[1].GetGUID());

                Call(store, "SetUnlockedBands");

                Assert.That(Bands(store), Is.EquivalentTo(new[] { DreamPowerStoreBandType.None, DreamPowerStoreBandType.Band1 }));
            });
        }

        // With the debug unlock of the store manager (IsDebugUnlockAll 0x61479E) every band value is added (AddRange of all enum
        // values, 0x6147DB) and the save is not consulted.
        [Test]
        public void TheDebugUnlockOpensEveryBand()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                world.Scene.Save.GetSaveDataSettings().Debug.UnlockDreamPowers = true;

                Call(store, "SetUnlockedBands");

                Assert.That(Bands(store), Is.EquivalentTo(Enum.GetValues(typeof(DreamPowerStoreBandType)).Cast<DreamPowerStoreBandType>()));
                Assert.That(Bands(store).Count, Is.EqualTo(5));
            });
        }

        // GetUIItemState 0x615900 asks for the archetype (ArchetypeAllowsPower), the band, the saved Bought flag and then: a locked
        // band wins whatever else holds (0x615990, 0x61599E); a bought power of an archetype that cannot use it is locked by archetype
        // (r12 = allows | !bought, 0x6159A0..0x6159BB); an unbought power is Purchasable when the cost is at most the coins
        // (setle at 0x615998) and Insufficient otherwise, even for an archetype that cannot use it; a bought and allowed power is
        // Equipped when the loadout holds it (EquippedSlot != -1, 0x6159F5) and Bought when it does not.
        [Test]
        public void TheItemStateFollowsTheBandTheBoughtFlagTheArchetypeAndTheCoins()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                DreamPowerDefinition open = AddPower(world, "lucid-state-open", 5);
                DreamPowerDefinition closedBand = AddPower(world, "lucid-state-closed", 5, DreamPowerStoreBandType.Band2);
                Call(store, "SetUnlockedBands");
                UIDreamPowerStoreItemState State(DreamPowerDefinition power, int coins) =>
                    (UIDreamPowerStoreItemState)Call(store, "GetUIItemState", power, coins);

                Assert.That(State(closedBand, 99), Is.EqualTo(UIDreamPowerStoreItemState.LockedByProgression), "a closed band");
                Buy(world, closedBand);
                Assert.That(State(closedBand, 99), Is.EqualTo(UIDreamPowerStoreItemState.LockedByProgression), "a closed band wins over bought");
                Assert.That(State(open, 5), Is.EqualTo(UIDreamPowerStoreItemState.Purchasable), "the coins equal the cost");
                Assert.That(State(open, 4), Is.EqualTo(UIDreamPowerStoreItemState.Insufficient), "one coin short");

                Buy(world, open);
                Assert.That(State(open, 0), Is.EqualTo(UIDreamPowerStoreItemState.Bought), "a bought power needs no coins");
                world.Scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(CharacterArchetype.None).Set(1, open.GetGUID());
                Assert.That(State(open, 0), Is.EqualTo(UIDreamPowerStoreItemState.Equipped));

                // The climbing archetype lists no powers, so it cannot use any.
                ActivateMission(world.Scene, CharacterArchetype.Climbing, true);
                Call(store, "SetCharacterArchetype");
                Assert.That(State(open, 0), Is.EqualTo(UIDreamPowerStoreItemState.LockedByArchetype), "bought, but of another archetype");
                // The archetype check comes before the loadout lookup (0x6159C3..0x6159CA skips EquippedSlot): a power the climbing
                // loadout holds is still locked by archetype.
                world.Scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(CharacterArchetype.Climbing).Set(0, open.GetGUID());
                Assert.That(State(open, 0), Is.EqualTo(UIDreamPowerStoreItemState.LockedByArchetype), "held by the loadout, but of another archetype");
                DreamPowerDefinition unbought = AddPower(world, "lucid-state-unbought", 5);
                Assert.That(State(unbought, 99), Is.EqualTo(UIDreamPowerStoreItemState.Purchasable), "an unbought power is not archetype locked");
                Assert.That(State(unbought, 1), Is.EqualTo(UIDreamPowerStoreItemState.Insufficient));
            });
        }

        // CreateWidgetParameters 0x615500: the parameters carry the power, its item state and the Available binder's value, and
        // the toggle callback (the closure over the state, 0x6170E0) reports a toggled power with the state captured when the
        // parameters were made, not the state at the time of the toggle.
        [Test]
        public void TheWidgetParametersCarryTheStateAndACallbackOverThatState()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                DreamPowerStoreChangeRecordingContainer store = NewStore<DreamPowerStoreChangeRecordingContainer>(world);
                DreamPowerDefinition power = AddPower(world, "lucid-parameters-power", 5);
                DreamPowerDefinition other = AddPower(world, "lucid-parameters-other", 7);
                Call(store, "SetUnlockedBands");
                store.Available.Value = true;

                var parameters = (UIWidgetToggleDreamPowerSlotParameters)Call(store, "CreateWidgetParameters", power, 5);
                Buy(world, power);
                parameters.OnToggleChanged(other);

                Assert.That(parameters.Definition, Is.SameAs(power));
                Assert.That(parameters.State, Is.EqualTo(UIDreamPowerStoreItemState.Purchasable));
                Assert.That(parameters.Available, Is.True);
                Assert.That(store.Changes.Count, Is.EqualTo(1));
                Assert.That(store.Changes[0].definition, Is.SameAs(other));
                Assert.That(store.Changes[0].itemState, Is.EqualTo(UIDreamPowerStoreItemState.Purchasable), "the state of creation");

                store.Available.Value = false;
                Assert.That(((UIWidgetToggleDreamPowerSlotParameters)Call(store, "CreateWidgetParameters", power, 5)).Available, Is.False);
                Assert.That(((UIWidgetToggleDreamPowerSlotParameters)Call(store, "CreateWidgetParameters", power, 5)).State,
                    Is.EqualTo(UIDreamPowerStoreItemState.Bought));
            });
        }

        // UpdateCounter 0x6152B0: the format string is the localised counter format (m_counterStringFormat, default 0xebf0870b,
        // "{0} of {1}" style), the total is the number of dream powers in the DataManager (Dictionary.Count, 0x615397) and the bought
        // amount counts the saved store items whose Bought flag is set (0x6153FF..0x615418).
        [Test]
        public void TheCounterShowsTheBoughtPowersOutOfAllPowers()
        {
            OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.FORMAT_X_OF_Y, "{0} of {1}" } }, () =>
                DreamPowerStoreScenes.Run(world =>
                {
                    UIContainerDreamPowerStore store = NewStore(world);
                    DreamPowerDefinition[] powers = { AddPower(world, "lucid-count-0", 1), AddPower(world, "lucid-count-1", 1), AddPower(world, "lucid-count-2", 1) };
                    Buy(world, powers[0]);
                    Buy(world, powers[2]);
                    world.Scene.Save.CurrentSave.DreamPowerStore.GetOrCreateDreamPowerData(powers[1].GetGUID());
                    WriteStoreItems(world);

                    Call(store, "UpdateCounter");

                    Assert.That(store.CounterFormatString.Value, Is.EqualTo("{0} of {1}"));
                    Assert.That(store.TotalAmount.Value, Is.EqualTo(3));
                    Assert.That(store.BoughtAmount.Value, Is.EqualTo(2));
                }));
        }

        // UpdateAction 0x616300: the locked states label the action with the locked string and offer no action; Bought labels it with
        // the equippable string and Equipped with 0xc422b241 (Strings.MENU_DREAM_POWER_STORE_UNEQUIP), both available only while
        // equipping is allowed (m_allowEquipping at +0x38, 0x616361); Purchasable is always available; Insufficient and every
        // state past Equipped (ja at 0x61630D) use the insufficient string with no action.
        [Test]
        public void EachItemStateLabelsTheActionAndSetsItsAvailability()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                var rows = new (UIDreamPowerStoreItemState state, Strings label, bool equippingNeeded, bool always)[]
                {
                    (UIDreamPowerStoreItemState.LockedByProgression, Locked, false, false),
                    (UIDreamPowerStoreItemState.LockedByArchetype, Locked, false, false),
                    (UIDreamPowerStoreItemState.Bought, Equippable, true, false),
                    (UIDreamPowerStoreItemState.Purchasable, Purchasable, false, true),
                    (UIDreamPowerStoreItemState.Insufficient, Insufficient, false, false),
                    (UIDreamPowerStoreItemState.Equipped, Strings.MENU_DREAM_POWER_STORE_UNEQUIP, true, false),
                    ((UIDreamPowerStoreItemState)6, Insufficient, false, false),
                };
                foreach (bool allowEquipping in new[] { true, false })
                {
                    Set(store, "m_allowEquipping", allowEquipping); // Reflection: authored serialized field
                    foreach (var row in rows)
                    {
                        store.ActionAvailable.Value = !(row.always || (row.equippingNeeded && allowEquipping));

                        Call(store, "UpdateAction", row.state);

                        string label = row.state + " with equipping " + allowEquipping;
                        Assert.That(store.ActionLabel.Value, Is.EqualTo(row.label), label);
                        Assert.That(store.ActionAvailable.Value, Is.EqualTo(row.always || (row.equippingNeeded && allowEquipping)), label);
                    }
                }
            });
        }

        // UpdateLockedStatus 0x616200: the locked description shows while any reason holds (or of the three flags, 0x616227..0x61622D);
        // the price reason (0x616240) wins over the archetype reason (0x6162B3) and sets the insufficient string, the archetype
        // reason sets its own string, and with no reason the lock text is left alone. The progression reason (the reward string
        // picker) is not covered.
        [Test]
        public void TheLockMessageIsThePriceThenTheArchetypeReason()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                store.DreamPowerLockedDescription.Value = Strings.MISSING;

                Call(store, "UpdateLockedStatus", false, false, false, null);
                Assert.That(store.LockedDescription.Value, Is.False);
                Assert.That(store.DreamPowerLockedDescription.Value, Is.EqualTo(Strings.MISSING), "no reason leaves the text");

                Call(store, "UpdateLockedStatus", true, false, false, null);
                Assert.That(store.LockedDescription.Value, Is.True);
                Assert.That(store.DreamPowerLockedDescription.Value, Is.EqualTo(ByArchetype));

                Call(store, "UpdateLockedStatus", true, false, true, null);
                Assert.That(store.LockedDescription.Value, Is.True);
                Assert.That(store.DreamPowerLockedDescription.Value, Is.EqualTo(Insufficient), "the price wins over the archetype");

                Call(store, "UpdateLockedStatus", false, false, true, null);
                Assert.That(store.DreamPowerLockedDescription.Value, Is.EqualTo(Insufficient));

                Call(store, "UpdateLockedStatus", false, false, false, null);
                Assert.That(store.LockedDescription.Value, Is.False, "the flag follows the reasons");
            });
        }

        // SetSlotStatus 0x6163C0 gives every slot button the item state (SetSlotState 0x507F70 in a loop): only a Bought state lets a
        // slot that is not yet equipped take the power.
        [Test]
        public void TheSlotStatusReachesEverySlotButton()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                ButtonSlot first = NewButtonSlot(world.Scene.Owned);
                ButtonSlot second = NewButtonSlot(world.Scene.Owned, true, false);
                Set(store, "m_slotButtons", new[] { first.Widget, second.Widget }); // Reflection: serialized references the editor assigns
                DreamPowerDefinition power = NewDefinition(world.Scene.Owned, "lucid-slot-power", PowerName, NewSprite(world.Scene.Owned));
                first.Widget.Equip(power);

                Call(store, "SetSlotStatus", UIDreamPowerStoreItemState.Bought);
                Assert.That(first.Widget.ValidForEquip.Value, Is.False, "already equipped");
                Assert.That(second.Widget.ValidForEquip.Value, Is.True);

                Call(store, "SetSlotStatus", UIDreamPowerStoreItemState.Purchasable);
                Assert.That(first.Widget.ValidForEquip.Value, Is.False);
                Assert.That(second.Widget.ValidForEquip.Value, Is.False);
            });
        }

        // ClearSelectedPower 0x616AC0 navigates every power widget away (Action_OnNavigatedAway 0x616BC5), forgets the selected power
        // and turns the action off. SetAllToggles 0x616C90 with no widgets only publishes the flag to ActionAvailable (0x616E59).
        [Test]
        public void ClearingTheSelectionNavigatesEveryWidgetAwayAndTurnsTheActionOff()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                Owned owned = world.Scene.Owned;
                AddBands(world, out _, (DreamPowerStoreBandType.None, (Strings)9601));
                var table = Get<Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower>>(store, "m_widgetByDefinition");
                var widgets = new PowerWidget[2];
                DreamPowerDefinition selected = null;
                for (int i = 0; i < widgets.Length; i++)
                {
                    DreamPowerDefinition power = NewDefinition(owned, "lucid-clear-power-" + i, PowerName, NewSprite(owned));
                    widgets[i] = NewPowerWidget(world);
                    widgets[i].Widget.Setup(new UIWidgetToggleDreamPowerSlotParameters(power, UIDreamPowerStoreItemState.Bought, true, _ => { }));
                    widgets[i].Toggle.IsOn = true;
                    widgets[i].Widget.ShowSelected.Value = true;
                    table[power] = widgets[i].Widget;
                    selected = power;
                }
                Set(store, "m_currentSelectedDefinition", selected); // Reflection: set by the widgets' selection callback
                store.ActionAvailable.Value = true;

                store.ClearSelectedPower();

                Assert.That(widgets.Select(widget => widget.Toggle.IsOn).ToArray(), Is.EqualTo(new[] { false, false }));
                Assert.That(widgets.Select(widget => widget.Widget.ShowSelected.Value).ToArray(), Is.EqualTo(new[] { false, false }));
                Assert.That(Get<DreamPowerDefinition>(store, "m_currentSelectedDefinition") == null, Is.True);
                Assert.That(store.ActionAvailable.Value, Is.False);

                table.Clear();
                store.SetAllToggles(true);
                Assert.That(store.ActionAvailable.Value, Is.True);
                store.SetAllToggles(false);
                Assert.That(store.ActionAvailable.Value, Is.False);
            });
        }

        // Action_DoAction 0x616460 does nothing while the store is not available (0x616482) and when no power is selected
        // (op_Equality with null, 0x61648F): the selected power is not bought and no refresh is made.
        [Test]
        public void TheActionDoesNothingWithoutAvailabilityOrASelectedPower()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                DreamPowerDefinition power = AddPower(world, "lucid-action-power", 5);

                store.Available.Value = true;
                Assert.DoesNotThrow(store.Action_DoAction, "no selected power");

                Set(store, "m_currentSelectedDefinition", power); // Reflection: set by the widgets' selection callback
                store.Available.Value = false;
                Assert.DoesNotThrow(store.Action_DoAction, "not available");
                Assert.That(world.Scene.Save.CurrentSave.DreamPowerStore.DreamPowers.Count, Is.Zero, "nothing was bought or created");
            });
        }

        // OnDreamPowerChanged 0x613490 with a Purchasable power: the purchasable flag follows availability (Available && state == 3,
        // 0x61356B), the selection is kept in the container and the store manager (0x613594, 0x6135B1), the cost container is set up
        // (0x61360F) and the cost text is the purchase cost (0x613629), the description shows the power (UpdateAvailableDescription
        // 0x615BA0), the locked message is cleared (0x613674, state 3 skips the lock logic) and the action is labelled. The
        // next-frame callback is queued on the coroutine host, which refuses it with the logged error.
        [Test]
        public void ChoosingAPurchasablePowerPublishesItsPriceDescriptionAndAction()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                Sprite icon = NewSprite(world.Scene.Owned);
                DreamPowerDefinition power = NewDefinition(world.Scene.Owned, "lucid-change-power", PowerName, icon);
                Set(power, "m_purchaseCost", 35); // Reflection: authored serialized field
                Set(power, "m_description", PowerDescription); // Reflection: authored serialized field
                var cost = world.Scene.Owned.Host("Lucid change blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();
                Set(store, "m_blueCoinCost", cost); // Reflection: serialized reference the editor assigns
                Set(store, "m_slotButtons", new UIWidgetButtonDreamPowerSlot[0]); // Reflection: serialized references the editor assigns
                store.Available.Value = true;
                store.LockedDescription.Value = true;
                store.ShowDescriptionArchetype.Value = true;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Call(store, "OnDreamPowerChanged", power, UIDreamPowerStoreItemState.Purchasable); // Reflection: protected OnDreamPowerChanged of UIContainerDreamPowerStore, not reachable from the test

                Assert.That(store.Purchasable.Value, Is.True);
                Assert.That(Get<DreamPowerDefinition>(store, "m_currentSelectedDefinition"), Is.SameAs(power));
                Assert.That(world.Store.LastSelectedDefinition, Is.SameAs(power));
                Assert.That(cost.ShowCost.Value, Is.True);
                Assert.That(cost.Cost.Value, Is.EqualTo("35"));
                Assert.That(store.BlueCoinCost.Value, Is.EqualTo("35"));
                Assert.That(store.DreamPowerName.Value, Is.EqualTo(PowerName));
                Assert.That(store.DreamPowerDescription.Value, Is.EqualTo(PowerDescription));
                Assert.That(store.DreamPowerIcon.Value, Is.SameAs(icon));
                Assert.That(store.ShowDescriptionArchetype.Value, Is.False, "the power has no required archetype");
                Assert.That(store.DescriptionArchetypeImage.Value, Is.Null);
                Assert.That(store.LockedDescription.Value, Is.False);
                Assert.That(store.ActionLabel.Value, Is.EqualTo(Purchasable));
                Assert.That(store.ActionAvailable.Value, Is.True);

                // An unavailable store keeps the power from being offered for purchase (a logical and, not a copy of the state test).
                store.Available.Value = false;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Call(store, "OnDreamPowerChanged", power, UIDreamPowerStoreItemState.Purchasable); // Reflection: protected OnDreamPowerChanged of UIContainerDreamPowerStore, not reachable from the test
                Assert.That(store.Purchasable.Value, Is.False);
            });
        }

        // OnDreamPowerChanged formats the price for the store's BlueCoinCost with the current culture (Int32.ToString 0x060007a8 at
        // 0x613629), while the cost container formats it with the invariant culture (0x617394): with a "~" negative sign as the
        // current culture a price of -5 reads "~5" in the store and "-5" in the container.
        [Test]
        public void TheStorePriceFollowsTheCurrentCultureAndTheCostContainerTheInvariantOne()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                DreamPowerDefinition power = NewDefinition(world.Scene.Owned, "lucid-culture-power", PowerName, NewSprite(world.Scene.Owned));
                Set(power, "m_purchaseCost", -5); // Reflection: authored serialized field
                var cost = world.Scene.Owned.Host("Lucid culture blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();
                Set(store, "m_blueCoinCost", cost); // Reflection: serialized reference the editor assigns
                Set(store, "m_slotButtons", new UIWidgetButtonDreamPowerSlot[0]); // Reflection: serialized references the editor assigns
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                TildeNegativeSignCulture.Run(() => Call(store, "OnDreamPowerChanged", power, UIDreamPowerStoreItemState.Purchasable)); // Reflection: protected OnDreamPowerChanged of UIContainerDreamPowerStore, not reachable from the test

                Assert.That(store.BlueCoinCost.Value, Is.EqualTo("~5"));
                Assert.That(cost.Cost.Value, Is.EqualTo("-5"));
            });
        }

        // The lock logic of OnDreamPowerChanged (0x61367F..0x613758): an unaffordable power shows the insufficient message
        // (state 4, 0x6136BD); a bought power of an archetype that cannot use it shows the archetype message (the combined archetype
        // reason is state == LockedByArchetype or the archetype not allowing the power, 0x61367F..0x613685); a bought power the
        // archetype can use shows no lock.
        [Test]
        public void TheLockMessageFollowsTheItemStateAndTheArchetype()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                DreamPowerDefinition power = NewDefinition(world.Scene.Owned, "lucid-lock-power", PowerName, NewSprite(world.Scene.Owned));
                var cost = world.Scene.Owned.Host("Lucid lock blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();
                Set(store, "m_blueCoinCost", cost); // Reflection: serialized reference the editor assigns
                Set(store, "m_slotButtons", new UIWidgetButtonDreamPowerSlot[0]); // Reflection: serialized references the editor assigns
                void Choose(UIDreamPowerStoreItemState state)
                {
                    LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    Call(store, "OnDreamPowerChanged", power, state); // Reflection: protected OnDreamPowerChanged of UIContainerDreamPowerStore, not reachable from the test
                }

                Choose(UIDreamPowerStoreItemState.Insufficient);
                Assert.That(store.LockedDescription.Value, Is.True);
                Assert.That(store.DreamPowerLockedDescription.Value, Is.EqualTo(Insufficient));

                Choose(UIDreamPowerStoreItemState.Bought);
                Assert.That(store.LockedDescription.Value, Is.False, "the unrestricted archetype can use the power");
                Assert.That(store.ActionLabel.Value, Is.EqualTo(Equippable));

                ActivateMission(world.Scene, CharacterArchetype.Climbing, true);
                Call(store, "SetCharacterArchetype");
                Choose(UIDreamPowerStoreItemState.Bought);
                Assert.That(store.LockedDescription.Value, Is.True, "the climbing archetype cannot use the power");
                Assert.That(store.DreamPowerLockedDescription.Value, Is.EqualTo(ByArchetype));
            });
        }

        // CombinedArchetypeIcon (0x0200086c) is a private serialized class: SetUp 0x614260 wraps the icon reference and refills the
        // archetype set from the serialized list (clearing it first, 0x61433A), ArchetypesMatch 0x616410 is SetEquals on that set.
        [Test]
        public void ACombinedIconMatchesExactlyItsArchetypeSet()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                object icon = NewCombinedIcon(CharacterArchetype.Climbing, CharacterArchetype.Flying, CharacterArchetype.Climbing);
                var set = Get<HashSet<CharacterArchetype>>(icon, "ArchetypeSet");
                set.Add(CharacterArchetype.Dashing); // left over from an earlier set-up

                Call(icon, "SetUp"); // Reflection: CombinedArchetypeIcon is a private nested class of UIContainerDreamPowerStore

                Assert.That(set, Is.EquivalentTo(new[] { CharacterArchetype.Climbing, CharacterArchetype.Flying }), "the stale entry is cleared, the duplicate collapsed");
                Assert.That(Call(icon, "get_Icon"), Is.Not.Null); // Reflection: Icon getter of the private nested CombinedArchetypeIcon
                Assert.That(Call(icon, "ArchetypesMatch", new[] { CharacterArchetype.Flying, CharacterArchetype.Climbing }), Is.EqualTo(true), "order does not matter"); // Reflection: CombinedArchetypeIcon is a private nested class of UIContainerDreamPowerStore
                Assert.That(Call(icon, "ArchetypesMatch", new[] { CharacterArchetype.Climbing }), Is.EqualTo(false), "a subset does not match"); // Reflection: CombinedArchetypeIcon is a private nested class of UIContainerDreamPowerStore
                Assert.That(Call(icon, "ArchetypesMatch", new[] { CharacterArchetype.Climbing, CharacterArchetype.Flying, CharacterArchetype.Dashing }), Is.EqualTo(false), // Reflection: CombinedArchetypeIcon is a private nested class of UIContainerDreamPowerStore
                    "a superset does not match");
            });
        }

        private static object NewCombinedIcon(params CharacterArchetype[] archetypes)
        {
            Type type = typeof(UIContainerDreamPowerStore).GetNestedType("CombinedArchetypeIcon", BindingFlags.NonPublic);
            object icon = Activator.CreateInstance(type); // Reflection: the class is private to the store
            Set(icon, "m_archetypes", new List<CharacterArchetype>(archetypes)); // Reflection: authored serialized field
            Set(icon, "m_iconReference", new AssetReferenceAtlasedSprite("lucid-combined-icon")); // Reflection: authored serialized field
            return icon;
        }

        // UpdateAvailableDescription 0x615BA0 loads the icon, releases the icon the previous description kept and clears it
        // (0x615CF3..0x615D0D), then asks the store manager for the required archetypes. One required archetype shows that
        // archetype's alternate image (+0x60, 0x615F54) and keeps it as the last icon; several show the first combined icon whose
        // set is equal (SetEquals 0x61609A), and the loop stops at that match; a power usable by every archetype hides the
        // archetype art (0x616193). With no matching combined icon the previous image is kept.
        [Test]
        public void TheDescriptionShowsTheArchetypeOrTheMatchingCombinedIconAndReleasesTheLastOne()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerDreamPowerStore store = NewStore(world);
                Owned owned = world.Scene.Owned;
                var alternate = new Dictionary<CharacterArchetype, Sprite>();
                foreach (CharacterArchetype archetype in new[] { CharacterArchetype.Climbing, CharacterArchetype.Flying, CharacterArchetype.Dashing })
                {
                    var definition = Create<CharacterArchetypeDefinition>(owned);
                    Call(definition, "OnEnable");
                    alternate[archetype] = NewSprite(owned);
                    Cache(definition.AlternateImage, alternate[archetype]);
                    world.Scene.Data.CharacterArchetypes[archetype] = definition;
                }
                DreamPowerDefinition single = NewDefinition(owned, "lucid-description-single", PowerName, NewSprite(owned));
                DreamPowerDefinition pair = NewDefinition(owned, "lucid-description-pair", PowerName, NewSprite(owned));
                DreamPowerDefinition unmatched = NewDefinition(owned, "lucid-description-unmatched", PowerName, NewSprite(owned));
                DreamPowerDefinition everyone = NewDefinition(owned, "lucid-description-everyone", PowerName, NewSprite(owned));
                // Three archetypes list powers, so a power usable by fewer than three has required archetypes.
                var byArchetype = Get<Dictionary<CharacterArchetype, HashSet<DreamPowerDefinition>>>(world.Store, "m_powersByArchetype");
                var archetypesByPower = Get<Dictionary<DreamPowerDefinition, HashSet<CharacterArchetype>>>(world.Store, "m_archetypesByPower");
                foreach (CharacterArchetype archetype in alternate.Keys) byArchetype[archetype] = new HashSet<DreamPowerDefinition>();
                archetypesByPower[single] = new HashSet<CharacterArchetype> { CharacterArchetype.Flying };
                archetypesByPower[pair] = new HashSet<CharacterArchetype> { CharacterArchetype.Climbing, CharacterArchetype.Dashing };
                archetypesByPower[unmatched] = new HashSet<CharacterArchetype> { CharacterArchetype.Flying, CharacterArchetype.Dashing };
                archetypesByPower[everyone] = new HashSet<CharacterArchetype>(alternate.Keys);
                object other = NewCombinedIcon(CharacterArchetype.Climbing);
                object matching = NewCombinedIcon(CharacterArchetype.Dashing, CharacterArchetype.Climbing);
                object duplicate = NewCombinedIcon(CharacterArchetype.Climbing, CharacterArchetype.Dashing);
                Sprite matchingSprite = NewSprite(owned), duplicateSprite = NewSprite(owned);
                foreach (object icon in new[] { other, matching, duplicate }) Call(icon, "SetUp"); // Reflection: CombinedArchetypeIcon is a private nested class of UIContainerDreamPowerStore
                Cache((ManagedAddressableAsset<Sprite>)Call(matching, "get_Icon"), matchingSprite); // Reflection: Icon getter of the private nested CombinedArchetypeIcon
                Cache((ManagedAddressableAsset<Sprite>)Call(duplicate, "get_Icon"), duplicateSprite); // Reflection: Icon getter of the private nested CombinedArchetypeIcon
                var icons = new List<object> { other, matching, duplicate };
                // Reflection: serialized list of a private class, which the editor fills.
                Set(store, "m_combinedArchetypeIcons", ConvertIcons(icons));

                Call(store, "UpdateAvailableDescription", single);
                Assert.That(store.ShowDescriptionArchetype.Value, Is.True);
                Assert.That(store.DescriptionArchetypeImage.Value, Is.SameAs(alternate[CharacterArchetype.Flying]));
                var kept = Get<ManagedAddressableAsset<Sprite>>(store, "m_lastArchetypeIcon");
                Assert.That(kept, Is.Not.Null);
                Assert.That(Count(kept), Is.EqualTo(1));

                Call(store, "UpdateAvailableDescription", pair);
                Assert.That(Count(kept), Is.Zero, "the previous icon is released");
                Assert.That(store.DescriptionArchetypeImage.Value, Is.SameAs(matchingSprite), "the first matching combined icon");
                Assert.That(Get<ManagedAddressableAsset<Sprite>>(store, "m_lastArchetypeIcon"), Is.SameAs(Call(matching, "get_Icon"))); // Reflection: Icon getter of the private nested CombinedArchetypeIcon
                Assert.That(Count((ManagedAddressableAsset<Sprite>)Call(duplicate, "get_Icon")), Is.Zero, "the loop stops at the first match"); // Reflection: Icon getter of the private nested CombinedArchetypeIcon

                Call(store, "UpdateAvailableDescription", unmatched);
                Assert.That(store.ShowDescriptionArchetype.Value, Is.True);
                Assert.That(store.DescriptionArchetypeImage.Value, Is.SameAs(matchingSprite), "no combined icon matches, so the image stays");
                Assert.That(Get<ManagedAddressableAsset<Sprite>>(store, "m_lastArchetypeIcon") == null, Is.True);

                Call(store, "UpdateAvailableDescription", everyone);
                Assert.That(store.ShowDescriptionArchetype.Value, Is.False);
                Assert.That(store.DescriptionArchetypeImage.Value, Is.Null);
            });
        }

        private static object ConvertIcons(List<object> icons)
        {
            Type type = typeof(UIContainerDreamPowerStore).GetNestedType("CombinedArchetypeIcon", BindingFlags.NonPublic);
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
            foreach (object icon in icons) list.Add(icon);
            return list;
        }

        private sealed class Grid
        {
            public UIContainerDreamPowerStore Store;
            public Transform[] Rows;
            public DreamPowerDefinition[] Powers;
            public GameObject Padder;
            public UIToggleGroup Group;
        }

        // A store over the given number of rows and powers: the power widgets are clones of a real widget prefab, the padder a
        // plain rect transform. Each power costs ten times its number.
        private static void WithGrid(int rowCount, int powerCount, Action<World, Grid> body) => DreamPowerStoreScenes.Run(world =>
        {
            Owned owned = world.Scene.Owned;
            var grid = new Grid { Store = NewStore(world), Rows = new Transform[rowCount], Powers = new DreamPowerDefinition[powerCount] };
            AddBands(world, out _, (DreamPowerStoreBandType.None, (Strings)9701));
            for (int i = 0; i < rowCount; i++)
                grid.Rows[i] = owned.Track(new GameObject("Lucid dream power row " + i, typeof(RectTransform))).transform;
            for (int i = 0; i < powerCount; i++)
            {
                grid.Powers[i] = NewDefinition(owned, "lucid-grid-power-" + i, PowerName, NewSprite(owned));
                Set(grid.Powers[i], "m_purchaseCost", 10 * (i + 1)); // Reflection: authored serialized field
                world.Scene.Data.DreamPowerDefinitions[grid.Powers[i].GetGUID()] = grid.Powers[i];
            }
            PowerWidget prefab = NewPowerWidget(world);
            grid.Padder = owned.Track(new GameObject("Lucid dream power padder", typeof(RectTransform)));
            grid.Group = owned.Host("Lucid dream power toggle group", false).AddComponent<UIToggleGroup>();
            Set(grid.Store, "m_dreamPowerRows", Array.ConvertAll(grid.Rows, row => (RectTransform)row)); // Reflection: serialized references
            Set(grid.Store, "m_dreamPowerPrefab", prefab.Widget); // Reflection: serialized reference the editor assigns
            Set(grid.Store, "m_dreamPowerPadderPrefab", grid.Padder); // Reflection: serialized reference the editor assigns
            Set(grid.Store, "m_dreamPowersToggleGroup", grid.Group); // Reflection: serialized reference the editor assigns
            SetBlueCoins(world, 25, 0);
            Call(grid.Store, "SetUnlockedBands");
            body(world, grid);
        });

        private static string Layout(Grid grid) => string.Join("|", grid.Rows.Select(row => string.Join(",",
            Enumerable.Range(0, row.childCount).Select(index =>
            {
                var widget = row.GetChild(index).GetComponent<UIWidgetToggleDreamPower>();
                return widget != null ? widget.Cost.Value : "pad";
            }))));

        // SetupDreamPowers 0x614A60 deals the powers over the rows in turn (the row index wraps at the row count, 0x615015..0x615024),
        // each as a clone of the prefab that is set up with its parameters (CreateWidgetParameters 0x614F3F, Setup 0x614F98),
        // put in the toggle group (set_Group 0x614FAD) and recorded by definition (Dictionary.Add 0x614FC9). When the last row is
        // left part filled the remaining rows get the padder prefab (0x6150E5..0x61512F): an odd count over two rows pads the last
        // row, fewer powers than rows pad the rows after the last power, and a full last row gets none.
        [TestCase(2, 3, "10,30|20,pad", TestName = "AnOddCountOverTwoRowsPadsTheLastRow")]
        [TestCase(2, 4, "10,30|20,40", TestName = "AnEvenCountOverTwoRowsNeedsNoPadding")]
        [TestCase(3, 2, "10|20|pad", TestName = "FewerPowersThanRowsPadTheRemainingRows")]
        [TestCase(3, 3, "10|20|30", TestName = "OnePowerPerRowNeedsNoPadding")]
        [TestCase(1, 3, "10,20,30", TestName = "OneRowTakesEveryPowerWithoutPadding")]
        public void ThePowersAreDealtOverTheRowsAndTheLastRowIsPadded(int rows, int powers, string expectedLayout)
        {
            WithGrid(rows, powers, (world, grid) =>
            {
                Call(grid.Store, "SetupDreamPowers");

                Assert.That(Layout(grid), Is.EqualTo(expectedLayout));
                var table = Get<Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower>>(grid.Store, "m_widgetByDefinition");
                Assert.That(table.Keys, Is.EquivalentTo(grid.Powers));
                foreach (UIWidgetToggleDreamPower widget in table.Values)
                    Assert.That(widget.GroupElement.Group, Is.SameAs(grid.Group));
            });
        }

        // Each cloned widget is set up with the item state of its power from the coins available (25 here): a bought power shows no cost,
        // an affordable one (cost 20) and an unaffordable one (cost 30) show theirs.
        [Test]
        public void EachClonedWidgetShowsTheStateOfItsPower()
        {
            WithGrid(2, 3, (world, grid) =>
            {
                Buy(world, grid.Powers[0]);

                Call(grid.Store, "SetupDreamPowers");

                var table = Get<Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower>>(grid.Store, "m_widgetByDefinition");
                Assert.That(table[grid.Powers[0]].ShowCost.Value, Is.False, "bought");
                Assert.That(table[grid.Powers[1]].ShowCost.Value, Is.True);
                Assert.That(table[grid.Powers[1]].Cost.Value, Is.EqualTo("20"));
                Assert.That(table[grid.Powers[2]].Cost.Value, Is.EqualTo("30"));
                Assert.That(table[grid.Powers[2]].ShowCost.Value, Is.True, "an unaffordable power still shows its price");
            });
        }

        // A second SetupDreamPowers first forgets the recorded widgets (Dictionary.Clear 0x614E7C) and destroys every child of every
        // row (Object.Destroy 0x614C05); Edit Mode refuses the destruction with a logged error for each child, so the children stay.
        [Test]
        public void SettingUpAgainClearsTheRecordsAndDestroysTheChildren()
        {
            WithGrid(2, 3, (world, grid) =>
            {
                Call(grid.Store, "SetupDreamPowers");
                var first = Get<Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower>>(grid.Store, "m_widgetByDefinition").Values.ToArray();
                for (int i = 0; i < 4; i++)
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^Destroy may not be called from edit mode"));

                Call(grid.Store, "SetupDreamPowers");

                var second = Get<Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower>>(grid.Store, "m_widgetByDefinition").Values.ToArray();
                Assert.That(second.Length, Is.EqualTo(3));
                Assert.That(second.Intersect(first), Is.Empty, "new clones are recorded");
            });
        }
    }
}
