using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using FeatureUnlockState = HardlightProject.SaveDataChallengeRewardTrack.FeatureUnlockState;

namespace ProjectLucid.Tests
{
    // UIContainerChallengeRewardTrack (Game.Runtime 0x0200085b, x86-64 0x60F970..0x611080, closures 0x0200085c..0x0200085f) as a real
    // component on an inactive owned host over the genuine ChallengeManager (registered, with the open save's challenge data and one
    // reward track state), the reward track definition and the open save's track record, two recording rewards of 20 and 60 XP, real
    // reward button prefabs (inactive hosts with a transition over a shared animation), a real slider widget and real content rectangles.
    // The process coroutine host is inactive, so every coroutine start is refused with a logged error, which the cases expect.
    // Start is not called: it takes a UINavigationManager, whose construction reads the navigation configuration asset, so the
    // service references that Start assigns are written the way it would assign them; only the navigation focus (the PopulateUI
    // closure that selects the focused button) needs that manager. Not covered: Start, the focus closure, the unlocking delay's navigation
    // of a ready button (<OnRewardClick>b__0), Action_SpendXP past its guards (it needs the XP reward container and the slider animation), the
    // ornament branch of GetRewardStatus, OnRewardHighlighted (it drives the scroll selection) and the shader offset of Action_OnScrollRectScroll.
    public sealed class UIContainerChallengeRewardTrackTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private const string FirstGuid = "lucid-track-reward-one";
        private const string SecondGuid = "lucid-track-reward-two";
        private const int Medium = -915112498; // UIWidgetButtonChallengeRewardSize.Medium
        private const int Large = 1371542769; // UIWidgetButtonChallengeRewardSize.Large

        private sealed class StubUnlock : IMetaGameUnlock
        {
            public string GetId() => "lucid-track-unlock";
            public PlayerProgressionTypes GetProgressionType() => default;
            public UIWidgetProgression GetWidget() => null;
            public Strings GetTitle() => default;
            public Strings GetBody() => default;
            public HLAudioClipIdentifier GetAudio() => default;
            public void Save(SaveManager saveManager) { }
            public void GetCustomTexture(Action<Texture> onLoaded) { }
            public void GetCustomSprite(Action<Sprite> onLoaded) { }
            public void ReleaseAssets() { }
            public bool CanExitToMenu() => false;
            public bool UsesAlternativeExitToMenuEvent() => false;
        }

        private sealed class Rig
        {
            public Owned Owned;
            public OwnedChallengeRewardServices Services;
            public ChallengeManager Manager;
            public UIContainerChallengeRewardTrack Container;
            public RewardTrackDefinition Definition;
            public SaveDataChallengeRewardTrack Track;
            public ChallengeRewardTrackState State;
            public UIChallengeTrackRewardRecordingReward First, Second;
            public StubUnlock Unlock;
            public ApplicationStateEvent CollectEvent;
            public Slider Slider;
            public UIWidgetChallengesSlider SliderWidget;
            public RectTransform Content, Rewards;
            public Animation Animation;
            public AnimationClip Collected, WithMask, Playable, Ready, Hidden;

            public List<UIWidgetButtonChallengeReward> Buttons => Get<List<UIWidgetButtonChallengeReward>>(Container, "m_rewardButtons");
        }

