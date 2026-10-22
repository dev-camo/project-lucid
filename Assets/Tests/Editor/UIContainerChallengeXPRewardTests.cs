using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
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
    // UIContainerChallengeXPReward (Game.Runtime 0x0200088a, x86-64 0x61F750..0x620B0A, iterators 0x0200088b and 0x0200088c) as a real
    // component on an inactive owned host over the genuine ChallengeManager (registered, with the open save's challenge data and one
    // reward track state), the reward track definition and the open save's track record, with two recording rewards of 20 and 60 XP.
    // The slider is the real mini widget over a real Slider. A coroutine start on an inactive host is refused with a logged error in
    // Edit Mode, so every case that reaches one expects that log; the iterators are stepped by hand. Not covered: the call-to-action
    // prompt shown for a seen track (it needs the prompt manager) and the frame timing of the two animations.
    public sealed class UIContainerChallengeXPRewardTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        // Reflection: the process coroutine host is the private static s_instance, which only an active CoroutineUtils.Awake sets;
        // a case swaps in an inactive component (its coroutine starts are refused) and restores the prior host with its release.
        private static readonly FieldInfo CoroutineHost =
            typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private const string FirstGuid = "lucid-xp-reward-one";
        private const string SecondGuid = "lucid-xp-reward-two";
        private const Strings FormatString = Strings.MENU_TAILS_CHALLENGES_LABEL_REWARD_XP;
        private const Strings FirstName = Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_MUSIC_TRACK;
        private const Strings SecondName = Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_STATUE;

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string> { { FormatString, "XP progress" } };

        private sealed class Rig
        {
            public OwnedChallengeRewardServices Services;
            public ChallengeManager Manager;
            public UIContainerChallengeXPReward Container;
            public RewardTrackDefinition Definition;
            public SaveDataChallengeRewardTrack Track;
            public ChallengeRewardTrackState State;
            public UIChallengeTrackRewardRecordingReward First, Second;
            public Sprite FirstSprite, SecondSprite;
            public Texture FirstTexture, SecondTexture;
            public UIWidgetChallengesSliderMini Mini;
            public Slider Slider;
            public Animation Animation;
            public AnimationClip TickClip, UnlockClip;
        }

        // The services (and with them the reward base's references) come first; the manager is registered before the container caches
        // its reference; the container is built last.
        private static void Run(Action<Owned, Rig> body, bool slider = false, bool animation = false) =>
            OwnedStringTable.Run(Texts, () => Owned.Run(owned =>
            {
                var rig = new Rig();
                rig.Services = owned.Lease(new OwnedChallengeRewardServices());
                OwnedChallengeServices.Register(owned);
                rig.Manager = ProcessManager.GetSystem<ChallengeManager>();
                rig.First = NewReward<UIChallengeTrackRewardRecordingReward>(owned, "{\"m_guid\":\"" + FirstGuid + "\",\"m_xpThreshold\":20}");
                rig.Second = NewReward<UIChallengeTrackRewardRecordingReward>(owned, "{\"m_guid\":\"" + SecondGuid + "\",\"m_xpThreshold\":60}");
                rig.First.Name = FirstName;
                rig.Second.Name = SecondName;
                rig.First.Texture = rig.FirstTexture = owned.Track(new Texture2D(2, 2));
                rig.Second.Texture = rig.SecondTexture = owned.Track(new Texture2D(2, 2));
                rig.First.Sprite = rig.FirstSprite = NewSprite(owned);
                rig.Second.Sprite = rig.SecondSprite = NewSprite(owned);
                rig.Definition = Create<RewardTrackDefinition>(owned,
                    "{\"m_type\":" + (int)RewardTrackType.Adventure + ",\"m_rewardGlowColour\":{\"r\":0.25,\"g\":0.5,\"b\":0.75,\"a\":1}}");
                rig.Definition.Rewards.Add(rig.First);
                rig.Definition.Rewards.Add(rig.Second);
                rig.Services.Data.RewardTrackDefinitions[RewardTrackType.Adventure] = rig.Definition;
                SaveDataGame save = rig.Services.Save.CurrentSave;
                rig.Track = save.GetOrCreateRewardTrackData(RewardTrackType.Adventure);
                rig.Track.CreateRewardData(FirstGuid, "lucid-xp-reward-one-id");
                rig.Track.CreateRewardData(SecondGuid, "lucid-xp-reward-two-id");
                rig.State = new ChallengeRewardTrackState(rig.Definition);
                // Reflection: the manager takes the open save's challenge data in OnSaveGameOpen and fills its states from its data manager
                // in a callback; both need the App.
                Set(rig.Manager, "m_saveDataChallenges", save.SaveDataChallenges);
                Get<Dictionary<RewardTrackType, ChallengeRewardTrackState>>(rig.Manager, "m_rewardTrackStates")[RewardTrackType.Adventure] = rig.State;

                rig.Container = owned.Host("Lucid xp reward container", false).AddComponent<UIContainerChallengeXPReward>();
                Set(rig.Container, "m_rewardTrackType", RewardTrackType.Adventure); // Reflection: serialized fields
                Set(rig.Container, "m_xpFormatString", FormatString);
                if (slider) AddSlider(owned, rig);
                if (animation) AddAnimation(owned, rig);
                body(owned, rig);
            }));

        // The mini slider's animation starts on the process coroutine host, which a case supplies as an inactive component.
        private static void AddSlider(Owned owned, Rig rig)
        {
            rig.Slider = owned.Host("Lucid xp reward slider", false).AddComponent<Slider>();
            rig.Mini = owned.Host("Lucid xp reward mini slider", false).AddComponent<UIWidgetChallengesSliderMini>();
            Set(rig.Mini, "m_slider", rig.Slider); // Reflection: serialized reference
            Set(rig.Container, "m_xpSlider", rig.Mini);
            object prior = CoroutineHost.GetValue(null);
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => CoroutineHost.SetValue(null, prior)));
            CoroutineHost.SetValue(null, owned.Host("Lucid xp reward coroutine host", false).AddComponent<CoroutineUtils>());
        }

        private static AnimationClip NewClip(Owned owned, string name)
        {
            var clip = owned.Track(new AnimationClip { name = name, legacy = true });
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        private static void AddAnimation(Owned owned, Rig rig)
        {
            rig.Animation = owned.Host("Lucid xp reward animation", true).AddComponent<Animation>();
            rig.TickClip = NewClip(owned, "Lucid xp tick");
            rig.UnlockClip = NewClip(owned, "Lucid xp unlock");
            rig.Animation.AddClip(rig.TickClip, rig.TickClip.name);
            rig.Animation.AddClip(rig.UnlockClip, rig.UnlockClip.name);
            rig.Animation.clip = rig.TickClip;
            Set(rig.Container, "m_animation", rig.Animation); // Reflection: serialized references
            Set(rig.Container, "m_unlockAnimation", rig.UnlockClip);
        }

        // Spends and earns the given XP on the open save, sets the track's unlock state and the collected flags, and lets the
        // track state read them (RefreshRewards owns a reward once its threshold is spent, as the manager's check does).
        private static void Configure(Rig rig, int spent, int earned, FeatureUnlockState unlock, bool firstCollected = false, bool secondCollected = false)
        {
            rig.Track.SpentXP = spent;
            rig.Track.UnlockState = unlock;
            rig.Track.GetRewardDataByTrackGUIDUnsafe(FirstGuid).Collected = firstCollected;
            rig.Track.GetRewardDataByTrackGUIDUnsafe(SecondGuid).Collected = secondCollected;
            rig.Services.Save.CurrentSave.SaveDataChallenges.TotalXPEarned = earned;
            rig.State.OnSaveGameOpen(rig.Track);
            rig.State.RefreshRewards((reward, spentXP) => reward.XPThreshold <= spentXP, reward => { });
        }

        // The slider setup of one call with a slider: the mini widget's animation and the container's label animation both start a
        // coroutine on an inactive host.
        private static void ExpectSliderCoroutines(int calls = 1)
        {
            for (int i = 0; i < 2 * calls; i++) LogAssert.Expect(LogType.Error, RefusedCoroutine);
        }

        private static float WaitSeconds(object yielded) => (float)typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(yielded);

        private static void AssertIteratorShape(Type container, string name, (string, int)[] fields)
        {
            Type iterator = container.GetNestedType(name, BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null, name);
            AssertMembers(iterator, 0x00100103, fields,
                new (string, int)[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1), ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
        }

        // dump.cs TypeDefIndex 2187/2185/2186 and the DummyDll rows (fields 0x04002180..0x0400219c, methods 0x0600314f..0x06003169). The
        // iterator names carry the methods' positions in the type (d__33, d__36).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            var bindables = new[] { "RewardTexture", "RewardSprite", "HasReward", "IsUnlocked", "SpentXP", "SpentXPString", "AvailableXP",
                "AvailableXPString", "TotalXP", "TotalXPString", "FormattedString", "RewardIsCollectable", "RewardCollectableType", "TrackGlowColour" };
            var fields = new List<(string, int)>();
            foreach (string name in new[] { "m_rewardTrackType", "m_xpSlider", "m_xpFormatString", "m_animation", "m_unlockAnimation",
                "m_xpAnimateTickSeconds", "m_ctaPromptTrigger" }) fields.Add((name, 0x0001));
            foreach (string name in bindables) fields.Add((name, 0x0026));
            fields.Add(("m_dataManagerRef", 0x0021));
            fields.Add(("m_challengeManagerRef", 0x0021));
            fields.Add(("m_challengeReward", 0x0001));
            fields.Add(("m_unlockAnimationMidpointReached", 0x0001));
            AssertMembers(typeof(UIContainerChallengeXPReward), 0x00100101, fields.ToArray(),
                new (string, int)[]
                {
                    ("Start", 0x0081), ("SetupSlider", 0x0081), ("SetupNextReward", 0x0081), ("OnDestroy", 0x0081),
                    ("UnloadCachedChallengeReward", 0x0081), ("SetupNoReward", 0x0081), ("SetTexture", 0x0081), ("SetSprite", 0x0081),
                    ("AnimateLabel", 0x0081), ("UpdateBindableNumberString", 0x0081), ("RefreshXP", 0x0086), ("ShowUnlockAnimation", 0x0081),
                    ("ShowProgressTowardsNextReward", 0x0081), ("UnlockMidAnimation", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerChallengeXPReward), 1, 2);
            Assert.That(typeof(UIContainerChallengeXPReward).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertIteratorShape(typeof(UIContainerChallengeXPReward), "<AnimateLabel>d__33",
                new (string, int)[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006), ("target", 0x0006) });
            AssertIteratorShape(typeof(UIContainerChallengeXPReward), "<ShowUnlockAnimation>d__36",
                new (string, int)[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006) });
        }

        // The constructor 0x620350 sets the label tick to 0.1 seconds (0x3dcccccd) at +0x60, creates the fourteen bindables and takes the
        // data and challenge manager references (+0xE0, +0xE8).
        [Test]
        public void ConstructionSetsTheTickAndTakesTheManagerReferences()
        {
            Run((owned, rig) =>
            {
                var container = rig.Container;
                Assert.That(Get<float>(container, "m_xpAnimateTickSeconds"), Is.EqualTo(0.1f));
                Assert.That(Get<SystemRef<DataManager>>(container, "m_dataManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<DataManager>()));
                Assert.That(Get<SystemRef<ChallengeManager>>(container, "m_challengeManagerRef").Get(), Is.SameAs(rig.Manager));
                var bindables = new object[]
                {
                    container.RewardTexture, container.RewardSprite, container.HasReward, container.IsUnlocked, container.SpentXP,
                    container.SpentXPString, container.AvailableXP, container.AvailableXPString, container.TotalXP, container.TotalXPString,
                    container.FormattedString, container.RewardIsCollectable, container.RewardCollectableType, container.TrackGlowColour,
                };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(new HashSet<object>(bindables).Count, Is.EqualTo(14));
                Assert.That(Get<ChallengeReward>(container, "m_challengeReward"), Is.Null);
                Assert.That(Get<bool>(container, "m_unlockAnimationMidpointReached"), Is.False);
            });
        }

        // SetTexture 0x61FE80 and SetSprite 0x61FEA0 store into their own bindables; UnlockMidAnimation 0x620340 sets the flag at +0xF8.
        [Test]
        public void TheImageSettersAndTheMidpointSignalStoreTheirValues()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "SetTexture", rig.FirstTexture); // Reflection: private UIContainerChallengeXPReward.SetTexture
                Call(rig.Container, "SetSprite", rig.FirstSprite); // Reflection: private UIContainerChallengeXPReward.SetSprite

                Assert.That(rig.Container.RewardTexture.Value, Is.SameAs(rig.FirstTexture));
                Assert.That(rig.Container.RewardSprite.Value, Is.SameAs(rig.FirstSprite));

                rig.Container.UnlockMidAnimation();

                Assert.That(Get<bool>(rig.Container, "m_unlockAnimationMidpointReached"), Is.True);
            });
        }

        // 0x61FEE0: the float bindable receives the number and the string bindable its "F0" text, rounded to a whole number.
        [TestCase(7f, "7", TestName = "AWholeNumberIsShownAsIs")]
        [TestCase(7.4f, "7", TestName = "ANumberBelowHalfRoundsDown")]
        [TestCase(7.6f, "8", TestName = "ANumberAboveHalfRoundsUp")]
        [TestCase(0f, "0", TestName = "ZeroIsShown")]
        public void ANumberIsPublishedAsAFloatAndAWholeNumberString(float number, string text)
        {
            Run((owned, rig) =>
            {
                var floatBindable = new Hardlight.UI.Binding.Bindable<float>();
                var stringBindable = new Hardlight.UI.Binding.Bindable<string>();

                Call(rig.Container, "UpdateBindableNumberString", floatBindable, stringBindable, number); // Reflection: private UIContainerChallengeXPReward.UpdateBindableNumberString

                Assert.That(floatBindable.Value, Is.EqualTo(number));
                Assert.That(stringBindable.Value, Is.EqualTo(text));
            });
        }

        // SetupSlider 0x61FA80 without a slider returns at once (the op_Equality at 0x61FAE2): no widget is set up and no coroutine starts.
        [Test]
        public void WithoutASliderTheSliderSetupDoesNothing()
        {
            Run((owned, rig) => Assert.DoesNotThrow(() => Call(rig.Container, "SetupSlider", 3, 9))); // Reflection: private UIContainerChallengeXPReward.SetupSlider
        }

        // With a slider the mini widget is set up with the spent XP as value and the total as maximum, then its animation starts, and the
        // label animation starts last.
        [Test]
        public void TheSliderIsSetUpWithTheSpentAndTotalExperience()
        {
            Run((owned, rig) =>
            {
                ExpectSliderCoroutines();

                Call(rig.Container, "SetupSlider", 3, 9); // Reflection: private UIContainerChallengeXPReward.SetupSlider

                Assert.That(rig.Slider.maxValue, Is.EqualTo(9f));
                Assert.That(rig.Slider.value, Is.EqualTo(3f));
                var parameters = Get<UIWidgetChallengesSliderMiniParameters>(rig.Mini, "m_parameters");
                Assert.That(parameters.Value, Is.EqualTo(3f));
                Assert.That(parameters.MaxValue, Is.EqualTo(9));
            }, slider: true);
        }

        // SetupNoReward 0x61FE40: no reward, and the slider reset to nothing out of 100 (edx = 0x64).
        [Test]
        public void NoRewardHidesTheRewardAndResetsTheSlider()
        {
            Run((owned, rig) =>
            {
                rig.Container.HasReward.Value = true;
                ExpectSliderCoroutines();

                Call(rig.Container, "SetupNoReward"); // Reflection: private UIContainerChallengeXPReward.SetupNoReward

                Assert.That(rig.Container.HasReward.Value, Is.False);
                Assert.That(rig.Slider.maxValue, Is.EqualTo(100f));
                Assert.That(rig.Slider.value, Is.Zero);
            }, slider: true);
        }

        // SetupNextReward 0x61FC40: a previous reward's image is unloaded first (the op_Equality at 0x61FCA3), then the new reward is cached, shown
        // as present, named, and asked to load its images into the texture and sprite bindables (callbacks SetTexture, SetSprite).
        [Test]
        public void TheNextRewardIsShownAndItsImagesLoadedIntoTheBindables()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "SetupNextReward", rig.First); // Reflection: private UIContainerChallengeXPReward.SetupNextReward

                Assert.That(rig.First.Calls, Is.EqualTo(new[] { "GetRewardName", "LoadImage" }), "nothing was cached, so nothing is unloaded");
                Assert.That(Get<ChallengeReward>(rig.Container, "m_challengeReward"), Is.SameAs(rig.First));
                Assert.That(rig.Container.HasReward.Value, Is.True);
                Assert.That(rig.Container.RewardCollectableType.Value, Is.EqualTo(FirstName));
                Assert.That(rig.Container.RewardTexture.Value, Is.SameAs(rig.FirstTexture), "the first callback fills the texture");
                Assert.That(rig.Container.RewardSprite.Value, Is.SameAs(rig.FirstSprite), "the second callback fills the sprite");

                Call(rig.Container, "SetupNextReward", rig.Second); // Reflection: private UIContainerChallengeXPReward.SetupNextReward

                Assert.That(rig.First.Calls, Is.EqualTo(new[] { "GetRewardName", "LoadImage", "UnloadImage" }));
                Assert.That(rig.Second.Calls, Is.EqualTo(new[] { "GetRewardName", "LoadImage" }));
                Assert.That(Get<ChallengeReward>(rig.Container, "m_challengeReward"), Is.SameAs(rig.Second));
                Assert.That(rig.Container.RewardCollectableType.Value, Is.EqualTo(SecondName));
                Assert.That(rig.Container.RewardSprite.Value, Is.SameAs(rig.SecondSprite));
            });
        }

        // OnDestroy 0x61FDF0 and UnloadCachedChallengeReward 0x61FDA0 unload the cached reward's image, and do nothing without one.
        [Test]
        public void DestroyingUnloadsTheCachedRewardImage()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Call(rig.Container, "UnloadCachedChallengeReward"); // Reflection: private UIContainerChallengeXPReward.UnloadCachedChallengeReward
                Assert.That(rig.First.Calls, Is.Empty);

                Call(rig.Container, "SetupNextReward", rig.First); // Reflection: private UIContainerChallengeXPReward.SetupNextReward
                Call(rig.Container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Call(rig.Container, "UnloadCachedChallengeReward"); // Reflection: private UIContainerChallengeXPReward.UnloadCachedChallengeReward

                Assert.That(rig.First.Calls, Is.EqualTo(new[] { "GetRewardName", "LoadImage", "UnloadImage", "UnloadImage" }));
            });
        }

        // Start 0x61F750 tail-calls RefreshXP: a locked track shows no reward and a reset slider.
        [Test]
        public void StartRefreshesTheLockedTrack()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, FeatureUnlockState.Locked);
                rig.Container.HasReward.Value = true;
                ExpectSliderCoroutines();

                Call(rig.Container, "Start"); // Reflection: Unity sends Start; Edit Mode does not

                Assert.That(rig.Container.HasReward.Value, Is.False);
                Assert.That(rig.Slider.maxValue, Is.EqualTo(100f));
            }, slider: true);
        }

        // RefreshXP 0x61F760, locked track (state 0): the glow colour of the track definition is published (+0x30 of the definition), the
        // track is not unlocked (state >= 10 is false), no reward is shown, the slider is reset and the collectable flag is false.
        [Test]
        public void ALockedTrackShowsNoRewardAndAResetSlider()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, FeatureUnlockState.Locked);
                rig.Container.HasReward.Value = true;
                rig.Container.IsUnlocked.Value = true;
                rig.Container.RewardIsCollectable.Value = true;
                ExpectSliderCoroutines();

                rig.Container.RefreshXP();

                Assert.That(rig.Container.TrackGlowColour.Value, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
                Assert.That(rig.Container.IsUnlocked.Value, Is.False);
                Assert.That(rig.Container.HasReward.Value, Is.False);
                Assert.That(rig.Container.RewardIsCollectable.Value, Is.False);
                Assert.That(rig.Slider.maxValue, Is.EqualTo(100f));
                Assert.That(rig.Slider.value, Is.Zero);
            }, slider: true);
        }

        // Unlocked but not yet seen (state 10): the track counts as unlocked, the unlock animation is started (a coroutine on the container,
        // refused here) and nothing else about the reward changes; the collectable flag is written false.
        [Test]
        public void AnUnlockedTrackStartsTheUnlockAnimationAndTouchesNothingElse()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, FeatureUnlockState.Unlocked);
                rig.Container.HasReward.Value = true;
                rig.Container.RewardIsCollectable.Value = true;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                rig.Container.RefreshXP();

                Assert.That(rig.Container.IsUnlocked.Value, Is.True);
                Assert.That(rig.Container.HasReward.Value, Is.True, "the reward display is not touched");
                Assert.That(rig.Container.RewardIsCollectable.Value, Is.False);
            });
        }

        // A state that is none of the three (5 here) is not unlocked (5 < 10), logs the unknown-state error naming the track and the track
        // state object itself (not its unlock state), then behaves as a locked track.
        [Test]
        public void AnUnknownStateLogsAnErrorAndShowsNoReward()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, (FeatureUnlockState)5);
                rig.Container.HasReward.Value = true;
                rig.Container.RewardIsCollectable.Value = true;
                ExpectSliderCoroutines();

                List<string> errors = HLOutputDiagnosticCapture.CaptureErrors(rig.Container.RefreshXP);

                Assert.That(errors.Count, Is.EqualTo(1));
                Assert.That(errors[0], Does.Match(@"^Reward track .+ is in unknown state: HardlightProject\.ChallengeRewardTrackState:0$"));
                Assert.That(rig.Container.IsUnlocked.Value, Is.False);
                Assert.That(rig.Container.HasReward.Value, Is.False);
                Assert.That(rig.Container.RewardIsCollectable.Value, Is.False);
                Assert.That(rig.Slider.maxValue, Is.EqualTo(100f));
            }, slider: true);
        }

        // A seen track (state 20) shows its progress; the collectable flag is the answer of ShowProgressTowardsNextReward.
        [Test]
        public void ASeenTrackShowsItsProgressAndTheCollectableFlag()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 30, 70, FeatureUnlockState.Seen);
                ExpectSliderCoroutines();

                rig.Container.RefreshXP();

                Assert.That(rig.Container.IsUnlocked.Value, Is.True);
                Assert.That(rig.Container.RewardIsCollectable.Value, Is.True);
                Assert.That(rig.Container.HasReward.Value, Is.True);
                Assert.That(rig.Container.SpentXPString.Value, Is.EqualTo("30"));
            }, slider: true);
        }

        // ShowProgressTowardsNextReward 0x61FFE0, a reward that can be collected (threshold 20 spent, not collected): it is the reward shown,
        // before the unreached one (60). Spent 30 and available 40 (earned 70 less spent 30) are published as floats and whole-number strings;
        // the reward's range is its threshold less the previous one (none: 0), so 20; the progress is spent less the previous threshold plus
        // the available XP (70), clamped to the range for the slider; reaching the range makes the answer true.
        [Test]
        public void ACollectableRewardIsShownBeforeTheNextUnreachedOne()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 30, 70, FeatureUnlockState.Seen);
                ExpectSliderCoroutines();

                bool collectable = (bool)Call(rig.Container, "ShowProgressTowardsNextReward", rig.State, rig.Manager); // Reflection: private UIContainerChallengeXPReward.ShowProgressTowardsNextReward

                Assert.That(collectable, Is.True);
                Assert.That(rig.Container.RewardIsCollectable.Value, Is.True);
                Assert.That(rig.Container.SpentXP.Value, Is.EqualTo(30f));
                Assert.That(rig.Container.SpentXPString.Value, Is.EqualTo("30"));
                Assert.That(rig.Container.AvailableXP.Value, Is.EqualTo(40f));
                Assert.That(rig.Container.AvailableXPString.Value, Is.EqualTo("40"));
                Assert.That(rig.Container.FormattedString.Value, Is.EqualTo("XP progress"));
                Assert.That(rig.Container.TotalXP.Value, Is.EqualTo(20f));
                Assert.That(rig.Container.TotalXPString.Value, Is.EqualTo("20"));
                Assert.That(Get<ChallengeReward>(rig.Container, "m_challengeReward"), Is.SameAs(rig.First));
                Assert.That(rig.Container.RewardCollectableType.Value, Is.EqualTo(FirstName));
                Assert.That(rig.Slider.maxValue, Is.EqualTo(20f));
                Assert.That(rig.Slider.value, Is.EqualTo(20f), "the progress 70 is clamped to the range");
            }, slider: true);
        }

        // With the first reward collected, the unreached next reward (60) is shown with the first as its previous reward: the range is
        // 60 - 20 = 40 and the progress 30 - 20 + 15 = 25 (earned 45 less spent 30), which is short of the range: the slider shows the
        // progress, the answer is false, and the collectable flag is not written (it keeps the value it had).
        [Test]
        public void TheNextRewardIsMeasuredFromThePreviousOne()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 30, 45, FeatureUnlockState.Seen, firstCollected: true);
                rig.Container.RewardIsCollectable.Value = true;
                ExpectSliderCoroutines();

                bool collectable = (bool)Call(rig.Container, "ShowProgressTowardsNextReward", rig.State, rig.Manager); // Reflection: private UIContainerChallengeXPReward.ShowProgressTowardsNextReward

                Assert.That(collectable, Is.False);
                Assert.That(rig.Container.RewardIsCollectable.Value, Is.True, "only the collectable branch writes the flag");
                Assert.That(rig.Container.AvailableXP.Value, Is.EqualTo(15f));
                Assert.That(rig.Container.TotalXP.Value, Is.EqualTo(40f));
                Assert.That(rig.Container.TotalXPString.Value, Is.EqualTo("40"));
                Assert.That(Get<ChallengeReward>(rig.Container, "m_challengeReward"), Is.SameAs(rig.Second));
                Assert.That(rig.Slider.maxValue, Is.EqualTo(40f));
                Assert.That(rig.Slider.value, Is.EqualTo(25f));
            }, slider: true);
        }

        // The answer compares the range with the progress (cmp r13d, r14d; setle): a progress exactly equal to the range reaches it, one
        // short of it does not. Range 40 (reward two after reward one), spent 30: the available XP decides the progress 10 + available.
        [TestCase(29, false, TestName = "AProgressOneShortOfTheRangeIsNotReached")]
        [TestCase(30, true, TestName = "AProgressEqualToTheRangeIsReached")]
        [TestCase(31, true, TestName = "AProgressAboveTheRangeIsReached")]
        public void TheRangeIsReachedWhenTheProgressEqualsIt(int available, bool reached)
        {
            Run((owned, rig) =>
            {
                Configure(rig, 30, 30 + available, FeatureUnlockState.Seen, firstCollected: true);
                ExpectSliderCoroutines();

                Assert.That(Call(rig.Container, "ShowProgressTowardsNextReward", rig.State, rig.Manager), Is.EqualTo(reached)); // Reflection: private UIContainerChallengeXPReward.ShowProgressTowardsNextReward
            }, slider: true);
        }

        // With nothing left to collect or reach, the reward display is cleared and the slider reset (0 of 100); the spent and available
        // XP and the format text are published first.
        [Test]
        public void WithoutAnyRewardLeftTheDisplayIsCleared()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 100, 150, FeatureUnlockState.Seen, firstCollected: true, secondCollected: true);
                rig.Container.HasReward.Value = true;
                ExpectSliderCoroutines();

                bool collectable = (bool)Call(rig.Container, "ShowProgressTowardsNextReward", rig.State, rig.Manager); // Reflection: private UIContainerChallengeXPReward.ShowProgressTowardsNextReward

                Assert.That(collectable, Is.False);
                Assert.That(rig.Container.HasReward.Value, Is.False);
                Assert.That(rig.Container.SpentXPString.Value, Is.EqualTo("100"));
                Assert.That(rig.Container.AvailableXPString.Value, Is.EqualTo("50"));
                Assert.That(rig.Container.FormattedString.Value, Is.EqualTo("XP progress"));
                Assert.That(rig.Container.TotalXPString.Value, Is.Null, "the range is only published for a reward");
                Assert.That(rig.Slider.maxValue, Is.EqualTo(100f));
                Assert.That(rig.Slider.value, Is.Zero);
            }, slider: true);
        }

        // AnimateLabel (0x61FBC0, MoveNext 0x6207D0) without an animation ends at once and touches no bindable.
        [Test]
        public void TheLabelAnimationNeedsAnAnimation()
        {
            Run((owned, rig) =>
            {
                rig.Container.SpentXP.Value = 5f;
                var steps = (IEnumerator)Call(rig.Container, "AnimateLabel", 3); // Reflection: private UIContainerChallengeXPReward.AnimateLabel

                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(rig.Container.SpentXP.Value, Is.EqualTo(5f));
            });
        }

        // The label counts up one point per tick: the spent XP is reset to 0 and the animation played, then each wait of the tick time (0.25 s
        // authored) is followed by one more point until the target is reached, when the animation is stopped.
        [Test]
        public void TheLabelCountsUpToTheTargetAndThenStopsTheAnimation()
        {
            Run((owned, rig) =>
            {
                Set(rig.Container, "m_xpAnimateTickSeconds", 0.25f); // Reflection: serialized tick time
                rig.Container.SpentXP.Value = 9f;
                var steps = (IEnumerator)Call(rig.Container, "AnimateLabel", 2);

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(WaitSeconds(steps.Current), Is.EqualTo(0.25f));
                Assert.That(rig.Container.SpentXP.Value, Is.Zero);
                Assert.That(rig.Container.SpentXPString.Value, Is.EqualTo("0"));
                Assert.That(rig.Animation.isPlaying, Is.True);

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(WaitSeconds(steps.Current), Is.EqualTo(0.25f));
                Assert.That(rig.Container.SpentXP.Value, Is.EqualTo(1f));
                Assert.That(rig.Container.SpentXPString.Value, Is.EqualTo("1"));
                Assert.That(rig.Animation.isPlaying, Is.True);

                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(rig.Container.SpentXP.Value, Is.EqualTo(2f));
                Assert.That(rig.Container.SpentXPString.Value, Is.EqualTo("2"));
                Assert.That(rig.Animation.isPlaying, Is.False);
            }, animation: true);
        }

        // The loop test is the strict target > spent (ucomiss, jbe): a zero or negative target counts nothing, and still resets the
        // number to 0 first and plays and stops the animation.
        [TestCase(0, TestName = "ATargetOfZeroCountsNothing")]
        [TestCase(-3, TestName = "ANegativeTargetCountsNothing")]
        public void ATargetNotAboveZeroEndsAfterTheReset(int target)
        {
            Run((owned, rig) =>
            {
                rig.Container.SpentXP.Value = 9f;
                var steps = (IEnumerator)Call(rig.Container, "AnimateLabel", target); // Reflection: private UIContainerChallengeXPReward.AnimateLabel

                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(rig.Container.SpentXP.Value, Is.Zero);
                Assert.That(rig.Container.SpentXPString.Value, Is.EqualTo("0"));
                Assert.That(rig.Animation.isPlaying, Is.False);
            }, animation: true);
        }

        // An animation that is still running holds the count back (a null step per frame, 0x620953) without touching the number, until
        // it has stopped.
        [Test]
        public void TheLabelWaitsForARunningAnimation()
        {
            Run((owned, rig) =>
            {
                rig.Container.SpentXP.Value = 9f;
                rig.Animation.Play();
                Assert.That(rig.Animation.isPlaying, Is.True, "set-up: the animation runs");
                var steps = (IEnumerator)Call(rig.Container, "AnimateLabel", 1); // Reflection: private UIContainerChallengeXPReward.AnimateLabel

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.Null);
                Assert.That(rig.Container.SpentXP.Value, Is.EqualTo(9f));

                rig.Animation.Stop();

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.InstanceOf<WaitForSeconds>());
                Assert.That(rig.Container.SpentXP.Value, Is.Zero);
            }, animation: true);
        }

        // ShowUnlockAnimation (0x61FF70, MoveNext 0x620B20) without an animation goes straight to marking the track seen on the manager (the
        // open save's record) and refreshing, which starts the unlock animation again for the still-cached unlocked state (refused here).
        [Test]
        public void WithoutAnAnimationTheTrackIsMarkedSeenAtOnce()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, FeatureUnlockState.Unlocked);
                var steps = (IEnumerator)Call(rig.Container, "ShowUnlockAnimation"); // Reflection: private UIContainerChallengeXPReward.ShowUnlockAnimation
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Assert.That(steps.MoveNext(), Is.False);

                Assert.That(rig.Track.UnlockState, Is.EqualTo(FeatureUnlockState.Seen));
                Assert.That(rig.Container.TrackGlowColour.Value, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)), "RefreshXP ran");
            });
        }

        // With an animation the unlock clip is played by name and the iterator waits (a null step) while it plays and the midpoint has not been
        // signalled; the signal releases it.
        [Test]
        public void TheUnlockAnimationWaitsForTheMidpointSignal()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, FeatureUnlockState.Unlocked);
                var steps = (IEnumerator)Call(rig.Container, "ShowUnlockAnimation"); // Reflection: private UIContainerChallengeXPReward.ShowUnlockAnimation

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.Null);
                Assert.That(rig.Animation.IsPlaying(rig.UnlockClip.name), Is.True);
                Assert.That(rig.Track.UnlockState, Is.EqualTo(FeatureUnlockState.Unlocked), "not marked seen yet");
                Assert.That(steps.MoveNext(), Is.True, "still waiting");

                rig.Container.UnlockMidAnimation();
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(rig.Track.UnlockState, Is.EqualTo(FeatureUnlockState.Seen));
            }, animation: true);
        }

        // An animation that finishes by itself ends the wait too (the test is isPlaying && !midpoint).
        [Test]
        public void AnAnimationThatEndsReleasesTheUnlockAnimation()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, FeatureUnlockState.Unlocked);
                var steps = (IEnumerator)Call(rig.Container, "ShowUnlockAnimation"); // Reflection: private UIContainerChallengeXPReward.ShowUnlockAnimation
                Assert.That(steps.MoveNext(), Is.True);

                rig.Animation.Stop();
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(rig.Track.UnlockState, Is.EqualTo(FeatureUnlockState.Seen));
            }, animation: true);
        }

        // The midpoint flag is not reset by the iterator: a signal given before it starts releases it right after the clip is played.
        [Test]
        public void AMidpointSignalGivenBeforehandSkipsTheWait()
        {
            Run((owned, rig) =>
            {
                Configure(rig, 0, 0, FeatureUnlockState.Unlocked);
                rig.Container.UnlockMidAnimation();
                var steps = (IEnumerator)Call(rig.Container, "ShowUnlockAnimation"); // Reflection: private UIContainerChallengeXPReward.ShowUnlockAnimation
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Assert.That(steps.MoveNext(), Is.False);

                Assert.That(rig.Animation.IsPlaying(rig.UnlockClip.name), Is.True, "the clip was started before the check");
                Assert.That(rig.Track.UnlockState, Is.EqualTo(FeatureUnlockState.Seen));
            }, animation: true);
        }
    }
}
