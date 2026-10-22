using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using RTLTMPro;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCollections (Game.Runtime 0x02000896, x86-64 0x625200..0x626640) as a real component over the character select
    // scene (a registered App with its graph storage), with a real tab toggle group, a real music page and player, and a real dream
    // power store page. Start is not run: it asks the process manager for the ornament and music track managers (whose constructors
    // subscribe to the level, mission and save managers) and subscribes to the gallery container. The App reference it takes is
    // assigned to the container's field as Start assigns it. Not covered because they report the tab to the analytics collector, whose
    // static references are shared by the whole process: Start, the badge setup of the gallery and music toggles, turning a tab on
    // from another tab, the badge refresh handlers (they read the ornament and music track managers, which Start takes), and the activation of the ornament viewer scene.
    public sealed class UIContainerCollectionsTests
    {
        private sealed class Rig
        {
            public Scene Scene;
            public UIContainerCollections Collections;
            public UIToggleGroup Tabs;
            public UIToggleGroupElement[] TabElements;
            public UIToggle[] TabToggles;
            public UIContainerCollectionsDreamPowerStore Store;
            public Animation PlayingAnimation;
            public AnimationClip PlayingClip;
        }

        private static void Run(Action<Rig> body) => CharacterSelectScene.Run(scene =>
        {
            Owned owned = scene.Owned;
            owned.LeaseRow<AudioManager>(); // The music player's field initialisers cache this row
            var rig = new Rig { Scene = scene };
            rig.Collections = owned.Host("Lucid collections", false).AddComponent<UIContainerCollections>();
            Set(rig.Collections, "m_app", scene.App); // Reflection: the reference Start takes from the process manager

            rig.Tabs = owned.Host("Lucid collections tabs", false).AddComponent<UIToggleGroup>();
            Set(rig.Tabs, "m_allowSwitchOff", true); // Reflection: authored serialized field
            rig.TabElements = new UIToggleGroupElement[3];
            rig.TabToggles = new UIToggle[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject host = owned.Host("Lucid collections tab " + i, false);
                rig.TabToggles[i] = host.AddComponent<UIToggle>();
                rig.TabElements[i] = host.AddComponent<UIToggleGroupElement>();
                Set(rig.TabElements[i], "m_toggle", rig.TabToggles[i]); // Reflection: serialized reference the editor Reset fills
                rig.Tabs.RegisterToggle(rig.TabElements[i]);
            }
            Set(rig.Collections, "m_tabsGroup", rig.Tabs); // Reflection: serialized reference the editor assigns

            rig.Store = owned.Host("Lucid collections store", false).AddComponent<UIContainerCollectionsDreamPowerStore>();
            Set(rig.Collections, "m_dreamPowerCollections", rig.Store); // Reflection: serialized reference the editor assigns

            var music = owned.Host("Lucid collections music", false).AddComponent<UIContainerCollectionsMusic>();
            var player = owned.Host("Lucid collections music player", false).AddComponent<UIContainerMusicPlayer>();
            GameObject animationHost = owned.Host("Lucid collections playing animation", true);
            rig.PlayingAnimation = animationHost.AddComponent<Animation>();
            rig.PlayingAnimation.playAutomatically = false;
            rig.PlayingClip = AddClip(owned, rig.PlayingAnimation, "collections playing");
            Set(player, "m_playingAnimations", new[] { rig.PlayingAnimation }); // Reflection: serialized references the editor assigns
            Set(music, "m_musicPlayer", player); // Reflection: serialized reference the editor assigns
            Set(rig.Collections, "m_musicCollectionsContainer", music); // Reflection: serialized reference the editor assigns
            body(rig);
        });

        private static bool StoredFlag(Rig rig, GraphStorageKey key) => rig.Scene.App.Storage.GetValue(key, false, false);

        // OnControllerPromptUpdated 0x626450 copies the prompt (0x626473) and shows the select button unless the prompt is
        // Strings.NONE (0xb6fa1284, setne at 0x626482).
        [Test]
        public void ThePromptIsCopiedAndTheSelectButtonIsShownOnlyForARealPrompt()
        {
            Run(rig =>
            {
                Call(rig.Collections, "OnControllerPromptUpdated", Strings.COMMON_MENU_UNLOCK); // Reflection: private UIContainerCollections.OnControllerPromptUpdated
                Assert.That(rig.Collections.SelectButtonPrompt.Value, Is.EqualTo(Strings.COMMON_MENU_UNLOCK));
                Assert.That(rig.Collections.SelectButtonShow.Value, Is.True);

                Call(rig.Collections, "OnControllerPromptUpdated", Strings.NONE); // Reflection: private UIContainerCollections.OnControllerPromptUpdated
                Assert.That(rig.Collections.SelectButtonPrompt.Value, Is.EqualTo(Strings.NONE));
                Assert.That(rig.Collections.SelectButtonShow.Value, Is.False);
            });
        }

        // The toggles carry no exclusive group in this rig, so each step starts with only the given tab on and reports the tab the
        // input turned on (the given tab again when it moved nowhere).
        private static int Step(Rig rig, int from, Action move)
        {
            for (int i = 0; i < rig.TabToggles.Length; i++)
                rig.TabToggles[i].SetIsOnWithoutNotify(i == from);
            move();
            for (int i = 0; i < rig.TabToggles.Length; i++)
                if (i != from && rig.TabToggles[i].IsOn) return i;
            return from;
        }

        // OnInputTabBackwards 0x6264A0 and OnInputTabForwards 0x6264C0 compare the input with the constant 0.5 (0x29a6c08) and only
        // an input of at least a half (ucomiss/jbe: also an unordered one) moves the tab group (SelectPrevious 0x1a3f010,
        // SelectNext 0x1a3ee40); the group cycles.
        [Test]
        public void TheTabInputsMoveTheTabGroupFromAHalfUpwards()
        {
            Run(rig =>
            {
                Assert.That(Step(rig, 1, () => Call(rig.Collections, "OnInputTabForwards", 0.49f)), Is.EqualTo(1), "forwards below a half"); // Reflection: private UIContainerCollections.OnInputTabForwards
                Assert.That(Step(rig, 1, () => Call(rig.Collections, "OnInputTabForwards", 0.5f)), Is.EqualTo(2), "forwards from exactly a half"); // Reflection: private UIContainerCollections.OnInputTabForwards
                Assert.That(Step(rig, 2, () => Call(rig.Collections, "OnInputTabForwards", 1f)), Is.EqualTo(0), "forwards cycles"); // Reflection: private UIContainerCollections.OnInputTabForwards

                Assert.That(Step(rig, 0, () => Call(rig.Collections, "OnInputTabBackwards", 0.1f)), Is.EqualTo(0), "backwards below a half"); // Reflection: private UIContainerCollections.OnInputTabBackwards
                Assert.That(Step(rig, 0, () => Call(rig.Collections, "OnInputTabBackwards", 0.5f)), Is.EqualTo(2), "backwards cycles"); // Reflection: private UIContainerCollections.OnInputTabBackwards
                Assert.That(Step(rig, 2, () => Call(rig.Collections, "OnInputTabBackwards", 1f)), Is.EqualTo(1)); // Reflection: private UIContainerCollections.OnInputTabBackwards

                Assert.That(Step(rig, 1, () => Call(rig.Collections, "OnInputTabForwards", float.NaN)), Is.EqualTo(2), "an unordered input is not below a half"); // Reflection: private UIContainerCollections.OnInputTabForwards
            });
        }

        // Update 0x625A40 reads the CollectionsUnlockInProgress flag from the App's graph storage (0x625B3E) and copies it into
        // NavigationEnabled when the two differ (0x625B5B).
        [Test]
        public void NavigationFollowsTheStoredUnlockInProgressFlag()
        {
            Run(rig =>
            {
                rig.Scene.App.Storage.SetValue(AppFSMKeys.CollectionsUnlockInProgress, true);
                Call(rig.Collections, "Update"); // Reflection: Unity sends Update; Edit Mode does not
                Assert.That(rig.Collections.NavigationEnabled.Value, Is.True);

                rig.Scene.App.Storage.SetValue(AppFSMKeys.CollectionsUnlockInProgress, false);
                Call(rig.Collections, "Update"); // Reflection: Unity sends Update; Edit Mode does not
                Assert.That(rig.Collections.NavigationEnabled.Value, Is.False);
            });
        }

        // OnMusicToggle 0x625FB0 clears the store's Purchasable flag first (0x626000). Turning the tab off, or switching it on while it
        // is already the current tab (cmp 1 at 0x62602E), only resets the playing animations of the music player (0x6260 .. 0x62604E):
        // the tab, the select button and the stored tab are left alone.
        [TestCase(false, UICollectionsTab.DreamPower, TestName = "TurningTheMusicTabOffOnlyStopsThePlayingAnimations")]
        [TestCase(true, UICollectionsTab.Music, TestName = "ReselectingTheCurrentMusicTabOnlyStopsThePlayingAnimations")]
        public void TheMusicToggleThatChangesNothingStopsThePlayingAnimations(bool isOn, UICollectionsTab current)
        {
            Run(rig =>
            {
                rig.Collections.CollectionsTab.Value = current;
                rig.Store.Purchasable.Value = true;
                rig.PlayingAnimation.Play(rig.PlayingClip.name);
                Assert.That(rig.PlayingAnimation.IsPlaying(rig.PlayingClip.name), Is.True, "set-up: the animation plays");

                Call(rig.Collections, "OnMusicToggle", isOn); // Reflection: private UIContainerCollections.OnMusicToggle

                Assert.That(rig.Store.Purchasable.Value, Is.False);
                Assert.That(rig.PlayingAnimation.IsPlaying(rig.PlayingClip.name), Is.False);
                Assert.That(rig.Collections.CollectionsTab.Value, Is.EqualTo(current));
                Assert.That(rig.Collections.SelectButtonShow.Value, Is.False);
                Assert.That(rig.Collections.SelectButtonPrompt.Value, Is.EqualTo(default(Strings)));
            });
        }

        // OnDreamPowerToggle 0x6261A0 returns at once when the toggle goes off or the store is already the current tab (cmp 2 at
        // 0x626210): neither the info flag nor the last opened tab is stored.
        [TestCase(false, UICollectionsTab.Music, TestName = "TurningTheStoreTabOffChangesNothing")]
        [TestCase(true, UICollectionsTab.DreamPower, TestName = "ReselectingTheCurrentStoreTabChangesNothing")]
        public void TheStoreToggleThatChangesNothingStoresNothing(bool isOn, UICollectionsTab current)
        {
            Run(rig =>
            {
                rig.Collections.CollectionsTab.Value = current;
                rig.Scene.App.Storage.SetValue(AppFSMKeys.CollectionsDreamPowerInfoReady, false);
                rig.Scene.App.Storage.SetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.Gallery);

                Call(rig.Collections, "OnDreamPowerToggle", isOn); // Reflection: private UIContainerCollections.OnDreamPowerToggle

                Assert.That(rig.Collections.CollectionsTab.Value, Is.EqualTo(current));
                Assert.That(StoredFlag(rig, AppFSMKeys.CollectionsDreamPowerInfoReady), Is.False);
                Assert.That(rig.Scene.App.Storage.GetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.Music, false), Is.EqualTo(UICollectionsTab.Gallery));
            });
        }

        // OnGalleryToggle 0x625BA0 clears the store's Purchasable flag (0x625C0B) and, with the toggle off, does nothing else
        // (0x625C14). Switching it on while the gallery is already the current tab skips the analytics event (0x625C2B..0x625C33),
        // sets the tab, shows the select button with Strings.COMMON_MENU_SELECT (0xedcfc5a2) and stores the tab as the last opened.
        [Test]
        public void TheGalleryToggleShowsTheSelectButtonAndStoresTheTab()
        {
            Run(rig =>
            {
                rig.Scene.App.Storage.SetValue(AppFSMKeys.LoadedLevels, new Dictionary<string, AsyncOperationHandle>()); // as the App sets it on Awake
                rig.Scene.App.Storage.SetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.Music);
                var level = rig.Scene.Owned.Track(ScriptableObject.CreateInstance<LevelDefinition>());
                Set(level, "m_guid", "lucid-ornament-viewer"); // Reflection: authored GUID of a serialized definition
                Set(rig.Collections, "m_ornamentViewerLevelDefinition", level); // Reflection: serialized reference the editor assigns
                rig.Store.Purchasable.Value = true;

                Call(rig.Collections, "OnGalleryToggle", false); // Reflection: private UIContainerCollections.OnGalleryToggle
                Assert.That(rig.Store.Purchasable.Value, Is.False);
                Assert.That(rig.Collections.SelectButtonShow.Value, Is.False);
                Assert.That(rig.Scene.App.Storage.GetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.Gallery, false), Is.EqualTo(UICollectionsTab.Music));

                rig.Store.Purchasable.Value = true;
                Call(rig.Collections, "OnGalleryToggle", true); // Reflection: private UIContainerCollections.OnGalleryToggle

                Assert.That(rig.Store.Purchasable.Value, Is.False);
                Assert.That(rig.Collections.CollectionsTab.Value, Is.EqualTo(UICollectionsTab.Gallery));
                Assert.That(rig.Collections.SelectButtonPrompt.Value, Is.EqualTo(Strings.COMMON_MENU_SELECT));
                Assert.That(rig.Collections.SelectButtonShow.Value, Is.True);
                Assert.That(rig.Scene.App.Storage.GetValue(AppFSMKeys.CollectionsTabLastOpened, UICollectionsTab.Music, false), Is.EqualTo(UICollectionsTab.Gallery));
            });
        }

        // OnDreamPowerStoreNavigatedAway 0x625F80 forwards to the store page, which clears its Purchasable flag.
        [Test]
        public void TheStoreNavigatingAwayClearsThePurchasableFlag()
        {
            Run(rig =>
            {
                rig.Store.Purchasable.Value = true;

                Call(rig.Collections, "OnDreamPowerStoreNavigatedAway"); // Reflection: private UIContainerCollections.OnDreamPowerStoreNavigatedAway

                Assert.That(rig.Store.Purchasable.Value, Is.False);
            });
        }

        // SetupDreamPowerStoreToggle 0x6258E0 sets the store tab's widget up with parameters whose label is empty and whose badge
        // count is zero (SetAlert(false, 0) hides the badge).
        [Test]
        public void TheStoreTabIsSetUpWithoutABadge()
        {
            Run(rig =>
            {
                Owned owned = rig.Scene.Owned;
                GameObject host = owned.Host("Lucid collections store toggle", false);
                var toggle = host.AddComponent<UIWidgetToggleCollections>();
                GameObject badgeHost = owned.Track(new GameObject("Lucid collections store badge", typeof(RectTransform)));
                badgeHost.SetActive(false);
                GameObject target = owned.Track(new GameObject("Lucid collections store badge target"));
                target.transform.SetParent(badgeHost.transform, false);
                target.SetActive(true);
                var badge = badgeHost.AddComponent<UIWidgetSDTAlertBadge>();
                Set(badge, "m_text", badgeHost.AddComponent<RTLTextMeshPro>()); // Reflection: serialized reference the editor assigns
                Set(badge, "m_enableObjectWhileActive", target); // Reflection: serialized reference the editor assigns
                Set(toggle, "m_alertBadge", badge); // Reflection: serialized reference the editor assigns
                Set(rig.Collections, "m_dreamPowerToggle", toggle); // Reflection: serialized reference the editor assigns

                Call(rig.Collections, "SetupDreamPowerStoreToggle");

                Assert.That(toggle.Label.Value, Is.Not.Null.And.Empty);
                Assert.That(target.activeSelf, Is.False);
            });
        }
    }
}