        private static AnimationClip NewClip(Owned owned, string name)
        {
            var clip = owned.Track(new AnimationClip { name = name, legacy = true });
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        private static UIChallengeTrackRewardRecordingReward NewTrackReward(Rig rig, string guid, int xp, int size)
        {
            var reward = NewReward<UIChallengeTrackRewardRecordingReward>(rig.Owned,
                "{\"m_guid\":\"" + guid + "\",\"m_xpThreshold\":" + xp + ",\"m_rewardSize\":" + size + "}");
            reward.Unlock = rig.Unlock;
            rig.Definition.Rewards.Add(reward);
            rig.Track.CreateRewardData(guid, guid + "-id");
            return reward;
        }

        // The services (and with them the reward base's references) come first; the manager is registered before the container is built.
        private static void Run(Action<Rig> body)
        {
            SettingsPageTestSupport.InitialiseAnalytics(); // the collector's type initialiser runs over owned rows here, not in whichever case reports first
            Owned.Run(owned =>
            {
                owned.LeaseRow<UINavigationManager>(); // UIContainerNavigation's field initialiser creates the row
                var rig = new Rig { Owned = owned, Unlock = new StubUnlock() };
                rig.Services = owned.Lease(new OwnedChallengeRewardServices());
                OwnedChallengeServices.Register(owned);
                rig.Manager = ProcessManager.GetSystem<ChallengeManager>();
                rig.Definition = Create<RewardTrackDefinition>(owned,
                    "{\"m_type\":" + (int)RewardTrackType.Adventure + ",\"m_rewardGlowColour\":{\"r\":0.25,\"g\":0.5,\"b\":0.75,\"a\":1}}");
                rig.Services.Data.RewardTrackDefinitions[RewardTrackType.Adventure] = rig.Definition;
                SaveDataGame save = rig.Services.Save.CurrentSave;
                rig.Track = save.GetOrCreateRewardTrackData(RewardTrackType.Adventure);
                rig.First = NewTrackReward(rig, FirstGuid, 20, Medium);
                rig.Second = NewTrackReward(rig, SecondGuid, 60, Medium);
                rig.State = new ChallengeRewardTrackState(rig.Definition);
                // Reflection: the manager takes the open save's challenge data in OnSaveGameOpen and fills its states from its data manager in a
                // callback; both need the App.
                Set(rig.Manager, "m_saveDataChallenges", save.SaveDataChallenges);
                Get<Dictionary<RewardTrackType, ChallengeRewardTrackState>>(rig.Manager, "m_rewardTrackStates")[RewardTrackType.Adventure] = rig.State;
                rig.CollectEvent = owned.Track(ScriptableObject.CreateInstance<ApplicationStateEvent>());

                rig.Slider = owned.Host("Lucid reward track slider", false).AddComponent<Slider>();
                GameObject sliderHost = owned.Host("Lucid reward track slider widget", false);
                sliderHost.AddComponent<RectTransform>();
                rig.SliderWidget = sliderHost.AddComponent<UIWidgetChallengesSlider>();
                Set(rig.SliderWidget, "m_slider", rig.Slider); // Reflection: serialized reference
                rig.Content = owned.Host("Lucid reward track content", false).AddComponent<RectTransform>();
                rig.Rewards = owned.Host("Lucid reward track rewards", false).AddComponent<RectTransform>();
                rig.Animation = owned.Host("Lucid reward track animation", true).AddComponent<Animation>();
                rig.Collected = NewClip(owned, "Lucid track reward collected");
                rig.WithMask = NewClip(owned, "Lucid track reward collected with mask");
                rig.Playable = NewClip(owned, "Lucid track reward collected playable");
                rig.Ready = NewClip(owned, "Lucid track reward ready");
                rig.Hidden = NewClip(owned, "Lucid track reward hidden");
                foreach (AnimationClip clip in new[] { rig.Collected, rig.WithMask, rig.Playable, rig.Ready, rig.Hidden })
                    rig.Animation.AddClip(clip, clip.name);

                rig.Container = owned.Host("Lucid reward track container", false).AddComponent<UIContainerChallengeRewardTrack>();
                // Reflection: serialized references and numbers of the authored container
                Set(rig.Container, "m_challengesSlider", rig.SliderWidget);
                Set(rig.Container, "m_scrollRectContentTransform", rig.Content);
                Set(rig.Container, "m_rewardsContentTransform", rig.Rewards); // Reflection: serialized m_rewardsContentTransform the editor assigns
                Set(rig.Container, "m_containerNavigation", owned.Host("Lucid reward track navigation", false).AddComponent<UIContainerNavigation>()); // Reflection: serialized m_containerNavigation the editor assigns
                Set(rig.Container, "m_openCollectRewardEvent", rig.CollectEvent); // Reflection: serialized m_openCollectRewardEvent the editor assigns
                Set(rig.Container, "m_spaceBetweenRewards", 100); // Reflection: serialized m_spaceBetweenRewards the editor assigns
                Set(rig.Container, "m_sliderPadding", 5f); // Reflection: serialized m_sliderPadding the editor assigns
                Get<SerializableDictionary<UIWidgetButtonChallengeRewardSize, UIWidgetButtonChallengeReward>>(rig.Container, "m_rewardBySizePrefabs")
                    [UIWidgetButtonChallengeRewardSize.Medium] = NewButtonPrefab(owned, rig, "Lucid reward track medium button");
                // Reflection: the references Start assigns (see the type comment).
                Set(rig.Container, "m_app", ProcessManager.GetSystem<App>());
                Set(rig.Container, "m_dataManager", rig.Services.Data);
                Set(rig.Container, "m_challengeManager", rig.Manager); // Reflection: Start takes this from the registry; Edit Mode does not send Start
                Set(rig.Container, "m_saveManager", rig.Services.Save); // Reflection: Start takes this from the registry; Edit Mode does not send Start
                Set(rig.Container, "m_rewardTrackDefinition", rig.Definition); // Reflection: Start takes this from the data manager; Edit Mode does not send Start
                body(rig);
            });
        }

        private static UIWidgetButtonChallengeReward NewButtonPrefab(Owned owned, Rig rig, string name)
        {
            GameObject host = owned.Host(name, false);
            var widget = host.AddComponent<UIWidgetButtonChallengeReward>();
            var transition = host.AddComponent<UITransitionAnimation>();
            Set(transition, "m_animation", rig.Animation); // Reflection: serialized references
            Set(transition, "m_settings", new List<UITransitionAnimationSetting>());
            Set(widget, "m_transition", transition);
            Set(widget, "m_collectedAnimationClip", rig.Collected); // Reflection: serialized m_collectedAnimationClip the editor assigns
            Set(widget, "m_collectedAnimationClipWithMask", rig.WithMask); // Reflection: serialized m_collectedAnimationClipWithMask the editor assigns
            Set(widget, "m_collectedAnimationClipPlayable", rig.Playable); // Reflection: serialized m_collectedAnimationClipPlayable the editor assigns
            Set(widget, "m_readyToCollectAnimationClip", rig.Ready); // Reflection: serialized m_readyToCollectAnimationClip the editor assigns
            Set(widget, "m_hiddenAnimationClip", rig.Hidden); // Reflection: serialized m_hiddenAnimationClip the editor assigns
            return widget;
        }

        // Spends and earns the given XP on the open save, sets the collected flags, and lets the track state read them (RefreshRewards owns a
        // reward once its threshold is spent, as the manager's check does).
        private static void Configure(Rig rig, int spent, int earned, bool firstCollected = false, bool secondCollected = false)
        {
            rig.Track.SpentXP = spent;
            rig.Track.UnlockState = FeatureUnlockState.Seen;
            rig.Track.GetRewardDataByTrackGUIDUnsafe(FirstGuid).Collected = firstCollected;
            rig.Track.GetRewardDataByTrackGUIDUnsafe(SecondGuid).Collected = secondCollected;
            rig.Services.Save.CurrentSave.SaveDataChallenges.TotalXPEarned = earned;
            rig.State.OnSaveGameOpen(rig.Track);
            rig.State.RefreshRewards((reward, spentXP) => reward.XPThreshold <= spentXP, reward => { });
        }

        // PopulateUI starts the focus wait on the refused coroutine host whenever a button is chosen for the focus.
        private static void Populate(Rig rig, bool focusChosen = true)
        {
            if (focusChosen) LogAssert.Expect(LogType.Error, RefusedCoroutine);
            Call(rig.Container, "PopulateUI"); // Reflection: private UIContainerChallengeRewardTrack.PopulateUI
        }

        private static void PrepareStorage(out List<IMetaGameUnlock> unlocks, out List<ApplicationStateEvent> events)
        {
            IGraphStorage storage = ProcessManager.GetSystem<App>().Storage;
            unlocks = new List<IMetaGameUnlock>();
            events = new List<ApplicationStateEvent>();
            storage.SetValue(AppFSMKeys.MetaGameUnlocks, unlocks);
            storage.SetValue(AppFSMKeys.StateEvents, events);
        }

        // dump.cs TypeDefIndex 2142 and the DummyDll rows (fields 0x0400201b..0x0400203b, methods 0x06003071..0x0600307e).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            var serialized = new[]
            {
                "m_rewardTrackType", "m_scrollRectSelection", "m_scrollRectContentTransform", "m_rewardsContentTransform", "m_challengesSlider",
                "m_rewardBySizePrefabs", "m_openCollectRewardEvent", "m_movingTrackRenderers", "m_containerNavigation", "m_containerXPCounter",
                "m_sliderPadding", "m_sliderShaderOffsetMultiplier", "m_spaceBetweenRewards", "m_unlockingCoroutineDelay", "m_xpTotalCounter",
            };
            AssertMembers(typeof(UIContainerChallengeRewardTrack), 0x00100101,
                serialized.Select(name => (name, 0x0001))
                    .Concat(new[] { "ShowAddButton", "ShowCollectButton", "EnableButtons", "ShowMoreXPNeeded", "ShowAllCollected", "XPIncrement" }
                        .Select(name => (name, 0x0026)))
                    .Concat(new (string, int)[]
                    {
                        ("m_app", 0x0001), ("m_dataManager", 0x0001), ("m_challengeManager", 0x0001), ("m_saveManager", 0x0001),
                        ("m_ornamentManager", 0x0001), ("m_rewardTrackDefinition", 0x0001), ("m_sliderShaderModifier", 0x0001),
                        ("m_uiNavigationManager", 0x0001), ("m_rewardButtons", 0x0021), ("m_sliderIsAnimating", 0x0001),
                        ("m_unlockingCoroutine", 0x0001), ("m_isUnlocking", 0x0001),
                    }).ToArray(),
                new (string, int)[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("PopulateUI", 0x0081), ("RefreshBinders", 0x0081), ("OnRewardClick", 0x0081),
                    ("Action_CollectReward", 0x0086), ("TryCollectReward", 0x0081), ("OnRewardHighlighted", 0x0081), ("OnRewardHighlighted", 0x0081),
                    ("GetRewardStatus", 0x0081), ("Action_OnScrollRectScroll", 0x0086), ("Action_SpendXP", 0x0086), ("GetSliderValue", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerChallengeRewardTrack), 2, 1);
            Assert.That(typeof(UIContainerChallengeRewardTrack).BaseType, Is.EqualTo(typeof(UIContainer)));
            foreach ((string name, string tooltip) in new[]
            {
                ("m_movingTrackRenderers", "Renderers that contains a custom shader needed to be modified when the track moves."),
                ("m_sliderPadding", "When adjusting the size of the slider, this will create extra padding on the sides so the slider doesn't end flushed."),
                ("m_sliderShaderOffsetMultiplier", "Multiplies the offset in the slider shader. Thus, if set to 1, it will be 1-to-1 with the scroll values, other values will increase or decrease the offset."),
                ("m_spaceBetweenRewards", "The space between each reward."),
            })
                Assert.That(Tooltip(typeof(UIContainerChallengeRewardTrack).GetField(name, Declared)), Is.EqualTo(tooltip), name);
            ParameterInfo skip = typeof(UIContainerChallengeRewardTrack).GetMethod("OnRewardClick", Declared).GetParameters()[1];
            Assert.That(skip.HasDefaultValue && Equals(skip.DefaultValue, false), Is.True, "OnRewardClick skipRefreshBinders");
            skip = typeof(UIContainerChallengeRewardTrack).GetMethod("TryCollectReward", Declared).GetParameters()[0];
            Assert.That(skip.HasDefaultValue && Equals(skip.DefaultValue, false), Is.True, "TryCollectReward skipRefreshBinders");
        }

        // The four closure classes (one per scope of PopulateUI, OnRewardClick and Action_SpendXP).
        [Test]
        public void TheClosureClassesMatchTheOriginalMetadata()
        {
            foreach ((string name, (string, int)[] fields, string method) in new[]
            {
                ("<>c__DisplayClass35_0", new (string, int)[] { ("<>4__this", 0x0006), ("focusReward", 0x0006) }, "<PopulateUI>b__0"),
                ("<>c__DisplayClass35_1", new (string, int)[] { ("challengeButton", 0x0006), ("CS$<>8__locals1", 0x0006) }, "<PopulateUI>b__1"),
                ("<>c__DisplayClass37_0", new (string, int)[] { ("<>4__this", 0x0006), ("skipRefreshBinders", 0x0006) }, "<OnRewardClick>b__0"),
                ("<>c__DisplayClass44_0", new (string, int)[] { ("trackState", 0x0006), ("<>4__this", 0x0006) }, "<Action_SpendXP>b__0"),
            })
            {
                Type closure = typeof(UIContainerChallengeRewardTrack).GetNestedType(name, BindingFlags.NonPublic);
                Assert.That(closure, Is.Not.Null, name);
                AssertMembers(closure, 0x00100103, fields, new (string, int)[] { (".ctor", 0x1886), (method, 0x0083) });
            }
        }

        // The constructor 0x611080 stores the Adventure track type (0x25b86f1f) at +0x38, the size dictionary over the size comparer, the
        // slider shader multiplier 10 (0x41200000) and the unlocking delay 0.5 (0x3f000000), creates the six bindables and the button list.
        [Test]
        public void ConstructionSetsTheDefaultsAndCreatesTheBindablesAndTheList()
        {
            Run(rig =>
            {
                var fresh = rig.Owned.Host("Lucid reward track fresh container", false).AddComponent<UIContainerChallengeRewardTrack>();
                Assert.That((int)Get<RewardTrackType>(fresh, "m_rewardTrackType"), Is.EqualTo(0x25b86f1f));
                Assert.That(Get<RewardTrackType>(fresh, "m_rewardTrackType"), Is.EqualTo(RewardTrackType.Adventure));
                Assert.That(Get<float>(fresh, "m_sliderShaderOffsetMultiplier"), Is.EqualTo(10f));
                Assert.That(Get<float>(fresh, "m_unlockingCoroutineDelay"), Is.EqualTo(0.5f));
                Assert.That(Get<float>(fresh, "m_sliderPadding"), Is.Zero);
                Assert.That(Get<int>(fresh, "m_spaceBetweenRewards"), Is.Zero);
                // Reflection: the dictionary keeps its comparer in a private inner dictionary.
                var sizes = Get<SerializableDictionary<UIWidgetButtonChallengeRewardSize, UIWidgetButtonChallengeReward>>(fresh, "m_rewardBySizePrefabs");
                Assert.That(Get<Dictionary<UIWidgetButtonChallengeRewardSize, UIWidgetButtonChallengeReward>>(sizes, "m_dictionary").Comparer,
                    Is.SameAs(HardlightProject.HardlightEnumComparers.UIWidgetButtonChallengeRewardSizeComparer));
                var bindables = new object[] { fresh.ShowAddButton, fresh.ShowCollectButton, fresh.EnableButtons, fresh.ShowMoreXPNeeded, fresh.ShowAllCollected, fresh.XPIncrement };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(6));
                Assert.That(Get<List<UIWidgetButtonChallengeReward>>(fresh, "m_rewardButtons"), Is.Empty);
            });
        }

