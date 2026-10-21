using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.ProgressionContainerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerProgressionTask (Game.Runtime 0200090e, x86-64 0x674BD0..0x675E90) as a real component on an owned inactive
    // host over the world of LevelRequirementWorld (real App, DataManager, SaveManager, LevelManager with an authored
    // GameLevels), with real unlocks over real definition assets, an owned string table and the cached-asset path of the
    // asset wrappers (no Addressables). Awake is called directly.
    //
    // Not covered: the unload of a valid current level and the gameplay camera switch at the end of Awake, because they need
    // a level that is loaded (the unload is a coroutine on the process-wide host) and a CinemachineCameraManager.
    public sealed class UIContainerProgressionTaskTests
    {
        private static readonly Color DefaultGlow = new Color(0.125f, 0.25f, 0.5f, 0.75f);
        private static readonly Color TrackGlow = new Color(1f, 0.5f, 0.25f, 1f);

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string>
        {
            { Strings.MISSING, "Run {0}to {1}" },
            { Strings.CHALLENGES_LABEL_XP, "XP" },
            { Strings.ORNAMENT_CAT_STORY, "Achievement title" },
            { Strings.ORNAMENT_CAT_ACHIEVER, "Power name" },
        };

        private sealed class Rig
        {
            public Owned Owned;
            public LevelRequirementWorld World;
            public App App;
            public RewardTrackDefinition Track;
            public Sprite TrackIcon, MissionSprite, AchievementSprite, DreamPowerSprite;
        }

        private static void WithTask(Action<Rig> body) => LevelRequirementWorld.Run((owned, world) =>
        {
            // The container's field initialiser caches a reference from this row.
            owned.LeaseRow<CinemachineCameraManager>();
            var rig = new Rig { Owned = owned, World = world, App = ProcessManager.GetSystem<App>() };
            rig.App.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
            rig.MissionSprite = NewSprite(owned);
            rig.AchievementSprite = NewSprite(owned);
            rig.DreamPowerSprite = NewSprite(owned);
            // The reward track the challenge unlocks name by type.
            rig.Track = Create<RewardTrackDefinition>(owned,
                "{\"m_guid\":\"lucid-task-track\",\"m_type\":-1400767250,\"m_trackName\":" + (int)Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE +
                ",\"m_rewardGlowColour\":{\"r\":1,\"g\":0.5,\"b\":0.25,\"a\":1}}");
            Call(rig.Track, "OnEnable");
            rig.TrackIcon = NewSprite(owned);
            Cache(rig.Track.IconAsset, rig.TrackIcon);
            world.Data.RewardTrackDefinitions[RewardTrackType.Outfits] = rig.Track;
            OwnedStringTable.Run(Texts, () => body(rig));
        });

        private static UIContainerProgressionTask Show(Rig rig, IMetaGameUnlock unlock)
        {
            SetUnlocks(rig.App, unlock);
            var container = NewContainer<UIContainerProgressionTask>(rig.Owned);
            // Serialized authored values.
            Set(container, "m_defaultRewardGlowColour", DefaultGlow);
            Set(container, "m_missionSprite", rig.MissionSprite);
            Set(container, "m_achievementSprite", rig.AchievementSprite);
            Set(container, "m_dreamPowerSprite", rig.DreamPowerSprite);
            Call(container, "Awake");
            return container;
        }

        // What SetChallengeRewardDetails 0x675850 publishes for the Outfits track of the rig.
        private static void AssertChallengeDetails(Rig rig, UIContainerProgressionTask task, int xp)
        {
            Assert.That(task.ShowNextButton.Value, Is.True);
            Assert.That(task.ChallengeXP.Value, Is.EqualTo(xp));
            Assert.That(task.TaskType.Value, Is.EqualTo(Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE), "the track name (+0x40)");
            Assert.That(task.TaskName.Value, Is.EqualTo("XP"), "the XP label 0xc57c5a49");
            Assert.That(task.TaskImage.Value, Is.SameAs(rig.TrackIcon), "the track icon (wrapper +0xA0)");
            Assert.That(task.RewardGlowColour.Value, Is.EqualTo(TrackGlow), "the track glow (+0x30)");
            Assert.That(task.ShowChallengeData.Value, Is.True);
        }

        // dump.cs TypeDefIndex 2317 and the DummyDll rows (fields 0x04002524..0x0400253e, methods 0x06003456..0x06003469).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            var fields = new List<(string, int)>();
            foreach (string name in new[]
            {
                "TaskType", "TaskName", "ShowMissionData", "MissionZone", "MissionAct", "ShowChallengeData", "ChallengeXP",
                "TaskImage", "UnlockImage", "UnlockSprite", "DreamPowerImage", "ShowNextButton", "RewardGlowColour",
            })
                fields.Add((name, 0x0026));
            foreach (string name in new[]
            {
                "m_animationComponent", "m_revealAnimation", "m_pressedAnimation", "m_achievementSprite", "m_challengeSprite",
                "m_missionSprite", "m_dreamPowerSprite", "m_timeCategoryObject", "m_onRevealComplete", "m_defaultRewardGlowColour",
                "m_unlock",
            })
                fields.Add((name, 0x0001));
            foreach (string name in new[] { "m_levelManagerRef", "m_cinemachineManagerRef", "m_dataManagerRef" })
                fields.Add((name, 0x0021));
            AssertMembers(typeof(UIContainerProgressionTask), 0x00100001, fields.ToArray(),
                new (string, int)[]
                {
                    ("Awake", 0x0081), ("OnDestroy", 0x0081), ("OnRevealAnimationComplete", 0x0081), ("OnButtonPressed", 0x0086),
                    ("OnShownAnimationComplete", 0x0081),
                    ("ShowCompletedTaskDetails", 0x0081), ("ShowCompletedTaskDetails", 0x0081), ("ShowCompletedTaskDetails", 0x0081),
                    ("ShowCompletedTaskDetails", 0x0081), ("ShowCompletedTaskDetails", 0x0081), ("ShowCompletedTaskDetails", 0x0081),
                    ("ShowCompletedTaskDetails", 0x0081),
                    ("SetChallengeRewardDetails", 0x0081), (".ctor", 0x1886),
                    ("<ShowCompletedTaskDetails>b__32_0", 0x0081), ("<ShowCompletedTaskDetails>b__34_0", 0x0081),
                    ("<ShowCompletedTaskDetails>b__35_0", 0x0081), ("<ShowCompletedTaskDetails>b__36_0", 0x0081),
                    ("<ShowCompletedTaskDetails>b__37_0", 0x0081), ("<ShowCompletedTaskDetails>b__38_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerProgressionTask), 1, 2);
            Assert.That(typeof(UIContainerProgressionTask).BaseType, Is.EqualTo(typeof(UIContainer)));
            // The overloads take the seven unlock types in the order of the DummyDll rows 0x0600345b..0x06003461.
            Type[] overloads = typeof(UIContainerProgressionTask).GetMethods(Declared)
                .Where(method => method.Name == "ShowCompletedTaskDetails").OrderBy(method => method.MetadataToken)
                .Select(method => method.GetParameters()[0].ParameterType).ToArray();
            Assert.That(overloads, Is.EqualTo(new[]
            {
                typeof(MetaGameUnlockOrnament), typeof(MetaGameUnlockMusicTrack), typeof(MetaGameUnlockDreamPowerStoreBand),
                typeof(MetaGameUnlockBlueCoins), typeof(MetaGameUnlockDreamPowerStoreItem), typeof(MetaGameUnlockRewardTrack),
                typeof(MetaGameUnlockCharacterSkin),
            }));
        }

        // Awake 0x674BD0 returns when there is no pending unlock (0x674C78): the next button stays hidden.
        [Test]
        public void WithoutAPendingUnlockNothingIsPublished()
        {
            WithTask(rig =>
            {
                var container = NewContainer<UIContainerProgressionTask>(rig.Owned);
                Call(container, "Awake");
                Assert.That(container.ShowNextButton.Value, Is.False);
                Assert.That(container.RewardGlowColour.Value, Is.EqualTo(default(Color)));
            });
        }

        // With any pending unlock Awake shows the next button (0x674C8F) and the default glow (0x674CA1..0x674CBB) before the
        // type dispatch; an unlock kind the screen has no details for adds nothing (the dispatch falls through to 0x674DCB).
        [Test]
        public void AnUnlockWithoutTaskDetailsOnlyShowsTheNextButtonAndTheDefaultGlow()
        {
            WithTask(rig =>
            {
                var store = Create<DreamPowerStoreDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-store\"}");
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockDreamPowerStore(store));
                Assert.That(container.ShowNextButton.Value, Is.True);
                Assert.That(container.RewardGlowColour.Value, Is.EqualTo(DefaultGlow));
                Assert.That(container.TaskName.Value, Is.Null);
                Assert.That(container.ShowChallengeData.Value, Is.False);
                Assert.That(container.ShowMissionData.Value, Is.False);
            });
        }

        // 0x675340: the coin icon goes to UnlockSprite (callback 06003466), then the unlock's XP (+0x28) and track (+0x2c).
        [Test]
        public void BlueCoinsShowTheCoinIconAndTheChallengeDetails()
        {
            WithTask(rig =>
            {
                var coins = Create<CollectableDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-coins\"}");
                Call(coins, "OnEnable");
                Sprite icon = NewSprite(rig.Owned);
                Cache(coins.IconAsset, icon);
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockBlueCoins(coins, 250, 40, RewardTrackType.Outfits));
                Assert.That(container.UnlockSprite.Value, Is.SameAs(icon));
                AssertChallengeDetails(rig, container, 40);
                Call(container, "OnDestroy");
                Assert.That(Count(coins.IconAsset), Is.EqualTo(0), "the unlock's assets are released");
            });
        }

        // 0x6755B0: the skin thumbnail goes to UnlockSprite, then the unlock's XP (+0x20) and track (+0x24).
        [Test]
        public void ACharacterSkinShowsItsThumbnailAndTheChallengeDetails()
        {
            WithTask(rig =>
            {
                var skin = Create<CharacterSkinDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-skin\"}");
                Call(skin, "OnEnable");
                Sprite thumbnail = NewSprite(rig.Owned);
                Cache(skin.Thumbnail, thumbnail);
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockCharacterSkin(skin, 55, RewardTrackType.Outfits));
                Assert.That(container.UnlockSprite.Value, Is.SameAs(thumbnail));
                AssertChallengeDetails(rig, container, 55);
            });
        }

        // 0x675290: the band image goes to UnlockSprite, then the unlock's XP (+0x20) and track (+0x24).
        [Test]
        public void ABandShowsItsImageAndTheChallengeDetails()
        {
            WithTask(rig =>
            {
                var band = Create<DreamPowerStoreBandDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-band\"}");
                Call(band, "OnEnable");
                Sprite image = NewSprite(rig.Owned);
                Cache(band.ImageAsset, image);
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockDreamPowerStoreBand(band, 70, RewardTrackType.Outfits));
                Assert.That(container.UnlockSprite.Value, Is.SameAs(image));
                AssertChallengeDetails(rig, container, 70);
            });
        }

        // 0x675500: the unlocked track's own icon goes to UnlockSprite while the details come from the track named by the
        // unlock's track type (+0x18), after the unlock's XP (+0x1c).
        [Test]
        public void ARewardTrackShowsItsIconAndTheDetailsOfTheNamedTrack()
        {
            WithTask(rig =>
            {
                var unlocked = Create<RewardTrackDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-unlocked-track\"}");
                Call(unlocked, "OnEnable");
                Sprite icon = NewSprite(rig.Owned);
                Cache(unlocked.IconAsset, icon);
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockRewardTrack(unlocked, 90, RewardTrackType.Outfits));
                Assert.That(container.UnlockSprite.Value, Is.SameAs(icon));
                Assert.That(rig.TrackIcon, Is.Not.SameAs(icon));
                AssertChallengeDetails(rig, container, 90);
            });
        }

        // 0x6751B0: the image is the music texture asset when its reference is valid (IsValid 0x675208, Load 0x675224) and
        // otherwise the texture of the sprite asset (Load 0x67523A, Sprite.texture 0x675244); then the unlock's XP and track.
        [Test]
        public void AMusicTrackShowsTheSpriteTextureWhenThereIsNoTextureAsset()
        {
            WithTask(rig =>
            {
                var track = Create<MusicTrackDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-music\"}");
                Call(track, "OnEnable");
                Sprite sprite = NewSprite(rig.Owned);
                Cache(track.ImageAsset, sprite);
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockMusicTrack(track, 30, RewardTrackType.Outfits));
                Assert.That(container.UnlockImage.Value, Is.SameAs(sprite.texture));
                AssertChallengeDetails(rig, container, 30);
            });
        }

        [Test]
        public void AMusicTrackShowsItsTextureAssetWhenTheReferenceIsValid()
        {
            WithTask(rig =>
            {
                var track = Create<MusicTrackDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-music-texture\"}");
                // Reflection: the serialized reference has no setter; a 32-digit key is a valid runtime key.
                Set(track, "m_imageAsTexture", new AssetReferenceTexture("0123456789abcdef0123456789abcdef"));
                Call(track, "OnEnable");
                Assert.That(track.ImageAssetTexture.IsValid(), Is.True, "set-up: the texture reference is valid");
                Texture texture = rig.Owned.Track(new Texture2D(2, 2));
                Cache(track.ImageAssetTexture, texture);
                Sprite sprite = NewSprite(rig.Owned);
                Cache(track.ImageAsset, sprite);
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockMusicTrack(track, 31, RewardTrackType.Outfits));
                Assert.That(container.UnlockImage.Value, Is.SameAs(texture));
                Assert.That(Count(track.ImageAsset), Is.EqualTo(0), "the sprite asset is not loaded");
                AssertChallengeDetails(rig, container, 31);
            });
        }

        // 0x6753F0: the type is 0x2a5ca793 (the dream power store info header), the name is the localised title of the
        // power (GetTitle 0x675457 then StringTable.GetString), the icon goes to DreamPowerImage (callback 06003467) and
        // the image is the dream power sprite. No challenge details are shown and the glow stays the default.
        [Test]
        public void ADreamPowerShowsItsNameIconAndTheDreamPowerSprite()
        {
            WithTask(rig =>
            {
                var power = Create<DreamPowerDefinition>(rig.Owned,
                    "{\"m_guid\":\"lucid-task-power\",\"m_name\":" + (int)Strings.ORNAMENT_CAT_ACHIEVER + ",\"m_description\":" + (int)Strings.MISSING + "}");
                Call(power, "OnEnable");
                Sprite icon = NewSprite(rig.Owned);
                Cache(power.IconAsset, icon);
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockDreamPowerStoreItem(power));
                Assert.That(container.TaskType.Value, Is.EqualTo(Strings.MENU_DREAM_POWERS_STORE_INFO_HEADER));
                Assert.That(container.TaskName.Value, Is.EqualTo("Power name"));
                Assert.That(container.DreamPowerImage.Value, Is.SameAs(icon));
                Assert.That(container.TaskImage.Value, Is.SameAs(rig.DreamPowerSprite));
                Assert.That(container.ShowChallengeData.Value, Is.False);
                Assert.That(container.ChallengeXP.Value, Is.Zero);
                Assert.That(container.RewardGlowColour.Value, Is.EqualTo(DefaultGlow));
                Assert.That(container.ShowNextButton.Value, Is.True);
            });
        }

        // 0x674E50, mission branch: the type is 0xd3db7cd (MISSIONS) and the name the localised mission name (0x674F76,
        // GetString 0x674F9B); an instanced tracker formats it with an empty first argument and the objective count
        // (String.Format 0x674FDB, ToString 0x674FC4). The act comes from the mission's level (the DataManager lookup
        // +0x1c0, act name +0x24); with a zone for the level (TryGetZoneForLevel 0x675053) the zone title, the mission sprite
        // and the mission data flag follow, and without one they stay unset.
        [Test]
        public void AMissionOrnamentShowsTheMissionNameActZoneAndSprite()
        {
            WithTask(rig =>
            {
                var ornament = Create<OrnamentDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-ornament-mission\"}");
                Call(ornament, "OnEnable");
                Texture picture = rig.Owned.Track(new Texture2D(2, 2));
                Cache(ornament.ThumbnailAsset, picture);
                GameplayLevelDefinition level = rig.World.NewLevel("lucid-task-level", false);
                JsonUtility.FromJsonOverwrite("{\"m_actName\":" + (int)Strings.ORNAMENT_CAT_STORY + "}", level);
                GameplayLevelCategory zone = rig.World.AddZone("lucid-task-zone", level);
                JsonUtility.FromJsonOverwrite("{\"m_title\":" + (int)Strings.ORNAMENT_CAT_CHALLENGES + "}", zone);
                MissionDefinition plain = rig.World.NewMission("lucid-task-plain");
                JsonUtility.FromJsonOverwrite("{\"m_missionName\":" + (int)Strings.MISSING + "}", plain);
                rig.World.Data.MissionDefinitionLevelLookup[plain] = level;

                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockOrnament(ornament, plain));
                Assert.That(container.TaskType.Value, Is.EqualTo(Strings.MISSIONS));
                Assert.That(container.TaskName.Value, Is.EqualTo("Run {0}to {1}"), "a plain mission keeps the template");
                Assert.That(container.MissionAct.Value, Is.EqualTo(Strings.ORNAMENT_CAT_STORY));
                Assert.That(container.MissionZone.Value, Is.EqualTo(Strings.ORNAMENT_CAT_CHALLENGES));
                Assert.That(container.TaskImage.Value, Is.SameAs(rig.MissionSprite));
                Assert.That(container.ShowMissionData.Value, Is.True);
                Assert.That(container.UnlockImage.Value, Is.SameAs(picture), "the ornament thumbnail");
                Assert.That(container.ShowChallengeData.Value, Is.False);

                MissionDefinition counted = rig.World.NewMission("lucid-task-counted");
                JsonUtility.FromJsonOverwrite("{\"m_missionName\":" + (int)Strings.MISSING + ",\"m_isInstancedTracker\":true,\"m_totalObjectivesCount\":5}", counted);
                rig.World.Data.MissionDefinitionLevelLookup[counted] = level;
                UIContainerProgressionTask instanced = Show(rig, new MetaGameUnlockOrnament(ornament, counted));
                Assert.That(instanced.TaskName.Value, Is.EqualTo("Run to 5"), "{0} is empty and {1} is the objective count");

                GameplayLevelDefinition lonely = rig.World.NewLevel("lucid-task-lonely-level", false);
                JsonUtility.FromJsonOverwrite("{\"m_actName\":" + (int)Strings.MISSIONS + "}", lonely);
                rig.World.Data.MissionDefinitionLevelLookup[plain] = lonely;
                UIContainerProgressionTask unzoned = Show(rig, new MetaGameUnlockOrnament(ornament, plain));
                Assert.That(unzoned.MissionAct.Value, Is.EqualTo(Strings.MISSIONS));
                Assert.That(unzoned.MissionZone.Value, Is.EqualTo(default(Strings)));
                Assert.That(unzoned.TaskImage.Value == null, Is.True);
                Assert.That(unzoned.ShowMissionData.Value, Is.False);
            });
        }

        // 0x674E50, achievement branch (0x6750B4): the type is 0x71146f0d (the achievements label), the name is the localised
        // title of the achievement (DataManager lookup +0x1f8, title +0x1c) and the image the achievement sprite.
        [Test]
        public void AnAchievementOrnamentShowsTheAchievementTitleAndSprite()
        {
            WithTask(rig =>
            {
                var ornament = Create<OrnamentDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-ornament-achievement\"}");
                Call(ornament, "OnEnable");
                Cache(ornament.ThumbnailAsset, rig.Owned.Track(new Texture2D(2, 2)));
                rig.World.Data.AchievementDefinitions[AchievementIdentifier.A_New_Challenger] =
                    Create<AchievementDefinition>(rig.Owned, "{\"m_title\":" + (int)Strings.ORNAMENT_CAT_STORY + ",\"m_description\":" + (int)Strings.MISSING + "}");
                UIContainerProgressionTask container = Show(rig,
                    new MetaGameUnlockOrnament(ornament, AchievementIdentifier.A_New_Challenger));
                Assert.That(container.TaskType.Value, Is.EqualTo(Strings.SETTINGS_BUTTON_LABEL_ACHIEVEMENTS));
                Assert.That(container.TaskName.Value, Is.EqualTo("Achievement title"));
                Assert.That(container.TaskImage.Value, Is.SameAs(rig.AchievementSprite));
                Assert.That(container.ShowMissionData.Value, Is.False);
                Assert.That(container.ShowChallengeData.Value, Is.False);
            });
        }

        // 0x674E50, last branch (0x6750B4..0x675193): an ornament with neither mission nor achievement shows the challenge
        // details when its XP (+0x2c) is above zero (jle 0x675167) and nothing otherwise.
        [Test]
        public void AnOrnamentWithOnlyChallengeExperienceShowsTheChallengeDetails()
        {
            WithTask(rig =>
            {
                var ornament = Create<OrnamentDefinition>(rig.Owned, "{\"m_guid\":\"lucid-task-ornament-xp\"}");
                Call(ornament, "OnEnable");
                Cache(ornament.ThumbnailAsset, rig.Owned.Track(new Texture2D(2, 2)));
                UIContainerProgressionTask container = Show(rig, new MetaGameUnlockOrnament(ornament, 120, RewardTrackType.Outfits));
                AssertChallengeDetails(rig, container, 120);

                UIContainerProgressionTask none = Show(rig, new MetaGameUnlockOrnament(ornament, 0, RewardTrackType.Outfits));
                Assert.That(none.ShowChallengeData.Value, Is.False);
                Assert.That(none.ChallengeXP.Value, Is.Zero);
                Assert.That(none.TaskName.Value, Is.Null);
                Assert.That(none.ShowNextButton.Value, Is.True);

                UIContainerProgressionTask negative = Show(rig, new MetaGameUnlockOrnament(ornament, -5, RewardTrackType.Outfits));
                Assert.That(negative.ShowChallengeData.Value, Is.False, "the comparison is signed");
            });
        }

        // OnRevealAnimationComplete 0x674E40 is a lone ret.
        [Test]
        public void TheRevealCompletionDoesNothing()
        {
            WithTask(rig =>
            {
                var container = NewContainer<UIContainerProgressionTask>(rig.Owned);
                Assert.DoesNotThrow(() => Call(container, "OnRevealAnimationComplete"));
                Assert.That(rig.App.GetUIModernEvents(), Is.Empty);
            });
        }

        // OnShownAnimationComplete 0x6757D0 publishes the reveal event (AddUniqueUIModernEvent 0x530D60) once.
        [Test]
        public void TheShownAnimationPublishesTheRevealEventOnce()
        {
            WithTask(rig =>
            {
                var container = NewContainer<UIContainerProgressionTask>(rig.Owned);
                UIModernEvent reveal = NewEvent(rig.Owned);
                Set(container, "m_onRevealComplete", reveal);
                Call(container, "OnShownAnimationComplete");
                Call(container, "OnShownAnimationComplete");
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { reveal }));
            });
        }

        // OnButtonPressed 0x675710 hides the next button (0x675759) and starts the pressed-animation wait (WaitForAnimation
        // 0x6B87D0, StartCoroutine 0x26CE160) with OnShownAnimationComplete as its callback. Without an animation component
        // the wait completes in its first step, which StartCoroutine runs at once, so the reveal event follows.
        [Test]
        public void PressingTheButtonHidesItAndPublishesTheRevealEventWhenThereIsNoAnimation()
        {
            WithTask(rig =>
            {
                // An active host, so the coroutine can start; Awake and OnEnable still do not run in Edit Mode.
                var container = rig.Owned.Host("Lucid task active container", true).AddComponent<UIContainerProgressionTask>();
                UIModernEvent reveal = NewEvent(rig.Owned);
                Set(container, "m_onRevealComplete", reveal);
                container.ShowNextButton.Value = true;
                container.OnButtonPressed();
                Assert.That(container.ShowNextButton.Value, Is.False);
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { reveal }));
            });
        }
    }
}
