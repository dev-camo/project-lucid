using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetButtonChallengeReward (Game.Runtime 0x0200099c, x86-64 0x699DA0..0x69A870) and its parameters (0x0200099d, 0x69A870..0x69A950)
    // as a real component on an inactive owned host next to a real UITransitionAnimation, over a recording reward, the DataManager that
    // holds the track colours and five legacy clips on an active animation host. A queued custom animation plays the clip it is given,
    // which the cases read back from the animation. Not covered: the zone name of a playable-level reward (it needs the level manager and
    // the zone categories) and the thumbnail loads of music-track and level rewards (they start Addressables loads).
    public sealed class UIWidgetButtonChallengeRewardTests
    {
        private const Strings NextText = Strings.MENU_TAILS_CHALLENGES_LABEL_REWARD;
        private const Strings ReadyText = Strings.MENU_TAILS_CHALLENGES_LABEL_COLLECT;
        private static readonly Color AdventureGlow = new Color(0.25f, 0.5f, 0.75f, 1f);
        private static readonly Color ShadowGlow = new Color(0.75f, 0.5f, 0.25f, 1f);

        private sealed class Rig
        {
            public UIWidgetButtonChallengeReward Widget;
            public UITransitionAnimation Transition;
            public UIChallengeTrackRewardRecordingReward Reward;
            public Sprite Sprite;
            public Texture Texture;
            public Animation Animation;
            public AnimationClip Collected, WithMask, Playable, Ready, Hidden;
            public int Clicks;
            public readonly List<RectTransform> Highlights = new List<RectTransform>();

            public AnimationClip[] Clips => new[] { Collected, WithMask, Playable, Ready, Hidden };

            // The clips that are playing; a queued animation leaves exactly the one it was given.
            public AnimationClip[] Playing => Clips.Where(clip => Animation.IsPlaying(clip.name)).ToArray();
        }

        private static AnimationClip NewClip(Owned owned, string name)
        {
            var clip = owned.Track(new AnimationClip { name = name, legacy = true });
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        // The DataManager is registered before the widget caches its reference.
        private static void Run(Action<Owned, Rig> body) => Owned.Run(owned =>
        {
            var rig = new Rig();
            LeaseRewardRows(owned);
            rig.Reward = NewReward<UIChallengeTrackRewardRecordingReward>(owned);
            rig.Reward.Name = Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_STATUE;
            rig.Reward.Texture = rig.Texture = owned.Track(new Texture2D(2, 2));
            rig.Reward.Sprite = rig.Sprite = NewSprite(owned);
            var data = owned.Host("Lucid reward data manager", false).AddComponent<DataManager>();
            owned.Register(data);
            data.RewardTrackDefinitions[RewardTrackType.Adventure] = Create<RewardTrackDefinition>(owned,
                "{\"m_rewardGlowColour\":{\"r\":0.25,\"g\":0.5,\"b\":0.75,\"a\":1}}");
            data.RewardTrackDefinitions[RewardTrackType.Shadow] = Create<RewardTrackDefinition>(owned,
                "{\"m_rewardGlowColour\":{\"r\":0.75,\"g\":0.5,\"b\":0.25,\"a\":1}}");

            GameObject host = owned.Host("Lucid challenge reward button", false);
            rig.Widget = host.AddComponent<UIWidgetButtonChallengeReward>();
            rig.Transition = host.AddComponent<UITransitionAnimation>();
            rig.Animation = owned.Host("Lucid challenge reward animation", true).AddComponent<Animation>();
            rig.Collected = NewClip(owned, "Lucid reward collected");
            rig.WithMask = NewClip(owned, "Lucid reward collected with mask");
            rig.Playable = NewClip(owned, "Lucid reward collected playable");
            rig.Ready = NewClip(owned, "Lucid reward ready");
            rig.Hidden = NewClip(owned, "Lucid reward hidden");
            foreach (AnimationClip clip in rig.Clips) rig.Animation.AddClip(clip, clip.name);
            Set(rig.Transition, "m_animation", rig.Animation); // Reflection: serialized references
            Set(rig.Transition, "m_settings", new List<UITransitionAnimationSetting>());
            Set(rig.Widget, "m_transition", rig.Transition);
            Set(rig.Widget, "m_nextRewardString", NextText);
            Set(rig.Widget, "m_readyToCollectString", ReadyText);
            Set(rig.Widget, "m_collectedAnimationClip", rig.Collected);
            Set(rig.Widget, "m_collectedAnimationClipWithMask", rig.WithMask);
            Set(rig.Widget, "m_collectedAnimationClipPlayable", rig.Playable);
            Set(rig.Widget, "m_readyToCollectAnimationClip", rig.Ready);
            Set(rig.Widget, "m_hiddenAnimationClip", rig.Hidden);
            body(owned, rig);
        });

        private static UIChallengeTrackRewardStatus Status(Rig rig, bool collected, bool unlocked, ChallengeReward reward = null) =>
            new UIChallengeTrackRewardStatus(reward ?? rig.Reward, collected, unlocked, 2, RewardTrackType.Adventure);

        private static UIWidgetButtonChallengesRewardParameters Parameters(Rig rig, UIChallengeTrackRewardStatus status, bool isNext = false,
            bool isLast = false, float position = 100f, RewardTrackType track = RewardTrackType.Adventure) =>
            new UIWidgetButtonChallengesRewardParameters(status, "120 XP", position, isNext, isLast, () => rig.Clicks++, rig.Highlights.Add, track);

        // dump.cs TypeDefIndex 2459 and the DummyDll rows (fields 0x04002836..0x0400284a, methods 0x0600379d..0x060037aa).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetButtonChallengeReward), 0x00100101,
                new (string, int)[]
                {
                    ("m_nextRewardString", 0x0001), ("m_readyToCollectString", 0x0001), ("m_widgetNavigation", 0x0001), ("m_transition", 0x0001),
                    ("m_collectedAnimationClip", 0x0001), ("m_collectedAnimationClipWithMask", 0x0001), ("m_collectedAnimationClipPlayable", 0x0001),
                    ("m_readyToCollectAnimationClip", 0x0001), ("m_hiddenAnimationClip", 0x0001), ("m_offsetBy", 0x0001),
                    ("Action", 0x0026), ("Name", 0x0026), ("RewardXP", 0x0026), ("Thumbnail", 0x0026), ("ThumbnailSprite", 0x0026),
                    ("HasAction", 0x0026), ("ButtonInteractable", 0x0026), ("GlowColour", 0x0026), ("ZoneName", 0x0026),
                    ("m_dataManagerRef", 0x0021), ("m_parameters", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_WidgetNavigation", 0x0886), ("get_RewardStatus", 0x0886), ("Setup", 0x0086), ("OnDestroy", 0x00c4),
                    ("Action_OnButtonClick", 0x0086), ("SetThumbnail", 0x0086), ("SetThumbnailSprite", 0x0086), ("Collect", 0x0086),
                    ("GetAction", 0x0081), ("IsReadyToCollect", 0x0086), ("Action_OnHighlighted", 0x0086), ("Highlight", 0x0086),
                    ("Refresh", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetButtonChallengeReward), 2, 1);
            Assert.That(typeof(UIWidgetButtonChallengeReward).BaseType, Is.EqualTo(typeof(UIInteractable)));
            Assert.That(Tooltip(typeof(UIWidgetButtonChallengeReward).GetField("m_offsetBy", Declared)),
                Is.EqualTo("Offsets the computed position so the separator icon matches the threshold of the slider."));
            var formerly = typeof(UIWidgetButtonChallengeReward).GetField("m_collectedAnimationClipWithMask", Declared)
                .GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false);
            Assert.That(((UnityEngine.Serialization.FormerlySerializedAsAttribute)formerly.Single()).oldName, Is.EqualTo("m_collectedMusicAnimationClip"));
        }

        // dump.cs TypeDefIndex 2460 and the DummyDll rows (fields 0x0400284b..0x04002852, methods 0x060037ab..0x060037ad; the track type
        // defaults to Adventure).
        [Test]
        public void TheParametersDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetButtonChallengesRewardParameters), 0x00100101,
                new (string, int)[]
                {
                    ("<RewardStatus>k__BackingField", 0x0001), ("RewardXP", 0x0026), ("HorizontalPosition", 0x0026), ("IsNextReward", 0x0026),
                    ("IsLast", 0x0026), ("OnClick", 0x0026), ("OnHighlighted", 0x0026), ("TrackType", 0x0026),
                },
                new (string, int)[] { ("get_RewardStatus", 0x0886), ("set_RewardStatus", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetButtonChallengesRewardParameters), 1, 2);
            Assert.That(typeof(UIWidgetButtonChallengesRewardParameters).GetInterfaces(), Does.Contain(typeof(IUIWidgetParameters)));
            AssertTrackTypeDefault();
        }

        private static void AssertTrackTypeDefault()
        {
            var last = typeof(UIWidgetButtonChallengesRewardParameters).GetConstructors().Single().GetParameters().Last();
            Assert.That(last.Name, Is.EqualTo("trackType"));
            Assert.That(last.HasDefaultValue, Is.True);
            Assert.That(Convert.ToInt32(last.DefaultValue), Is.EqualTo((int)RewardTrackType.Adventure));
        }

        // The parameters constructor 0x69A8A0 stores its eight arguments in their own members; the reward status is the only mutable one.
        [Test]
        public void TheParametersKeepTheirValuesApart()
        {
            Run((owned, rig) =>
            {
                UIChallengeTrackRewardStatus first = Status(rig, false, true);
                var highlighted = new List<RectTransform>();
                int clicks = 0;
                var parameters = new UIWidgetButtonChallengesRewardParameters(first, "12 XP", 33.5f, true, false, () => clicks++, highlighted.Add, RewardTrackType.Shadow);

                Assert.That(parameters.RewardStatus, Is.SameAs(first));
                Assert.That(parameters.RewardXP, Is.EqualTo("12 XP"));
                Assert.That(parameters.HorizontalPosition, Is.EqualTo(33.5f));
                Assert.That(parameters.IsNextReward, Is.True);
                Assert.That(parameters.IsLast, Is.False);
                parameters.OnClick();
                Assert.That(clicks, Is.EqualTo(1));
                Assert.That(parameters.OnHighlighted.Target, Is.SameAs(highlighted));
                Assert.That(parameters.TrackType, Is.EqualTo(RewardTrackType.Shadow));
                var swapped = new UIWidgetButtonChallengesRewardParameters(first, "x", 0f, false, true, null, null);
                Assert.That(swapped.IsNextReward, Is.False);
                Assert.That(swapped.IsLast, Is.True);
                Assert.That(swapped.TrackType, Is.EqualTo(RewardTrackType.Adventure), "the default");

                UIChallengeTrackRewardStatus second = Status(rig, true, true);
                parameters.RewardStatus = second;
                Assert.That(parameters.RewardStatus, Is.SameAs(second));
            });
        }

        // The constructor 0x69A540 sets the position offset to 10 (0x41200000) at +0x80, creates the nine bindables and takes the DataManager
        // reference.
        [Test]
        public void ConstructionSetsTheOffsetAndCreatesTheBindables()
        {
            Run((owned, rig) =>
            {
                var widget = rig.Widget;
                Assert.That(Get<float>(widget, "m_offsetBy"), Is.EqualTo(10f));
                Assert.That(Get<SystemRef<DataManager>>(widget, "m_dataManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<DataManager>()));
                var bindables = new object[] { widget.Action, widget.Name, widget.RewardXP, widget.Thumbnail, widget.ThumbnailSprite, widget.HasAction,
                    widget.ButtonInteractable, widget.GlowColour, widget.ZoneName };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(9));
                Assert.That(Get<UIWidgetButtonChallengesRewardParameters>(widget, "m_parameters"), Is.Null);
            });
        }

        // GetAction 0x69A3F0 and Refresh 0x699EE0: the next reward is labelled with the next-reward text whatever its status; any other
        // reward is labelled with the collect text while it can be collected and with none otherwise (0xb6fa1284 is Strings.NONE). The label
        // decides HasAction (not none).
        [TestCase(true, false, false, true, TestName = "ANextRewardIsLabelledNextWhateverItsStatus")]
        [TestCase(true, true, true, true, TestName = "ANextCollectableRewardIsStillLabelledNext")]
        [TestCase(false, true, false, true, TestName = "AnUnlockedUncollectedRewardInvitesCollecting")]
        [TestCase(false, false, false, false, TestName = "ALockedRewardHasNoLabel")]
        [TestCase(false, true, true, false, TestName = "ACollectedRewardHasNoLabel")]
        public void TheLabelFollowsTheNextFlagAndTheCollectableStatus(bool isNext, bool unlocked, bool collected, bool hasAction)
        {
            Run((owned, rig) =>
            {
                UIChallengeTrackRewardStatus status = Status(rig, collected, unlocked);
                rig.Widget.Setup(Parameters(rig, status, isNext));

                Strings expected = isNext ? NextText : (unlocked && !collected ? ReadyText : Strings.NONE);
                Assert.That(rig.Widget.Action.Value, Is.EqualTo(expected));
                Assert.That(rig.Widget.HasAction.Value, Is.EqualTo(hasAction));
                Assert.That(Call(rig.Widget, "GetAction"), Is.EqualTo(expected));
            });
        }

        // Refresh publishes the reward's name, the XP text of the parameters, the interactable state (the widget's own and the bindable) from
        // the readiness, the glow colour of the parameters' track, and wires the two thumbnail callbacks (texture, then sprite).
        [Test]
        public void RefreshPublishesTheRewardTheXpTextTheInteractableStateAndTheGlow()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(Parameters(rig, Status(rig, false, true), track: RewardTrackType.Shadow));

                Assert.That(rig.Widget.Name.Value, Is.EqualTo(Strings.MENU_TAILS_CHALLENGES_REWARD_TRACK_STATUE));
                Assert.That(rig.Widget.RewardXP.Value, Is.EqualTo("120 XP"));
                Assert.That(rig.Widget.ButtonInteractable.Value, Is.True);
                Assert.That(rig.Widget.Interactable, Is.True);
                Assert.That(rig.Widget.GlowColour.Value, Is.EqualTo(ShadowGlow));
                Assert.That(rig.Widget.Thumbnail.Value, Is.SameAs(rig.Texture));
                Assert.That(rig.Widget.ThumbnailSprite.Value, Is.SameAs(rig.Sprite));
                Assert.That(rig.Reward.Calls, Is.EqualTo(new[] { "GetRewardName", "LoadImage" }));

                rig.Widget.Refresh(Status(rig, false, false));

                Assert.That(rig.Widget.ButtonInteractable.Value, Is.False);
                Assert.That(rig.Widget.Interactable, Is.False);
                Assert.That(rig.Widget.GlowColour.Value, Is.EqualTo(ShadowGlow), "the track of the parameters, not of the status");
            });
        }

        // Refresh replaces the status in the parameters (the property reads it back) before it reads anything from it.
        [Test]
        public void RefreshStoresTheNewStatusInTheParameters()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(Parameters(rig, Status(rig, false, false)));
                UIChallengeTrackRewardStatus later = Status(rig, false, true);

                rig.Widget.Refresh(later);

                Assert.That(rig.Widget.RewardStatus, Is.SameAs(later));
                Assert.That(rig.Widget.IsReadyToCollect(), Is.True);
                Assert.That(rig.Widget.Action.Value, Is.EqualTo(ReadyText));
            });
        }

        // 0x69A24C..0x69A275, 0x69A18D: a collected reward queues the collected animation (unlocked and collected), an unlocked one that can
        // still be collected the ready animation, and a locked one the hidden animation; each leaves only its own clip playing.
        [TestCase(true, true, 0, TestName = "ACollectedRewardPlaysTheCollectedAnimation")]
        [TestCase(false, true, 3, TestName = "AnUncollectedUnlockedRewardPlaysTheReadyAnimation")]
        [TestCase(false, false, 4, TestName = "ALockedRewardPlaysTheHiddenAnimation")]
        [TestCase(true, false, 4, TestName = "ALockedRewardMarkedCollectedStillPlaysTheHiddenAnimation")]
        public void RefreshQueuesTheAnimationOfTheStatus(bool collected, bool unlocked, int clipIndex)
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(Parameters(rig, Status(rig, collected, unlocked)));

                Assert.That(rig.Playing, Is.EqualTo(new[] { rig.Clips[clipIndex] }));
            });
        }

        // Collect 0x69A330: a music track reward plays the clip with the mask, a playable level the playable clip, any other reward (and a
        // status without a reward) the plain collected clip. The reward members are not called.
        [Test]
        public void CollectingPicksTheClipOfTheRewardType()
        {
            Run((owned, rig) =>
            {
                owned.LeaseRow<MusicTrackManager>(); // ChallengeRewardMusicTrack's field initialiser
                var music = NewReward<ChallengeRewardMusicTrack>(owned);
                var level = NewReward<ChallengeRewardGameplayLevel>(owned);

                rig.Widget.Collect(Status(rig, true, true, music));
                Assert.That(rig.Playing, Is.EqualTo(new[] { rig.WithMask }));

                rig.Widget.Collect(Status(rig, true, true, level));
                Assert.That(rig.Playing, Is.EqualTo(new[] { rig.Playable }));

                rig.Widget.Collect(Status(rig, true, true));
                Assert.That(rig.Playing, Is.EqualTo(new[] { rig.Collected }));

                rig.Widget.Collect(new UIChallengeTrackRewardStatus(null, true, true, 0, RewardTrackType.Adventure));
                Assert.That(rig.Playing, Is.EqualTo(new[] { rig.Collected }));
                Assert.That(rig.Reward.Calls, Is.Empty);
            });
        }

        // Setup 0x699DD0: the parameters are taken (a foreign object faults in the cast), the widget is refreshed and then placed along the
        // track: x = width * -0.5 + horizontal position + offset (10, or none for the last one), y kept. Width 30: 95 for position 100.
        [TestCase(false, 95f, TestName = "ASetupButtonIsPlacedAtItsPositionLessHalfItsWidthPlusTheOffset")]
        [TestCase(true, 85f, TestName = "TheLastButtonGetsNoOffset")]
        public void SetupPlacesTheButtonAlongTheTrack(bool isLast, float expectedX)
        {
            Run((owned, rig) =>
            {
                var rect = (RectTransform)rig.Widget.transform;
                rect.sizeDelta = new Vector2(30f, 12f);
                rect.anchoredPosition = new Vector2(-5f, 7f);

                rig.Widget.Setup(Parameters(rig, Status(rig, false, true), isLast: isLast));

                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(expectedX, 7f)));
                Assert.That(rig.Widget.Action.Value, Is.EqualTo(ReadyText), "refreshed first");
            });
        }

        // The offset is the authored one, not a fixed 10, and the width term is exact in single precision.
        [Test]
        public void TheAuthoredOffsetAndTheWidthDecidePosition()
        {
            Run((owned, rig) =>
            {
                Set(rig.Widget, "m_offsetBy", 2.5f); // Reflection: serialized offset
                var rect = (RectTransform)rig.Widget.transform;
                rect.sizeDelta = new Vector2(7f, 1f);

                rig.Widget.Setup(Parameters(rig, Status(rig, false, true), position: 20f));

                Assert.That(rect.anchoredPosition.x, Is.EqualTo(19f), "7 * -0.5 + 20 + 2.5");
            });
        }

        [Test]
        public void SetupWithForeignParametersFaults()
        {
            Run((owned, rig) =>
                Assert.Throws<InvalidCastException>(() => rig.Widget.Setup(new UIWidgetChallengesSliderMiniParameters(1f, 2))));
        }

        // Action_OnButtonClick 0x69A2D0 invokes the parameters' click callback; the highlight members 0x69A460 and 0x69A4D0 both pass the
        // widget's transform, as a RectTransform, to the highlight callback.
        [Test]
        public void ClicksAndHighlightsReachTheParametersCallbacks()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(Parameters(rig, Status(rig, false, true)));

                rig.Widget.Action_OnButtonClick();
                rig.Widget.Action_OnButtonClick();
                rig.Widget.Action_OnHighlighted();
                rig.Widget.Highlight();

                Assert.That(rig.Clicks, Is.EqualTo(2));
                Assert.That(rig.Highlights, Is.EqualTo(new[] { rig.Widget.transform, rig.Widget.transform }));
            });
        }

        // The thumbnail setters 0x69A2F0 and 0x69A310 store into their own bindables; the properties read the navigation component and the
        // current status; IsReadyToCollect (0x69A430) is the status's.
        [Test]
        public void TheSettersAndPropertiesReadAndWriteTheirOwnMembers()
        {
            Run((owned, rig) =>
            {
                var navigation = owned.Host("Lucid reward navigation", false).AddComponent<UIWidgetNavigation>();
                Set(rig.Widget, "m_widgetNavigation", navigation); // Reflection: serialized reference
                rig.Widget.SetThumbnail(rig.Texture);
                rig.Widget.SetThumbnailSprite(rig.Sprite);
                UIChallengeTrackRewardStatus status = Status(rig, true, true);
                rig.Widget.Setup(Parameters(rig, status));

                Assert.That(rig.Widget.WidgetNavigation, Is.SameAs(navigation));
                Assert.That(rig.Widget.Thumbnail.Value, Is.SameAs(rig.Texture));
                Assert.That(rig.Widget.ThumbnailSprite.Value, Is.SameAs(rig.Sprite));
                Assert.That(rig.Widget.RewardStatus, Is.SameAs(status));
                Assert.That(rig.Widget.IsReadyToCollect(), Is.False, "collected");
            });
        }

        // OnDestroy 0x69A290 runs the base callback and then unloads the image of the status's reward.
        [Test]
        public void DestroyingUnloadsTheRewardImage()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(Parameters(rig, Status(rig, false, true)));
                rig.Reward.Calls.Clear();

                Call(rig.Widget, "OnDestroy");

                Assert.That(rig.Reward.Calls, Is.EqualTo(new[] { "UnloadImage" }));
            });
        }
    }
}