        // GetRewardStatus 0x610250: a reward is unlocked once the track's spent XP reaches its threshold (setge at 0x6103DC: 19 of 20 is not,
        // 20 is), collected as its save record says, and the status carries the reward number and the definition's track type: Shadow is
        // the container's own type here, while the definition (and so the status) is Adventure.
        [TestCase(19, false, TestName = "OneXpShortOfTheThresholdIsLocked")]
        [TestCase(20, true, TestName = "ExactlyTheThresholdIsUnlocked")]
        [TestCase(21, true, TestName = "PastTheThresholdIsUnlocked")]
        public void ARewardIsUnlockedWhenTheSpentXpReachesItsThreshold(int spent, bool unlocked)
        {
            Run(rig =>
            {
                Configure(rig, spent, 100);
                Set(rig.Container, "m_rewardTrackType", RewardTrackType.Shadow); // Reflection: serialized field (the definition decides the track)

                var status = (UIChallengeTrackRewardStatus)Call(rig.Container, "GetRewardStatus", rig.First, 3);

                Assert.That(status.Reward, Is.SameAs(rig.First));
                Assert.That(status.Unlocked, Is.EqualTo(unlocked));
                Assert.That(status.Collected, Is.False);
                status.FireAnalyticsEvents();
                Assert.That(rig.First.AnalyticsEvents, Is.EqualTo(new[] { (RewardTrackType.Adventure, 3) }));
            });
        }

