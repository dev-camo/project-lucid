using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetToggleMissionParameters (Game.Runtime 0x020009d5, constructor x86-64 0x6A4600) and UIWidgetToggleMission
    // (0x020009d4, Setup 0x6A3CC0 .. constructor 0x6A4290) as a real component on an inactive owned host over a real DataManager
    // whose reward collectable already holds its loaded sprite, so no Addressables load is made.
    // Not covered: the Lock and unlock-animation transitions (0x6A3EB5, 0x6A3ED9), which only queue animations on the toggle's
    // UITransitionAnimation; the fixture gives it an empty settings list so that queuing a state transition is a no-op.
    public sealed class UIWidgetToggleMissionTests
    {
        private sealed class Scene
        {
            public UIWidgetToggleMission Widget;
            public CollectableDefinition Reward;
            public Sprite RewardIcon;
        }

        private static void Run(Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            var scene = new Scene();
            // The widget caches the DataManager reference when it is constructed, so the row is registered first.
            DataManager data = owned.Host("Lucid mission toggle data manager", false).AddComponent<DataManager>();
            owned.Register(data);
            scene.Reward = owned.Track(ScriptableObject.CreateInstance<CollectableDefinition>());
            scene.RewardIcon = NewSprite(owned);
            Set(scene.Reward, "m_loadedSprite", scene.RewardIcon); // Reflection: the sprite an earlier Addressables load would hold
            data.CollectableDefinitions[CollectableType.Orb] = scene.Reward;
            GameObject host = owned.Host("Lucid mission toggle", false);
            scene.Widget = host.AddComponent<UIWidgetToggleMission>();
            var transition = host.AddComponent<UITransitionAnimation>();
            Set(transition, "m_settings", new List<UITransitionAnimationSetting>()); // Reflection: authored (empty) transition settings
            Set(host.GetComponent<UIToggle>(), "m_transition", transition); // Reflection: serialized reference
            Set(scene.Widget, "m_toggle", host.GetComponent<UIToggle>()); // Reflection: the editor Reset would fill this serialized reference
            body(owned, scene);
        });

        private static UIWidgetToggleMissionParameters Parameters(bool complete = false, bool locked = false, bool unlockSeen = true,
            bool anchorRight = false, MissionIsNewState isNew = MissionIsNewState.None, UIShowType red = UIShowType.Unlocked,
            UIShowType blue = UIShowType.Locked, UIShowType jukebox = UIShowType.Hidden, Action<bool> callback = null) =>
            new UIWidgetToggleMissionParameters(complete, locked, unlockSeen, anchorRight, "Mission label", CollectableType.Orb, callback,
                isNew, red, blue, jukebox);

        // 0x6A4600 forwards the label and callback to the UIWidgetToggleParameters constructor (0x1A49F90) and stores the other nine
        // arguments in their own fields: the booleans at +0x20..+0x23, the states at +0x24..+0x30 and the reward type at +0x34.
        [Test]
        public void TheParametersKeepEachArgumentInItsOwnProperty()
        {
            Action<bool> callback = _ => { };
            var parameters = new UIWidgetToggleMissionParameters(true, false, true, false, "label", CollectableType.Orb, callback,
                MissionIsNewState.Seen, UIShowType.Locked, UIShowType.Unlocked, UIShowType.Hidden);
            Assert.That(parameters.Label, Is.EqualTo("label"));
            Assert.That(parameters.Callback, Is.SameAs(callback));
            Assert.That(parameters.Complete, Is.True);
            Assert.That(parameters.Locked, Is.False);
            Assert.That(parameters.UnlockSeen, Is.True);
            Assert.That(parameters.SubRewardsAnchorRight, Is.False);
            Assert.That(parameters.IsNewState, Is.EqualTo(MissionIsNewState.Seen));
            Assert.That(parameters.RedStarRingsState, Is.EqualTo(UIShowType.Locked));
            Assert.That(parameters.BlueCoinsState, Is.EqualTo(UIShowType.Unlocked));
            Assert.That(parameters.JukeboxState, Is.EqualTo(UIShowType.Hidden));
            Assert.That(parameters.RewardType, Is.EqualTo(CollectableType.Orb));
        }

        // The four booleans are packed into one register (0x6A4628..0x6A4644); a single true flag must land only in its own property.
        [TestCase(0, TestName = "OnlyTheCompleteFlagReachesItsProperty")]
        [TestCase(1, TestName = "OnlyTheLockedFlagReachesItsProperty")]
        [TestCase(2, TestName = "OnlyTheUnlockSeenFlagReachesItsProperty")]
        [TestCase(3, TestName = "OnlyTheSubRewardsAnchorFlagReachesItsProperty")]
        public void ASingleTrueFlagReachesOnlyItsOwnProperty(int index)
        {
            var parameters = new UIWidgetToggleMissionParameters(index == 0, index == 1, index == 2, index == 3, "label", CollectableType.Orb,
                null, MissionIsNewState.None, UIShowType.Hidden, UIShowType.Hidden, UIShowType.Hidden);
            Assert.That(new[] { parameters.Complete, parameters.Locked, parameters.UnlockSeen, parameters.SubRewardsAnchorRight },
                Is.EqualTo(new[] { index == 0, index == 1, index == 2, index == 3 }));
        }

        // Setup 0x6A3CC0: the base toggle setup first (label and callback), then every binding from the parameters. An unlocked mission
        // mirrors the three sub-reward states, and the rank icon starts hidden whatever it showed before.
        [Test]
        public void SetupPublishesTheStatesOfAnUnlockedMission()
        {
            Run((owned, scene) =>
            {
                scene.Widget.ShowRankIcon.Value = true;
                var received = new List<bool>();

                scene.Widget.Setup(Parameters(complete: true, anchorRight: true, isNew: MissionIsNewState.IsNew, red: UIShowType.Unlocked,
                    blue: UIShowType.Locked, jukebox: UIShowType.Hidden, callback: received.Add));

                Assert.That(scene.Widget.Label.Value, Is.EqualTo("Mission label"));
                Assert.That(scene.Widget.Complete.Value, Is.True);
                Assert.That(scene.Widget.IsNew.Value, Is.True);
                Assert.That(scene.Widget.ShowRankIcon.Value, Is.False);
                Assert.That(scene.Widget.SubRewardsAnchorRight.Value, Is.True);
                Assert.That(scene.Widget.RedStarRingsShowState.Value, Is.EqualTo(UIShowType.Unlocked));
                Assert.That(scene.Widget.BlueCoinsShowState.Value, Is.EqualTo(UIShowType.Locked));
                Assert.That(scene.Widget.JukeboxShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(scene.Widget.RewardImage.Value, Is.SameAs(scene.RewardIcon));

                scene.Widget.Action_OnValueChanged(true);
                Assert.That(received, Is.EqualTo(new[] { true }), "the stored callback is the one UIWidgetToggle.Setup took");
            });
        }

        // 0x6A3D9A..0x6A3E16: a locked mission shows Hidden (0x875AD1C4) for all three sub-rewards whatever the parameters say; the other
        // bindings are still published.
        [Test]
        public void ALockedMissionHidesItsSubRewardStates()
        {
            Run((owned, scene) =>
            {
                scene.Widget.Setup(Parameters(complete: true, locked: true, anchorRight: true, red: UIShowType.Unlocked,
                    blue: UIShowType.Unlocked, jukebox: UIShowType.Locked));

                Assert.That(scene.Widget.RedStarRingsShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(scene.Widget.BlueCoinsShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(scene.Widget.JukeboxShowState.Value, Is.EqualTo(UIShowType.Hidden));
                Assert.That(scene.Widget.Complete.Value, Is.True);
                Assert.That(scene.Widget.SubRewardsAnchorRight.Value, Is.True);
                Assert.That(scene.Widget.RewardImage.Value, Is.SameAs(scene.RewardIcon));
            });
        }

        // 0x6A3D5E: IsNew is the comparison with the single state 0xB7834D4B (IsNew), not a test of "anything but None".
        [TestCase(MissionIsNewState.None, false, TestName = "TheNoneStateDoesNotRaiseTheNewMarker")]
        [TestCase(MissionIsNewState.Seen, false, TestName = "TheSeenStateDoesNotRaiseTheNewMarker")]
        [TestCase(MissionIsNewState.IsNew, true, TestName = "TheIsNewStateRaisesTheNewMarker")]
        public void OnlyTheIsNewStateRaisesTheNewMarker(MissionIsNewState state, bool expected)
        {
            Run((owned, scene) =>
            {
                scene.Widget.Setup(Parameters(isNew: state));
                Assert.That(scene.Widget.IsNew.Value, Is.EqualTo(expected));
            });
        }

        // IsReadyToUnlock 0x6A3F20 is !Locked && !UnlockSeen (je at 0x6A3F2B on the locked test, sete at 0x6A3F35 on the seen test). The
        // locked, unseen row rejects a body that returns just !UnlockSeen.
        [TestCase(false, false, true, TestName = "AnUnlockedMissionThatIsNotSeenIsReadyToUnlock")]
        [TestCase(false, true, false, TestName = "AnUnlockedMissionThatIsSeenIsNotReadyToUnlock")]
        [TestCase(true, false, false, TestName = "ALockedMissionIsNotReadyToUnlock")]
        public void AMissionIsReadyToUnlockOnlyWhenItIsNeitherLockedNorSeen(bool locked, bool unlockSeen, bool expected)
        {
            Run((owned, scene) =>
            {
                Set(scene.Widget, "m_parameters", Parameters(locked: locked, unlockSeen: unlockSeen)); // Reflection: Setup would also load the reward icon
                Assert.That(Call(scene.Widget, "IsReadyToUnlock"), Is.EqualTo(expected));
            });
        }

        // 0x6A4050 and 0x6A3EE0 write their bindings directly.
        [Test]
        public void SetIsNewAndSetRankIconVisibilityWriteTheirBindings()
        {
            Run((owned, scene) =>
            {
                scene.Widget.SetIsNew(true);
                scene.Widget.SetRankIconVisibility(true);
                Assert.That(scene.Widget.IsNew.Value, Is.True);
                Assert.That(scene.Widget.ShowRankIcon.Value, Is.True);

                scene.Widget.SetIsNew(false);
                scene.Widget.SetRankIconVisibility(false);
                Assert.That(scene.Widget.IsNew.Value, Is.False);
                Assert.That(scene.Widget.ShowRankIcon.Value, Is.False);
            });
        }

        // 0x6A4070, 0x6A4120, 0x6A41D0: the asset held before is unloaded, the new one is stored and loaded into its own binding; a
        // null asset clears the binding. The base, background and foreground setters are separate bodies on separate fields.
        private static void AssertRankIconSlot(Func<UIWidgetToggleMission, Action<ManagedAddressableAsset<Sprite>>> setter,
            Func<UIWidgetToggleMission, Bindable<Sprite>> binding)
        {
            Run((owned, scene) =>
            {
                UIWidgetToggleMission widget = scene.Widget;
                ManagedAddressableAsset<Sprite> first = LoadedWrapper(owned, out Sprite firstSprite);
                ManagedAddressableAsset<Sprite> second = LoadedWrapper(owned, out Sprite secondSprite);

                setter(widget)(first);
                Assert.That(binding(widget).Value, Is.SameAs(firstSprite));
                Assert.That(Count(first), Is.EqualTo(1), "loaded once");

                setter(widget)(second);
                Assert.That(binding(widget).Value, Is.SameAs(secondSprite));
                Assert.That(Count(first), Is.EqualTo(0), "the replaced asset was unloaded");
                Assert.That(IsCached(first), Is.False);
                Assert.That(Count(second), Is.EqualTo(1));

                setter(widget)(null);
                Assert.That(binding(widget).Value, Is.Null);
                Assert.That(Count(second), Is.EqualTo(0), "clearing unloads the held asset");

                foreach (Bindable<Sprite> other in new[] { widget.RankIconBase, widget.RankIconBackground, widget.RankIconForeground })
                    if (!ReferenceEquals(other, binding(widget)))
                        Assert.That(other.Value, Is.Null, "the other rank icons were never written");
            });
        }

        [Test]
        public void TheBaseRankIconIsReplacedAndReleasedThroughItsOwnSlot() =>
            AssertRankIconSlot(widget => widget.SetRankIconBase, widget => widget.RankIconBase);

        [Test]
        public void TheBackgroundRankIconIsReplacedAndReleasedThroughItsOwnSlot() =>
            AssertRankIconSlot(widget => widget.SetRankIconBackground, widget => widget.RankIconBackground);

        [Test]
        public void TheForegroundRankIconIsReplacedAndReleasedThroughItsOwnSlot() =>
            AssertRankIconSlot(widget => widget.SetRankIconForeground, widget => widget.RankIconForeground);

        // OnDisable 0x6A3F60 unloads each rank icon that is set (the null ones are skipped) and then the reward icon Setup loaded.
        [Test]
        public void DisablingReleasesTheRankIconsAndTheRewardIcon()
        {
            Run((owned, scene) =>
            {
                scene.Widget.Setup(Parameters());
                Assert.That(Get<int>(scene.Reward, "m_refCount"), Is.EqualTo(1), "Setup loaded the reward icon");
                ManagedAddressableAsset<Sprite> background = LoadedWrapper(owned, out _);
                scene.Widget.SetRankIconBackground(background);

                Call(scene.Widget, "OnDisable");

                Assert.That(Count(background), Is.EqualTo(0));
                Assert.That(Get<int>(scene.Reward, "m_refCount"), Is.EqualTo(0));
                Assert.That(IsCached(background), Is.False);
            });
        }
    }
}
