using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.DreamPowerStoreScenes;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetToggleDreamPower (Game.Runtime 0x020009d2, x86-64 0x6A3320..0x6A3C60) as a real component over a real toggle,
    // transition, group element and navigation component, a real DreamPowerStoreManager (reached through the widget's static
    // reference), and the DataManager's band table and band group. The widget plays its animations through the transition, which
    // queues each clip and plays it at once: only the last clip of a sequence is observable.
    public sealed class UIWidgetToggleDreamPowerTests
    {
        private const Strings FirstBandName = (Strings)9201, SecondBandName = (Strings)9202, PowerName = (Strings)9203;

        private sealed class Rig
        {
            public World World;
            public PowerWidget Rigs;
            public UIWidgetToggleDreamPower Widget => Rigs.Widget;
            public DreamPowerDefinition Power;
            public Sprite PowerIcon;
            public DreamPowerStoreBandDefinition[] Bands;
            public Sprite[] Backgrounds;
            public readonly List<DreamPowerDefinition> Reported = new List<DreamPowerDefinition>();
            public int SelectedEvents;
        }

        private static void Run(Action<Rig> body, DreamPowerStoreBandType powerBand = DreamPowerStoreBandType.Band2) => DreamPowerStoreScenes.Run(world =>
        {
            var rig = new Rig { World = world };
            rig.Bands = AddBands(world, out rig.Backgrounds, (DreamPowerStoreBandType.None, FirstBandName), (DreamPowerStoreBandType.Band2, SecondBandName));
            rig.PowerIcon = NewSprite(world.Scene.Owned);
            rig.Power = NewDefinition(world.Scene.Owned, "lucid-toggle-power", PowerName, rig.PowerIcon);
            Set(rig.Power, "m_purchaseCost", 35); // Reflection: authored serialized field
            Set(rig.Power, "m_band", powerBand); // Reflection: authored serialized field
            rig.Rigs = NewPowerWidget(world);
            var selected = new UnityEvent();
            selected.AddListener(() => rig.SelectedEvents++);
            Set(rig.Widget, "m_onSelected", selected); // Reflection: authored serialized event
            body(rig);
        });

        private static UIWidgetToggleDreamPowerSlotParameters Parameters(Rig rig, UIDreamPowerStoreItemState state, bool available = true) =>
            new UIWidgetToggleDreamPowerSlotParameters(rig.Power, state, available, rig.Reported.Add);

        private static bool Playing(Rig rig, AnimationClip clip) => rig.Rigs.Transitions.Animation.IsPlaying(clip.name);

        // The group element and navigation getters 0x6A3320 and 0x6A3330 return the serialized references.
        [Test]
        public void TheGroupElementAndTheNavigationAreTheSerializedReferences()
        {
            Run(rig =>
            {
                Assert.That(rig.Widget.GroupElement, Is.SameAs(rig.Rigs.GroupElement));
                Assert.That(rig.Widget.UIWidgetNavigation, Is.SameAs(rig.Rigs.Navigation));
            });
        }

        // Setup 0x6A3340: the base setup first, then the parameters are kept (+0xC8) and the power's band is looked up through the
        // store manager (GetBandDefinition 0x6A3443). The tier name is the band's name (+0x38), the tier index its one-based
        // position in the band group (GetBandIndex 0x6A3487, inc 0x6A3493), the background the band's slot background (+0x50, Load).
        // The debug name is set to string.Empty, the icon loads (LoadAsync), the cost is the purchase cost (+0x30), and the
        // navigation is enabled by the Available flag (+0x1C, 0x6A3591..0x6A35A2).
        [Test]
        public void SetupPublishesTheTierTheBackgroundTheIconAndTheCost()
        {
            Run(rig =>
            {
                rig.Widget.DebugName.Value = "stale";

                rig.Widget.Setup(Parameters(rig, UIDreamPowerStoreItemState.Purchasable, false));

                Assert.That(rig.Widget.TierName.Value, Is.EqualTo(SecondBandName));
                Assert.That(rig.Widget.TierIndex.Value, Is.EqualTo("2"));
                Assert.That(rig.Widget.BackgroundImage.Value, Is.SameAs(rig.Backgrounds[1]));
                Assert.That(Count(rig.Bands[1].SlotLockedBackground), Is.EqualTo(1));
                Assert.That(rig.Widget.DebugName.Value, Is.Not.Null.And.Empty);
                Assert.That(rig.Widget.Icon.Value, Is.SameAs(rig.PowerIcon));
                Assert.That(Count(rig.Power.IconAsset), Is.EqualTo(1));
                Assert.That(rig.Widget.Cost.Value, Is.EqualTo("35"));
                Assert.That(rig.Rigs.Navigation.enabled, Is.False);

                rig.Widget.Setup(Parameters(rig, UIDreamPowerStoreItemState.Purchasable, true));
                Assert.That(rig.Rigs.Navigation.enabled, Is.True);
            });
        }

        // The cost is formatted by Int32.ToString() with the current culture (0x6A3575): a "~" negative sign shows in the text.
        [Test]
        public void SetupFormatsTheCostWithTheCurrentCulture()
        {
            Run(rig =>
            {
                Set(rig.Power, "m_purchaseCost", -5); // Reflection: authored serialized field

                TildeNegativeSignCulture.Run(() => rig.Widget.Setup(Parameters(rig, UIDreamPowerStoreItemState.Purchasable)));

                Assert.That(rig.Widget.Cost.Value, Is.EqualTo("~5"));
            });
        }

        // A power in the first band is tier one.
        [Test]
        public void ThePowerOfTheFirstBandIsTierOne()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, UIDreamPowerStoreItemState.Bought));

                Assert.That(rig.Widget.TierName.Value, Is.EqualTo(FirstBandName));
                Assert.That(rig.Widget.TierIndex.Value, Is.EqualTo("1"));
                Assert.That(rig.Widget.BackgroundImage.Value, Is.SameAs(rig.Backgrounds[0]));
            }, DreamPowerStoreBandType.None);
        }

        // SetVisualState 0x6A35C0 clears ShowEquipped and ShowCost, queues the clear-selected animation, then switches on the item
        // state through the jump table at 0x6A36CC: the locked states clear ShowSelected and play their own clips; Bought plays the
        // available clip and leaves ShowSelected alone; Purchasable shows the cost with the purchasable clip; Equipped shows the
        // equipped flag with the equipped clip. A state past Equipped takes the Purchasable branch (ja 0x6A3673).
        [Test]
        public void EachItemStateShowsItsFlagsAndPlaysItsClip()
        {
            Run(rig =>
            {
                var rows = new (UIDreamPowerStoreItemState state, bool equipped, bool cost, bool selected, Func<AnimationClip> clip)[]
                {
                    (UIDreamPowerStoreItemState.LockedByProgression, false, false, false, () => rig.Rigs.Locked),
                    (UIDreamPowerStoreItemState.LockedByArchetype, false, false, false, () => rig.Rigs.LockedByArchetype),
                    (UIDreamPowerStoreItemState.Bought, false, false, true, () => rig.Rigs.Available),
                    (UIDreamPowerStoreItemState.Purchasable, false, true, true, () => rig.Rigs.Purchasable),
                    (UIDreamPowerStoreItemState.Insufficient, false, true, true, () => rig.Rigs.Purchasable),
                    (UIDreamPowerStoreItemState.Equipped, true, false, true, () => rig.Rigs.Equipped),
                    ((UIDreamPowerStoreItemState)6, false, true, true, () => rig.Rigs.Purchasable),
                };
                foreach (var row in rows)
                {
                    rig.Widget.ShowEquipped.Value = !row.equipped;
                    rig.Widget.ShowCost.Value = !row.cost;
                    rig.Widget.ShowSelected.Value = true;

                    rig.Widget.Setup(Parameters(rig, row.state));

                    string label = row.state.ToString();
                    Assert.That(rig.Widget.ShowEquipped.Value, Is.EqualTo(row.equipped), label);
                    Assert.That(rig.Widget.ShowCost.Value, Is.EqualTo(row.cost), label);
                    Assert.That(rig.Widget.ShowSelected.Value, Is.EqualTo(row.selected), label);
                    Assert.That(Playing(rig, row.clip()), Is.True, label);
                }
            });
        }

        // OnSelected 0x6A3790 with the toggle on: the selection event fires (0x6A37A6), ShowSelected is the unsigned state >= 2
        // (cmp 2, setae at 0x6A37B6..0x6A37C1), so a negative state selects too, the selected clip plays last (0x6A37F2), and the callback gets the definition.
        [TestCase(UIDreamPowerStoreItemState.LockedByProgression, false, TestName = "SelectingAProgressionLockedPowerShowsNoSelection")]
        [TestCase(UIDreamPowerStoreItemState.LockedByArchetype, false, TestName = "SelectingAnArchetypeLockedPowerShowsNoSelection")]
        [TestCase(UIDreamPowerStoreItemState.Bought, true, TestName = "SelectingABoughtPowerShowsTheSelection")]
        [TestCase(UIDreamPowerStoreItemState.Purchasable, true, TestName = "SelectingAPurchasablePowerShowsTheSelection")]
        [TestCase(UIDreamPowerStoreItemState.Equipped, true, TestName = "SelectingAnEquippedPowerShowsTheSelection")]
        [TestCase((UIDreamPowerStoreItemState)(-1), true, TestName = "SelectingANegativeStatePowerShowsTheSelection")]
        public void SelectingReportsThePowerAndShowsTheSelectionUnlessLocked(UIDreamPowerStoreItemState state, bool shown)
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, state));
                rig.Widget.ShowSelected.Value = !shown;

                rig.Widget.Action_OnToggleIsOn(true);

                Assert.That(rig.SelectedEvents, Is.EqualTo(1));
                Assert.That(rig.Widget.ShowSelected.Value, Is.EqualTo(shown));
                Assert.That(Playing(rig, rig.Rigs.Selected), Is.True);
                Assert.That(rig.Reported, Is.EqualTo(new[] { rig.Power }));
            });
        }

        // OnSelected with the toggle off (0x6A381A): the selection is cleared and the item-state visuals return; the callback is not
        // called and the selection event does not fire.
        [Test]
        public void DeselectingClearsTheSelectionAndRestoresTheItemStateVisuals()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, UIDreamPowerStoreItemState.Equipped));
                rig.Widget.Action_OnToggleIsOn(true);
                rig.Widget.ShowEquipped.Value = false;

                rig.Widget.Action_OnToggleIsOn(false);

                Assert.That(rig.Widget.ShowSelected.Value, Is.False);
                Assert.That(rig.Widget.ShowEquipped.Value, Is.True);
                Assert.That(Playing(rig, rig.Rigs.Equipped), Is.True);
                Assert.That(rig.SelectedEvents, Is.EqualTo(1), "only the selecting call fired the event");
                Assert.That(rig.Reported.Count, Is.EqualTo(1));
            });
        }

        // Action_OnNavigatedTo 0x6A3850 switches the toggle on (set_IsOn 0x6A3864) and then runs the selection sequence of OnSelected
        // (event, ShowSelected, selected clip, callback with the definition); Action_OnNavigatedAway 0x6A38F0 switches the toggle off,
        // clears ShowSelected and restores the item-state visuals.
        [Test]
        public void NavigatingToAndAwayTogglesTheWidgetAndRunsTheSelectionSequence()
        {
            Run(rig =>
            {
                rig.Widget.Setup(Parameters(rig, UIDreamPowerStoreItemState.Bought));
                Assert.That(rig.Rigs.Toggle.IsOn, Is.False);

                rig.Widget.Action_OnNavigatedTo();

                Assert.That(rig.Rigs.Toggle.IsOn, Is.True);
                Assert.That(rig.SelectedEvents, Is.EqualTo(1));
                Assert.That(rig.Widget.ShowSelected.Value, Is.True);
                Assert.That(Playing(rig, rig.Rigs.Selected), Is.True);
                Assert.That(rig.Reported, Is.EqualTo(new[] { rig.Power }));

                rig.Widget.Action_OnNavigatedAway();

                Assert.That(rig.Rigs.Toggle.IsOn, Is.False);
                Assert.That(rig.Widget.ShowSelected.Value, Is.False);
                Assert.That(Playing(rig, rig.Rigs.Available), Is.True);
                Assert.That(rig.Reported.Count, Is.EqualTo(1));
            });
        }

        // OnDestroy 0x6A36F0 releases the power's icon (through the stored parameters, 0x6A3733) and the band's slot background
        // (0x6A3767); a widget that was never set up releases nothing (null parameters at 0x6A371C, no band through the Unity check).
        [Test]
        public void DestroyReleasesTheIconAndTheBandBackgroundOnlyAfterSetup()
        {
            Run(rig =>
            {
                Assert.DoesNotThrow(() => Call(rig.Widget, "OnDestroy"), "nothing was retained");

                rig.Widget.Setup(Parameters(rig, UIDreamPowerStoreItemState.Bought));
                Assert.That(Count(rig.Power.IconAsset), Is.EqualTo(1));
                Assert.That(Count(rig.Bands[1].SlotLockedBackground), Is.EqualTo(1));

                Call(rig.Widget, "OnDestroy");

                Assert.That(Count(rig.Power.IconAsset), Is.Zero);
                Assert.That(Count(rig.Bands[1].SlotLockedBackground), Is.Zero);
            });
        }
    }
}