        [Test]
        public void ACollectedRewardIsTakenFromItsSaveRecord()
        {
            Run(rig =>
            {
                Configure(rig, 100, 100, firstCollected: true, secondCollected: false);

                var first = (UIChallengeTrackRewardStatus)Call(rig.Container, "GetRewardStatus", rig.First, 1); // Reflection: private UIContainerChallengeRewardTrack.GetRewardStatus
                var second = (UIChallengeTrackRewardStatus)Call(rig.Container, "GetRewardStatus", rig.Second, 2); // Reflection: private UIContainerChallengeRewardTrack.GetRewardStatus

                Assert.That(first.Collected, Is.True);
                Assert.That(second.Collected, Is.False);
                Assert.That(first.Unlocked && second.Unlocked, Is.True);
            });
        }

        // GetSliderValue 0x610400: (index + 1) * space + segment * (spent - highestXP) / max(nextXP - highestXP, 1), with the segment
        // (index - index + 1) * space = 100 here. The reward thresholds are 20 and 60; the highest reward is the last one the track owns.
        // 0x610517 subtracts the highest threshold from the next one and 0x61051E takes the maximum with 1.0 (0x29A6BE0): once both rewards are owned
        // the difference is 0 and the maximum keeps the division finite, which a version without it would not.
        [TestCase(0, 0f, TestName = "NothingSpentSitsAtTheStart")]
        [TestCase(10, 50f, TestName = "HalfWayToTheFirstReward")]
        [TestCase(20, 100f, TestName = "AtTheFirstRewardTheSliderSitsOnIt")]
        [TestCase(30, 125f, TestName = "AQuarterOfTheWayToTheSecondReward")]
        [TestCase(40, 150f, TestName = "HalfWayToTheSecondReward")]
        [TestCase(60, 200f, TestName = "AtTheLastRewardTheDivisionStaysFinite")]
        [TestCase(100, 4200f, TestName = "PastTheLastRewardTheSliderRunsOnByTheExcess")]
        public void TheSliderSitsAtTheLastRewardReachedPlusTheShareTowardsTheNext(int spent, float expected)
        {
            Run(rig =>
            {
                Configure(rig, spent, spent);

                Assert.That((float)Call(rig.Container, "GetSliderValue"), Is.EqualTo(expected)); // Reflection: private UIContainerChallengeRewardTrack.GetSliderValue
            });
        }

