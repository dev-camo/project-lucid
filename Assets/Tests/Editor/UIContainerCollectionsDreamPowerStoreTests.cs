using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.DreamPowerStoreScenes;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCollectionsDreamPowerStore (Game.Runtime 0x02000869, x86-64 0x612EB0..0x613F10, closure class 0x0200086a) as a real
    // component over the dream power store scene (see UIContainerDreamPowerStoreTests for the services it takes). OnEnable runs the
    // base store's own OnEnable with no power rows, since the rows would select through a UINavigationManager.
    public sealed class UIContainerCollectionsDreamPowerStoreTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Parts
        {
            public UIContainerCollectionsDreamPowerStore Store;
            public UIWidgetButtonImageCharacter InfoButton;
            public Button InfoUiButton;
            public GameObject InfoGlyph;
        }

        private static Parts WithInfoParts(World world, bool present)
        {
            var parts = new Parts { Store = NewStore<UIContainerCollectionsDreamPowerStore>(world) };
            WireForEnable(world, parts.Store);
            if (present)
            {
                Owned owned = world.Scene.Owned;
                GameObject buttonHost = owned.Host("Lucid collections store info button", false);
                parts.InfoButton = buttonHost.AddComponent<UIWidgetButtonImageCharacter>();
                parts.InfoUiButton = buttonHost.AddComponent<Button>();
                Set(parts.InfoButton, "m_uiButton", parts.InfoUiButton); // Reflection: serialized reference the editor assigns
                parts.InfoGlyph = owned.Host("Lucid collections store info glyph", true);
                Set(parts.Store, "m_infoButton", parts.InfoButton); // Reflection: serialized reference the editor assigns
                Set(parts.Store, "m_infoGlyph", parts.InfoGlyph); // Reflection: serialized reference the editor assigns
            }
            return parts;
        }

        private static void WithStringTable(System.Action body) =>
            OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.FORMAT_X_OF_Y, "{0} of {1}" } }, body);

        // OnEnable 0x613010 runs the base store's OnEnable first (0x61303C), publishes the literal 0x47c94108
        // (Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE) as the title (0x613057), switches the info button off through the Unity null
        // check (op_Inequality 0x613083, SetInteractable 0x613097) and the info glyph off (0x6130CF), and ends with this class's
        // own UpdateTitle (0x6130E1).
        [Test]
        public void EnablingShowsTheCollectionsTitleAndSwitchesTheInfoPartsOff()
        {
            WithStringTable(() => DreamPowerStoreScenes.Run(world =>
            {
                Parts parts = WithInfoParts(world, true);
                parts.Store.Available.Value = false;
                Assert.That(parts.InfoUiButton.interactable, Is.True);
                Assert.That(parts.InfoGlyph.activeSelf, Is.True);

                Call(parts.Store, "OnEnable");

                Assert.That(parts.Store.Available.Value, Is.True, "the base OnEnable ran");
                Assert.That(parts.Store.Title.Value, Is.EqualTo(Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE));
                Assert.That(parts.InfoUiButton.interactable, Is.False);
                Assert.That(parts.InfoGlyph.activeSelf, Is.False);
                Assert.That(parts.Store.HasArchetype.Value, Is.False, "no mission archetype");
            }));
        }

        // The info button and glyph are optional: without them (a null Unity reference, 0x613088 and 0x6130C0) OnEnable neither faults
        // nor skips the title.
        [Test]
        public void EnablingWithoutTheInfoPartsStillShowsTheTitle()
        {
            WithStringTable(() => DreamPowerStoreScenes.Run(world =>
            {
                Parts parts = WithInfoParts(world, false);

                Assert.DoesNotThrow(() => Call(parts.Store, "OnEnable"));

                Assert.That(parts.Store.Title.Value, Is.EqualTo(Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE));
            }));
        }

        // UpdateTitle 0x613360 (this class's own method, not the base's): without an archetype (m_characterArchetype at +0x160 is
        // zero) the archetype art is hidden (0x613441); otherwise it is shown and the archetype's UI image (+0x58, not the
        // alternate or silhouette image the base title uses) loads into CharacterArchetypeImage (0x6133F5..0x61343C).
        [Test]
        public void TheTitleShowsTheUiImageOfTheMissionsArchetype()
        {
            WithStringTable(() => DreamPowerStoreScenes.Run(world =>
            {
                Parts parts = WithInfoParts(world, false);
                ArchetypeImages images = AddArchetype(world, CharacterArchetype.Climbing);
                parts.Store.HasArchetype.Value = true;

                Call(parts.Store, "UpdateTitle");
                Assert.That(parts.Store.HasArchetype.Value, Is.False);
                Assert.That(parts.Store.CharacterArchetypeImage.Value, Is.Null);

                ActivateMission(world.Scene, CharacterArchetype.Climbing, true);
                Call(parts.Store, "SetCharacterArchetype");
                Call(parts.Store, "UpdateTitle");

                Assert.That(parts.Store.HasArchetype.Value, Is.True);
                Assert.That(parts.Store.CharacterArchetypeImage.Value, Is.SameAs(images.Ui));
                Assert.That(Count(world.Scene.Data.CharacterArchetypes[CharacterArchetype.Climbing].UIImage), Is.EqualTo(1));
            }));
        }

        // A store whose base OnDreamPowerChanged can run: a cost container and no slot buttons (the callback queued on the next frame
        // is refused by the inactive coroutine host).
        private static UIContainerCollectionsDreamPowerStore ChoosableStore(World world)
        {
            UIContainerCollectionsDreamPowerStore store = NewStore<UIContainerCollectionsDreamPowerStore>(world);
            var cost = world.Scene.Owned.Host("Lucid collections blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();
            Set(store, "m_blueCoinCost", cost); // Reflection: serialized reference the editor assigns
            Set(store, "m_slotButtons", new UIWidgetButtonDreamPowerSlot[0]); // Reflection: serialized references the editor assigns
            return store;
        }

        // OnDreamPowerChanged 0x613450 runs the base update (0x61345C) and then reports the selection through OnSelectionChanged
        // (0x613488, no null check: the field starts as an empty callback): Strings.COMMON_MENU_UNLOCK (0xccd99f8d) for a
        // Purchasable power (cmp 3 / cmove, 0x613461..0x61346E) and Strings.NONE (0xb6fa1284) for every other state.
        [Test]
        public void ChoosingAPowerReportsTheUnlockPromptOnlyForAPurchasableOne()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerCollectionsDreamPowerStore store = ChoosableStore(world);
                var received = new List<Strings>();
                DreamPowerDefinition power = NewDefinition(world.Scene.Owned, "lucid-collections-chosen", (Strings)9801, NewSprite(world.Scene.Owned));
                void Choose(UIDreamPowerStoreItemState state)
                {
                    LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    Call(store, "OnDreamPowerChanged", power, state);
                }

                Choose(UIDreamPowerStoreItemState.Bought); // an empty callback list does not fault
                store.OnSelectionChanged += received.Add;
                Choose(UIDreamPowerStoreItemState.Purchasable);
                Choose(UIDreamPowerStoreItemState.Insufficient);
                Choose(UIDreamPowerStoreItemState.Equipped);

                Assert.That(received, Is.EqualTo(new[] { Strings.COMMON_MENU_UNLOCK, Strings.NONE, Strings.NONE }));
                Assert.That(world.Store.LastSelectedDefinition, Is.SameAs(power), "the base update ran");
            });
        }

        // The event accessors 0x612EB0 and 0x612F60 combine and remove subscribers: a removed handler no longer hears the selection.
        [Test]
        public void ARemovedSelectionHandlerNoLongerHearsTheSelection()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerCollectionsDreamPowerStore store = ChoosableStore(world);
                var kept = new List<Strings>();
                var removed = new List<Strings>();
                DreamPowerDefinition power = NewDefinition(world.Scene.Owned, "lucid-collections-removed", (Strings)9801, NewSprite(world.Scene.Owned));
                System.Action<Strings> removable = removed.Add;
                store.OnSelectionChanged += kept.Add;
                store.OnSelectionChanged += removable;
                store.OnSelectionChanged -= removable;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Call(store, "OnDreamPowerChanged", power, UIDreamPowerStoreItemState.Purchasable);

                Assert.That(kept, Is.EqualTo(new[] { Strings.COMMON_MENU_UNLOCK }));
                Assert.That(removed, Is.Empty);
            });
        }

        // OnNavigatedAway 0x613850 only clears the Purchasable binder (set_Value false, 0x61386F).
        [Test]
        public void NavigatingAwayClearsThePurchasableFlag()
        {
            DreamPowerStoreScenes.Run(world =>
            {
                UIContainerCollectionsDreamPowerStore store = NewStore<UIContainerCollectionsDreamPowerStore>(world);
                store.Purchasable.Value = true;
                store.Available.Value = true;

                store.OnNavigatedAway();

                Assert.That(store.Purchasable.Value, Is.False);
                Assert.That(store.Available.Value, Is.True);
            });
        }
    }
}