        // RefreshBinders 0x610570, track with nothing spent and nothing earned: the next reward needs 20 XP (XPToNextReward), the buttons are
        // enabled and the XP text published; nothing can be collected or added, so only "more XP needed" shows.
        [Test]
        public void WithoutExperienceOnlyMoreXpNeededShows()
        {
            Run(rig =>
            {
                Configure(rig, 0, 0);

                Call(rig.Container, "RefreshBinders"); // Reflection: private UIContainerChallengeRewardTrack.RefreshBinders

                Assert.That(rig.Container.XPIncrement.Value, Is.EqualTo("20"));
                Assert.That(rig.Container.EnableButtons.Value, Is.True);
                Assert.That(rig.Container.ShowCollectButton.Value, Is.False);
                Assert.That(rig.Container.ShowAddButton.Value, Is.False);
                Assert.That(rig.Container.ShowAllCollected.Value, Is.False);
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.True);
            });
        }

        // Enough XP for the next reward (50 earned, 20 needed) offers the add button instead.
        [Test]
        public void EnoughExperienceOffersTheAddButton()
        {
            Run(rig =>
            {
                Configure(rig, 0, 50);

                Call(rig.Container, "RefreshBinders"); // Reflection: private UIContainerChallengeRewardTrack.RefreshBinders

                Assert.That(rig.Container.ShowAddButton.Value, Is.True);
                Assert.That(rig.Container.ShowCollectButton.Value, Is.False);
                Assert.That(rig.Container.ShowAllCollected.Value, Is.False);
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.False);
                Assert.That(rig.Container.EnableButtons.Value, Is.True);
            });
        }

        // An uncollected reward (20 XP spent, the first reward owned and not collected) shows the collect button and no add button; the next
        // reward is 40 XP away.
        [Test]
        public void AnUncollectedRewardShowsTheCollectButton()
        {
            Run(rig =>
            {
                Configure(rig, 20, 20);

                Call(rig.Container, "RefreshBinders"); // Reflection: private UIContainerChallengeRewardTrack.RefreshBinders

                Assert.That(rig.Container.XPIncrement.Value, Is.EqualTo("40"));
                Assert.That(rig.Container.ShowCollectButton.Value, Is.True);
                Assert.That(rig.Container.ShowAddButton.Value, Is.False);
                Assert.That(rig.Container.ShowAllCollected.Value, Is.False);
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.False);
            });
        }

        // Every reward collected: nothing is 0 XP away (XPToNextReward is 0), so the "all collected" state shows alone.
        [Test]
        public void WhenEveryRewardIsCollectedOnlyAllCollectedShows()
        {
            Run(rig =>
            {
                Configure(rig, 60, 60, firstCollected: true, secondCollected: true);

                Call(rig.Container, "RefreshBinders"); // Reflection: private UIContainerChallengeRewardTrack.RefreshBinders

                Assert.That(rig.Container.XPIncrement.Value, Is.EqualTo("0"));
                Assert.That(rig.Container.ShowAllCollected.Value, Is.True);
                Assert.That(rig.Container.ShowCollectButton.Value, Is.False);
                Assert.That(rig.Container.ShowAddButton.Value, Is.False);
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.False);
            });
        }

        // While the slider animates (0x610634) the buttons are disabled and the XP text is left alone; a collectable reward then hides the
        // collect button (it needs the slider at rest, 0x6106ED) and the add button follows only the unlocking flag.
        [Test]
        public void WhileTheSliderAnimatesTheButtonsAreDisabledAndTheCollectButtonIsHidden()
        {
            Run(rig =>
            {
                Configure(rig, 20, 20);
                Set(rig.Container, "m_sliderIsAnimating", true); // Reflection: set by Action_SpendXP's slider animation, which cannot run here

                Call(rig.Container, "RefreshBinders");

                Assert.That(rig.Container.EnableButtons.Value, Is.False);
                Assert.That(rig.Container.XPIncrement.Value, Is.Null);
                Assert.That(rig.Container.ShowCollectButton.Value, Is.False);
                Assert.That(rig.Container.ShowAddButton.Value, Is.True);
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.False);
            });
        }

        // While a reward unlocks (0x61063D) the buttons are disabled; without a collectable reward the collect button shows because the
        // track is unlocking and the slider is at rest (0x6106E4), and the add button is hidden for lack of XP.
        [Test]
        public void WhileARewardUnlocksTheCollectButtonShowsEvenWithoutACollectableReward()
        {
            Run(rig =>
            {
                Configure(rig, 0, 0);
                Set(rig.Container, "m_isUnlocking", true); // Reflection: set by OnRewardClick, which is covered separately

                Call(rig.Container, "RefreshBinders");

                Assert.That(rig.Container.EnableButtons.Value, Is.False);
                Assert.That(rig.Container.ShowCollectButton.Value, Is.True);
                Assert.That(rig.Container.ShowAddButton.Value, Is.False);
                Assert.That(rig.Container.ShowAllCollected.Value, Is.False);
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.False);
            });
        }

        // Unlocking with XP that can be spent (RefreshBinders 0x610570): the collect button shows (0x6106E4, 0x6106ED) while the add button stays
        // hidden because a reward is unlocking (the sete on m_isUnlocking at 0x610725). The first reward is 20 XP away.
        [Test]
        public void WhileARewardUnlocksWithSpendableXpTheAddButtonStaysHidden()
        {
            Run(rig =>
            {
                Configure(rig, 0, 50);
                Set(rig.Container, "m_isUnlocking", true); // Reflection: set by OnRewardClick, which is covered separately

                Call(rig.Container, "RefreshBinders");

                Assert.That(rig.Container.ShowAddButton.Value, Is.False);
                Assert.That(rig.Container.ShowCollectButton.Value, Is.True);
                Assert.That(rig.Container.ShowAllCollected.Value, Is.False, "20 XP to the next reward");
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.False);
                Assert.That(rig.Container.EnableButtons.Value, Is.False);
                Assert.That(rig.Container.XPIncrement.Value, Is.Null);
            });
        }

        // Every reward collected while the slider animates: XPToNextReward is 0 and nothing can be spent, but the animating slider still
        // offers the add button (0x61071F is not taken, then !m_isUnlocking at 0x610725), which in turn keeps "all collected" off (0x610806).
        [Test]
        public void WhileTheSliderAnimatesAfterTheLastRewardTheAddButtonShowsAndAllCollectedDoesNot()
        {
            Run(rig =>
            {
                Configure(rig, 60, 60, firstCollected: true, secondCollected: true);
                Set(rig.Container, "m_sliderIsAnimating", true); // Reflection: set by Action_SpendXP's slider animation, which cannot run here

                Call(rig.Container, "RefreshBinders");

                Assert.That(rig.Container.ShowCollectButton.Value, Is.False);
                Assert.That(rig.Container.ShowAddButton.Value, Is.True);
                Assert.That(rig.Container.ShowAllCollected.Value, Is.False);
                Assert.That(rig.Container.ShowMoreXPNeeded.Value, Is.False);
            });
        }

        // The unlocking delay's closure <OnRewardClick>b__0 (0x611390) with no ready button ends the unlocking (0x6114BD) and refreshes the
        // binders only when the captured skipRefreshBinders is false (0x6114C4).
        [TestCase(false, true, TestName = "TheUnlockingDelayEndsTheUnlockingAndRefreshesTheBinders")]
        [TestCase(true, false, TestName = "TheUnlockingDelayEndsTheUnlockingAndLeavesTheBindersWhenSkipped")]
        public void TheUnlockingDelayEndsTheUnlocking(bool skipRefreshBinders, bool bindersRefreshed)
        {
            Run(rig =>
            {
                Configure(rig, 0, 50);
                Set(rig.Container, "m_isUnlocking", true); // Reflection: set by OnRewardClick
                rig.Container.EnableButtons.Value = false;
                Type closureType = typeof(UIContainerChallengeRewardTrack).GetNestedType("<>c__DisplayClass37_0", BindingFlags.NonPublic);
                object closure = Activator.CreateInstance(closureType);
                Set(closure, "<>4__this", rig.Container); // Reflection: the closure's captured state
                Set(closure, "skipRefreshBinders", skipRefreshBinders);

                Call(closure, "<OnRewardClick>b__0"); // Reflection: compiler-generated lambda body, reachable only through reflection

                Assert.That(Get<bool>(rig.Container, "m_isUnlocking"), Is.False);
                Assert.That(rig.Container.EnableButtons.Value, Is.EqualTo(bindersRefreshed));
                Assert.That(rig.Container.ShowAddButton.Value, Is.EqualTo(bindersRefreshed), "50 XP can be spent on the 20 XP reward");
            });
        }

        // PopulateUI 0x60FBA0: one button per reward from the prefab of its size, placed along the track: the slider's position plus
        // 100 per reward number (the clone's own width and, for every button but the last, 10 are folded in by its Setup). The track is 200
        // wide (100 per reward) plus the 5 padding; the slider is set up with the slider value and 200 for both its maximum and its size.
        [Test]
        public void EachRewardGetsAButtonPlacedAlongTheTrack()
        {
            Run(rig =>
            {
                Configure(rig, 0, 0);
                ((RectTransform)rig.SliderWidget.transform).anchoredPosition = new Vector2(7f, 3f);

                Populate(rig);

                List<UIWidgetButtonChallengeReward> buttons = rig.Buttons;
                Assert.That(buttons.Count, Is.EqualTo(2));
                Assert.That(buttons.Select(button => button.RewardStatus.Reward).ToArray(), Is.EqualTo(new ChallengeReward[] { rig.First, rig.Second }));
                Assert.That(buttons.Select(button => button.RewardXP.Value).ToArray(), Is.EqualTo(new[] { "20", "60" }));
                Assert.That(buttons.All(button => button.transform.parent == rig.Rewards), "the buttons are spawned into the rewards content");
                var parameters = buttons.Select(button => Get<UIWidgetButtonChallengesRewardParameters>(button, "m_parameters")).ToArray();
                Assert.That(parameters.Select(p => p.HorizontalPosition).ToArray(), Is.EqualTo(new[] { 107f, 207f }));
                Assert.That(parameters.Select(p => p.IsNextReward).ToArray(), Is.EqualTo(new[] { true, false }), "the next reward is the first");
                Assert.That(parameters.Select(p => p.IsLast).ToArray(), Is.EqualTo(new[] { false, true }));
                Assert.That(parameters.All(p => p.TrackType == RewardTrackType.Adventure), Is.True);
                Assert.That(((RectTransform)buttons[0].transform).anchoredPosition.x, Is.EqualTo(67f), "-50 (half the 100 width) + 107 + 10");
                Assert.That(((RectTransform)buttons[1].transform).anchoredPosition.x, Is.EqualTo(157f), "-50 + 207, the last button gets no offset");
                Assert.That(rig.Content.sizeDelta.x, Is.EqualTo(205f));
                Assert.That(rig.Slider.maxValue, Is.EqualTo(200f));
                Assert.That(rig.Slider.value, Is.Zero);
                Assert.That(((RectTransform)rig.Slider.transform).sizeDelta.x, Is.EqualTo(200f));
                Assert.That(rig.Container.EnableButtons.Value, Is.True, "RefreshBinders ran last");
                Assert.That(rig.Container.XPIncrement.Value, Is.EqualTo("20"));
            });
        }

        // A reward whose size has no prefab gets no button (the TryGetValue at 0x60FEB8 jumps to the next reward), while the rewards
        // after it still count their own numbers and the last reward is the last one of the track, whether or not it has a button.
        [Test]
        public void ARewardWithoutAPrefabOfItsSizeGetsNoButton()
        {
            Run(rig =>
            {
                UIChallengeTrackRewardRecordingReward large = NewTrackReward(rig, "lucid-track-reward-large", 80, Large);
                UIChallengeTrackRewardRecordingReward third = NewTrackReward(rig, "lucid-track-reward-three", 100, Medium);
                Configure(rig, 0, 0);

                Populate(rig);

                List<UIWidgetButtonChallengeReward> buttons = rig.Buttons;
                Assert.That(buttons.Select(button => button.RewardStatus.Reward).ToArray(), Is.EqualTo(new ChallengeReward[] { rig.First, rig.Second, third }));
                var parameters = buttons.Select(button => Get<UIWidgetButtonChallengesRewardParameters>(button, "m_parameters")).ToArray();
                Assert.That(parameters.Select(p => p.HorizontalPosition).ToArray(), Is.EqualTo(new[] { 100f, 200f, 400f }), "the skipped reward still holds number 3");
                Assert.That(parameters.Select(p => p.IsLast).ToArray(), Is.EqualTo(new[] { false, false, true }));
                Assert.That(large.Calls, Is.Empty, "no button, so no image load");
                Assert.That(rig.Content.sizeDelta.x, Is.EqualTo(405f), "four rewards of 100 plus the padding");
            });
        }

        // Without any prefab no button is made and no focus is chosen, so no wait for the UI starts (an unexpected logged error would fail).
        [Test]
        public void WithoutPrefabsNoButtonAndNoFocusWaitAreMade()
        {
            Run(rig =>
            {
                Configure(rig, 0, 0);
                Get<SerializableDictionary<UIWidgetButtonChallengeRewardSize, UIWidgetButtonChallengeReward>>(rig.Container, "m_rewardBySizePrefabs").Clear();

                Populate(rig, focusChosen: false);

                Assert.That(rig.Buttons, Is.Empty);
                Assert.That(rig.Rewards.childCount, Is.Zero);
            });
        }

        // A button's click is wired to OnRewardClick without skipping the binders (the closure 0x611350 passes edx = 0): clicking a button whose
        // reward is ready saves the reward, reports it (reward number 1 of the Adventure track), plays its collect animation, queues its meta-game
        // unlock and the collect event, starts the unlocking state with the buttons disabled, and starts the unlocking delay on the coroutine host.
        [Test]
        public void ClickingAReadyRewardCollectsIt()
        {
            Run(rig =>
            {
                Configure(rig, 20, 20);
                Populate(rig);
                PrepareStorage(out List<IMetaGameUnlock> unlocks, out List<ApplicationStateEvent> events);
                UIWidgetButtonChallengeReward button = rig.Buttons[0];
                LogAssert.Expect(LogType.Error, RefusedCoroutine); // the unlocking delay

                button.Action_OnButtonClick();

                Assert.That(rig.First.Calls, Does.Contain("Save"));
                Assert.That(rig.First.AnalyticsEvents, Is.EqualTo(new[] { (RewardTrackType.Adventure, 1) }));
                Assert.That(rig.First.UnlockRequests, Is.EqualTo(new[] { RewardTrackType.Adventure }));
                Assert.That(button.RewardStatus.Collected, Is.True);
                Assert.That(rig.Animation.IsPlaying(rig.Collected.name), Is.True, "the collect animation was queued");
                Assert.That(unlocks, Is.EqualTo(new IMetaGameUnlock[] { rig.Unlock }));
                Assert.That(events, Is.EqualTo(new[] { rig.CollectEvent }));
                Assert.That(Get<bool>(rig.Container, "m_isUnlocking"), Is.True);
                Assert.That(rig.Container.EnableButtons.Value, Is.False, "RefreshBinders ran while unlocking");
                Assert.That(Get<Coroutine>(rig.Container, "m_unlockingCoroutine"), Is.Null, "the refused coroutine");
            });
        }

        // The skip flag keeps RefreshBinders from running (0x610B80), and the collect event is added only once to the list.
        [Test]
        public void ClickingWithTheBindersSkippedLeavesThemAlone()
        {
            Run(rig =>
            {
                Configure(rig, 20, 20);
                Populate(rig);
                PrepareStorage(out List<IMetaGameUnlock> unlocks, out List<ApplicationStateEvent> events);
                events.Add(rig.CollectEvent);
                rig.Container.EnableButtons.Value = true;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Call(rig.Container, "OnRewardClick", rig.Buttons[0], true); // Reflection: private UIContainerChallengeRewardTrack.OnRewardClick

                Assert.That(rig.Container.EnableButtons.Value, Is.True, "untouched");
                Assert.That(events, Is.EqualTo(new[] { rig.CollectEvent }), "AddUnique");
                Assert.That(unlocks.Count, Is.EqualTo(1));
                Assert.That(Get<bool>(rig.Container, "m_isUnlocking"), Is.True);
            });
        }

        // Nothing happens while the slider animates or a reward is unlocking (0x6108EA, 0x6108F3).
        [TestCase("m_sliderIsAnimating", TestName = "ClickingWhileTheSliderAnimatesDoesNothing")]
        [TestCase("m_isUnlocking", TestName = "ClickingWhileARewardUnlocksDoesNothing")]
        public void ClickingWhileBusyDoesNothing(string busyFlag)
        {
            Run(rig =>
            {
                Configure(rig, 20, 20);
                Populate(rig);
                PrepareStorage(out List<IMetaGameUnlock> unlocks, out List<ApplicationStateEvent> events);
                Set(rig.Container, busyFlag, true); // Reflection: set by Action_SpendXP's animation and by OnRewardClick

                rig.Buttons[0].Action_OnButtonClick();

                Assert.That(rig.First.Calls, Does.Not.Contain("Save"));
                Assert.That(unlocks, Is.Empty);
                Assert.That(events, Is.Empty);
            });
        }

        // TryCollectReward 0x610C00 clicks the first button that is ready to collect, with the skip flag it was given, and answers true; with
        // none ready it answers false and clicks nothing. Action_CollectReward (0x610BF0) is it with the binders not skipped.
        [Test]
        public void TheFirstReadyRewardIsCollectedAndNoneReadyAnswersFalse()
        {
            Run(rig =>
            {
                Configure(rig, 60, 60); // both rewards owned and uncollected
                Populate(rig);
                PrepareStorage(out List<IMetaGameUnlock> unlocks, out List<ApplicationStateEvent> events);
                rig.Container.EnableButtons.Value = true;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                bool collected = (bool)Call(rig.Container, "TryCollectReward", true); // Reflection: private UIContainerChallengeRewardTrack.TryCollectReward

                Assert.That(collected, Is.True);
                Assert.That(rig.First.Calls, Does.Contain("Save"));
                Assert.That(rig.Second.Calls, Does.Not.Contain("Save"), "only the first ready reward is clicked");
                Assert.That(rig.Container.EnableButtons.Value, Is.True, "the skip flag was passed on");
                Assert.That(unlocks.Count, Is.EqualTo(1));
            });
        }

        [Test]
        public void WithNoReadyRewardNothingIsCollected()
        {
            Run(rig =>
            {
                Configure(rig, 0, 0);
                Populate(rig);
                PrepareStorage(out List<IMetaGameUnlock> unlocks, out List<ApplicationStateEvent> events);

                Assert.That((bool)Call(rig.Container, "TryCollectReward", false), Is.False); // Reflection: private UIContainerChallengeRewardTrack.TryCollectReward
                rig.Container.Action_CollectReward();

                Assert.That(rig.First.Calls, Does.Not.Contain("Save"));
                Assert.That(unlocks, Is.Empty);
                Assert.That(Get<bool>(rig.Container, "m_isUnlocking"), Is.False);
            });
        }

        [Test]
        public void ActionCollectRewardCollectsTheFirstReadyRewardAndRefreshesTheBinders()
        {
            Run(rig =>
            {
                Configure(rig, 20, 20);
                Populate(rig);
                PrepareStorage(out _, out _);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                rig.Container.Action_CollectReward();

                Assert.That(rig.First.Calls, Does.Contain("Save"));
                Assert.That(rig.Container.EnableButtons.Value, Is.False, "the binders were refreshed while unlocking");
            });
        }

        // Action_SpendXP 0x610DF0 returns at once while a reward unlocks (0x610E79) or the slider animates (0x610E86), and when the XP
        // cannot be spent (CanSpendXP at 0x610F4D: 20 needed, 0 available): the track's spent XP is untouched.
        [Test]
        public void SpendingDoesNothingWhileBusyOrWithoutEnoughExperience()
        {
            Run(rig =>
            {
                Configure(rig, 0, 50);
                Set(rig.Container, "m_isUnlocking", true); // Reflection: set by OnRewardClick
                rig.Container.Action_SpendXP();
                Set(rig.Container, "m_isUnlocking", false);
                Set(rig.Container, "m_sliderIsAnimating", true); // Reflection: set by the spend itself
                rig.Container.Action_SpendXP();
                Set(rig.Container, "m_sliderIsAnimating", false);
                Assert.That(rig.Track.SpentXP, Is.Zero, "busy");

                Configure(rig, 0, 0);
                rig.Container.Action_SpendXP();

                Assert.That(rig.Track.SpentXP, Is.Zero, "not enough experience");
                Assert.That(Get<bool>(rig.Container, "m_sliderIsAnimating"), Is.False);
            });
        }

        // Action_OnScrollRectScroll 0x610DC0 returns when there is no shader modifier (the test at 0x610DCE).
        [Test]
        public void ScrollingWithoutAShaderModifierDoesNothing()
        {
            Run(rig => Assert.That(() => rig.Container.Action_OnScrollRectScroll(new Vector2(0.5f, 0f)), Throws.Nothing));
        }

        // OnDestroy 0x610210 stops the unlocking coroutine reference (StopUtilCoroutine on the field), which is then empty.
        [Test]
        public void DestroyingLeavesNoUnlockingCoroutine()
        {
            Run(rig =>
            {
                Call(rig.Container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(Get<Coroutine>(rig.Container, "m_unlockingCoroutine"), Is.Null);
            });
        }
    }
}
