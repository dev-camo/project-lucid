using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The four rewards that carry one authored definition: ChallengeRewardCharacterArchetype (x86-64
    // 0x54FA20..0x54FF80), ChallengeRewardDreamStoreBand (0x550080..0x550370), ChallengeRewardGameplayLevel
    // (0x5506A0..0x550A00) and ChallengeRewardSkin (0x5516C0..0x551A00). Each is built by its real constructor over
    // real definition assets, and the saving cases run over OwnedChallengeRewardServices (a real SaveManager with an
    // open save whose coroutine host refuses the save request, logging the engine error each request expects).
    public sealed class ChallengeDefinitionRewardTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly Regex DefinitionAwakeFault = new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        // dump.cs TypeDefIndex 1399, 1401, 1403 and 1407 with the DummyDll rows.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ChallengeRewardCharacterArchetype), 0x00100001,
                new (string, int)[]
                {
                    ("m_archetypeDefinition", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_RewardType", 0x08c6),
                    ("get_ArchetypeDefinition", 0x0886),
                    ("Validate", 0x00c6),
                    ("GetAnalyticsName", 0x00c6),
                    ("GetRewardGUID", 0x00c6),
                    ("GiveReward", 0x00c6),
                    ("GetRewardName", 0x00c6),
                    ("LoadImage", 0x00c6),
                    ("UnloadImage", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetMetaGameUnlock", 0x00c6),
                    (".ctor", 0x1886),
                });
            AssertMembers(typeof(ChallengeRewardDreamStoreBand), 0x00100001,
                new (string, int)[]
                {
                    ("m_rewardDefinition", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_RewardType", 0x08c6),
                    ("get_DreamPowerStoreBand", 0x0886),
                    ("Validate", 0x00c6),
                    ("GetAnalyticsName", 0x00c6),
                    ("GetRewardGUID", 0x00c6),
                    ("GiveReward", 0x00c6),
                    ("GetRewardName", 0x00c6),
                    ("LoadImage", 0x00c6),
                    ("UnloadImage", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetMetaGameUnlock", 0x00c6),
                    ("GetDefaultRewardSize", 0x00c4),
                    (".ctor", 0x1886),
                });
            AssertMembers(typeof(ChallengeRewardGameplayLevel), 0x00100001,
                new (string, int)[]
                {
                    ("m_reward", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_RewardType", 0x08c6),
                    ("get_LevelDefinition", 0x0886),
                    ("Validate", 0x00c6),
                    ("GetAnalyticsName", 0x00c6),
                    ("GetRewardGUID", 0x00c6),
                    ("GiveReward", 0x00c6),
                    ("GetRewardName", 0x00c6),
                    ("LoadImage", 0x00c6),
                    ("UnloadImage", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetMetaGameUnlock", 0x00c6),
                    ("GetZoneName", 0x0086),
                    ("GetDefaultRewardSize", 0x00c4),
                    (".ctor", 0x1886),
                });
            AssertMembers(typeof(ChallengeRewardSkin), 0x00100001,
                new (string, int)[]
                {
                    ("m_reward", 0x0001),
                },
                new (string, int)[]
                {
                    ("get_RewardType", 0x08c6),
                    ("get_Reward", 0x0886),
                    ("Validate", 0x00c6),
                    ("GetAnalyticsName", 0x00c6),
                    ("GetRewardGUID", 0x00c6),
                    ("GiveReward", 0x00c6),
                    ("GetRewardName", 0x00c6),
                    ("LoadImage", 0x00c6),
                    ("UnloadImage", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetMetaGameUnlock", 0x00c6),
                    (".ctor", 0x1886),
                });
            // Option 2 is ArrayBoundsChecks, 1 is NullChecks, in the DummyDll attribute order of each type.
            AssertIl2CppOptions(typeof(ChallengeRewardCharacterArchetype), 2, 1);
            AssertIl2CppOptions(typeof(ChallengeRewardDreamStoreBand), 1, 2);
            AssertIl2CppOptions(typeof(ChallengeRewardGameplayLevel), 2, 1);
            AssertIl2CppOptions(typeof(ChallengeRewardSkin), 2, 1);
            foreach (Type type in new[] { typeof(ChallengeRewardCharacterArchetype), typeof(ChallengeRewardDreamStoreBand),
                         typeof(ChallengeRewardGameplayLevel), typeof(ChallengeRewardSkin) })
                Assert.That(type.IsDefined(typeof(CreateAssetMenuAttribute), false), Is.True, type.Name);
        }

        // ---- ChallengeRewardCharacterArchetype ----

        // get_RewardType 0x54FA20 returns 5; get_ArchetypeDefinition 0x54FA30 the field (+0x28); GetRewardGUID 0x54FB10
        // the definition's GUID (+0x18); GetAnalyticsName 0x54FA50 formats "{0}_{1}" with the boxed reward type and the
        // archetype's identifier string (+0x20); Validate 0x54FA40 is a lone ret. The default size is the inherited Medium.
        [Test]
        public void ArchetypeRewardAnswersFromItsDefinition()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardCharacterArchetype>(owned);
                var definition = Create<CharacterArchetypeDefinition>(owned,
                    "{\"m_guid\":\"lucid-archetype-guid\",\"m_characterArchetype\":-825660132}");
                Set(reward, "m_archetypeDefinition", definition);
                Assert.That((int)reward.RewardType, Is.EqualTo(5));
                Assert.That(reward.ArchetypeDefinition, Is.SameAs(definition));
                Assert.That(reward.GetRewardGUID(), Is.EqualTo("lucid-archetype-guid"));
                Assert.That(reward.GetAnalyticsName(), Is.EqualTo("CharacterArchetype_Dashing"));
                JsonUtility.FromJsonOverwrite("{\"m_characterArchetype\":1160987322}", definition);
                Assert.That(reward.GetAnalyticsName(), Is.EqualTo("CharacterArchetype_Flying"));
                Assert.DoesNotThrow(() => reward.Validate(null));
                Assert.That((int)Call(reward, "GetDefaultRewardSize"), Is.EqualTo(-915112498), "the inherited Medium default");
            });
        }

        // LoadImage 0x54FDA0 tail-calls LoadAsync on the definition's Texture wrapper (+0x70) with the texture callback,
        // never touching the sprite callback or the definition's sprite wrappers; UnloadImage 0x54FDF0 tail-calls Unload.
        [Test]
        public void ArchetypeRewardLoadsTheDefinitionTextureThroughTheTextureCallback()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardCharacterArchetype>(owned);
                var definition = Create<CharacterArchetypeDefinition>(owned);
                Call(definition, "OnEnable");
                Set(reward, "m_archetypeDefinition", definition);
                Texture texture = owned.Track(new Texture2D(2, 2));
                Cache(definition.Texture, texture);
                var textures = new List<Texture>();
                var sprites = new List<Sprite>();
                reward.LoadImage(textures.Add, sprites.Add);
                Assert.That(textures, Is.EqualTo(new[] { texture }));
                Assert.That(sprites, Is.Empty);
                Assert.That(Count(definition.Texture), Is.EqualTo(1));
                Assert.That(Count(definition.UIImage), Is.EqualTo(0));
                Assert.That(Count(definition.AlternateImage), Is.EqualTo(0));
                Assert.That(Count(definition.SilhouetteImage), Is.EqualTo(0));
                reward.UnloadImage();
                Assert.That(Count(definition.Texture), Is.EqualTo(0));
                Assert.That(IsCached(definition.Texture), Is.False);
            });
        }

        // GiveReward 0x54FB20 calls SaveDataGame.UnlockCharacterArchetype on the open save for the definition's
        // archetype and nothing else: no reward record is collected and no save is requested.
        [Test]
        public void ArchetypeRewardGiveUnlocksOnlyThatArchetype()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                var reward = NewReward<ChallengeRewardCharacterArchetype>(owned);
                var definition = Create<CharacterArchetypeDefinition>(owned, "{\"m_guid\":\"lucid-archetype-guid\",\"m_characterArchetype\":-825660132}");
                Set(reward, "m_archetypeDefinition", definition);
                SaveDataChallengeReward record = services.AddRecord("lucid-track", "lucid-archetype-guid");
                reward.GiveReward();
                SaveDataGame save = services.Save.CurrentSave;
                Assert.That(save.GetOrCreateCharacterArchetypeData(CharacterArchetype.Dashing).Unlocked, Is.True);
                Assert.That(save.GetOrCreateCharacterArchetypeData(CharacterArchetype.Flying).Unlocked, Is.False);
                Assert.That(save.GetOrCreateCharacterArchetypeData(CharacterArchetype.Climbing).Unlocked, Is.False);
                Assert.That(record.Collected, Is.False, "giving does not collect the reward");
            });
        }

        // Save 0x54FE40 unlocks the archetype (GetOrCreateCharacterArchetypeData(...).Unlocked = true), finds the reward
        // record by the virtual reward GUID as a REWARD guid, marks it collected and requests a save. A record whose
        // track GUID equals that value is not the one.
        [Test]
        public void ArchetypeRewardSaveUnlocksAndCollectsByTheDefinitionGuid()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                var reward = NewReward<ChallengeRewardCharacterArchetype>(owned, "{\"m_guid\":\"lucid-reward-own-guid\"}");
                var definition = Create<CharacterArchetypeDefinition>(owned, "{\"m_guid\":\"lucid-archetype-guid\",\"m_characterArchetype\":2054273620}");
                Set(reward, "m_archetypeDefinition", definition);
                SaveDataChallengeReward own = services.AddRecord("lucid-archetype-track", "lucid-archetype-guid");
                SaveDataChallengeReward decoy = services.AddRecord("lucid-archetype-guid", "lucid-decoy-reward");
                SaveDataChallengeReward byOwnGuid = services.AddRecord("lucid-reward-own-guid", "lucid-reward-other");
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                reward.Save();
                SaveDataGame save = services.Save.CurrentSave;
                Assert.That(save.GetOrCreateCharacterArchetypeData(CharacterArchetype.Shadow).Unlocked, Is.True);
                Assert.That(save.GetOrCreateCharacterArchetypeData(CharacterArchetype.Dashing).Unlocked, Is.False);
                Assert.That(own.Collected, Is.True);
                Assert.That(decoy.Collected, Is.False, "not looked up by track GUID");
                Assert.That(byOwnGuid.Collected, Is.False, "not looked up by the reward's own GUID");
            });
        }

        // GetRewardName 0x54FBA0 walks the DataManager character dictionary (+0x50) in order and returns the name
        // (+0x40) of the first character whose archetype (+0x5C) equals the definition's (+0x20); with no match it
        // returns 0xb6fa1284 (Strings.NONE).
        [Test]
        public void ArchetypeRewardNameIsTheFirstCharacterOfThatArchetype()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                var reward = NewReward<ChallengeRewardCharacterArchetype>(owned);
                var definition = Create<CharacterArchetypeDefinition>(owned, "{\"m_characterArchetype\":-825660132}");
                Set(reward, "m_archetypeDefinition", definition);
                Assert.That(reward.GetRewardName(), Is.EqualTo(Strings.NONE), "no characters");

                CharacterDefinition flying = NewCharacter(owned, 1160987322, (int)Strings.MISSING);
                CharacterDefinition firstDashing = NewCharacter(owned, -825660132, (int)Strings.CULTURE_NUMBER_BILLIONS);
                CharacterDefinition secondDashing = NewCharacter(owned, -825660132, (int)Strings.DECIMAL_SEPARATOR);
                services.Data.Characters[CharacterId.Amy] = flying;
                services.Data.Characters[CharacterId.Cream] = firstDashing;
                services.Data.Characters[CharacterId.Sonic] = secondDashing;
                Assert.That(reward.GetRewardName(), Is.EqualTo(Strings.CULTURE_NUMBER_BILLIONS), "the first match in dictionary order");

                JsonUtility.FromJsonOverwrite("{\"m_characterArchetype\":1160987322}", definition);
                Assert.That(reward.GetRewardName(), Is.EqualTo(Strings.MISSING));
                JsonUtility.FromJsonOverwrite("{\"m_characterArchetype\":2054273620}", definition);
                Assert.That(reward.GetRewardName(), Is.EqualTo(Strings.NONE), "no character has that archetype");
                services.Data.Characters.Clear();
            });
        }

        private static CharacterDefinition NewCharacter(Owned owned, int archetype, int name)
        {
            // CreateInstance sends Awake, whose UpdateCachedValues reads the unassigned Settings.
            LogAssert.Expect(LogType.Exception, DefinitionAwakeFault);
            return Create<CharacterDefinition>(owned, "{\"m_archetype\":" + archetype + ",\"m_name\":" + name + "}");
        }

        // GetMetaGameUnlock 0x54FF20 builds MetaGameUnlockCharacterArchetype over the definition and ignores the track.
        [Test]
        public void ArchetypeRewardUnlockIsTheArchetypeUnlockOfItsDefinition()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardCharacterArchetype>(owned);
                var definition = Create<CharacterArchetypeDefinition>(owned, "{\"m_characterArchetype\":2054273620}");
                var widget = owned.Host("Lucid archetype widget", false).AddComponent<UIWidgetProgression>();
                Set(definition, "m_progressionUnlockWidget", widget);
                Set(reward, "m_archetypeDefinition", definition);
                foreach (RewardTrackType track in TrackTypes)
                {
                    IMetaGameUnlock unlock = reward.GetMetaGameUnlock(track);
                    Assert.That(unlock, Is.TypeOf<MetaGameUnlockCharacterArchetype>());
                    Assert.That(unlock.GetId(), Is.EqualTo("Shadow"));
                    Assert.That(unlock.GetWidget(), Is.SameAs(widget));
                }
            });
        }

        // ---- ChallengeRewardDreamStoreBand ----

        // get_RewardType 0x550080 returns 3; GetRewardGUID 0x550170 the band's GUID; GetAnalyticsName 0x5500B0 the boxed
        // reward type and the band type's identifier string (+0x20); GetRewardName 0x550190 returns the constant
        // 0xcfd0dbdb; GiveReward 0x550180 and Validate 0x5500A0 are lone rets; GetDefaultRewardSize 0x550360 returns
        // 0x51c014f1 (Large).
        [Test]
        public void BandRewardAnswersFromItsBand()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardDreamStoreBand>(owned);
                var band = Create<DreamPowerStoreBandDefinition>(owned, "{\"m_guid\":\"lucid-band-guid\",\"m_type\":511723480}");
                Set(reward, "m_rewardDefinition", band);
                Assert.That((int)reward.RewardType, Is.EqualTo(3));
                Assert.That(reward.DreamPowerStoreBand, Is.SameAs(band));
                Assert.That(reward.GetRewardGUID(), Is.EqualTo("lucid-band-guid"));
                Assert.That(reward.GetAnalyticsName(), Is.EqualTo("DreamPowerStoreBand_Band2"));
                JsonUtility.FromJsonOverwrite("{\"m_type\":1770485582}", band);
                Assert.That(reward.GetAnalyticsName(), Is.EqualTo("DreamPowerStoreBand_Band3"));
                Assert.That((int)reward.GetRewardName(), Is.EqualTo(-808395813), "0xcfd0dbdb, whatever the band");
                Assert.DoesNotThrow(reward.GiveReward);
                Assert.DoesNotThrow(() => reward.Validate(null));
                Assert.That((int)Call(reward, "GetDefaultRewardSize"), Is.EqualTo(1371542769), "0x51c014f1 Large");
            });
        }

        // LoadImage 0x5501A0 and UnloadImage 0x5501F0 use the band's ImageAsset wrapper (+0x48) and the sprite
        // callback; the locked-background wrapper (+0x50) is untouched.
        [Test]
        public void BandRewardLoadsTheBandImageThroughTheSpriteCallback()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardDreamStoreBand>(owned);
                var band = Create<DreamPowerStoreBandDefinition>(owned);
                Call(band, "OnEnable");
                Set(reward, "m_rewardDefinition", band);
                Sprite sprite = NewSprite(owned);
                Cache(band.ImageAsset, sprite);
                var textures = new List<Texture>();
                var sprites = new List<Sprite>();
                reward.LoadImage(textures.Add, sprites.Add);
                Assert.That(sprites, Is.EqualTo(new[] { sprite }));
                Assert.That(textures, Is.Empty);
                Assert.That(Count(band.ImageAsset), Is.EqualTo(1));
                Assert.That(Count(band.SlotLockedBackground), Is.EqualTo(0));
                reward.UnloadImage();
                Assert.That(Count(band.ImageAsset), Is.EqualTo(0));
                Assert.That(IsCached(band.ImageAsset), Is.False);
            });
        }

        // Save 0x550240 reaches the SaveManager through ProcessManager.GetSystem, finds the record by the virtual reward
        // GUID (the band's GUID) as a reward GUID, marks it collected and requests a save.
        [Test]
        public void BandRewardSaveCollectsTheRecordByTheBandGuid()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                var reward = NewReward<ChallengeRewardDreamStoreBand>(owned, "{\"m_guid\":\"lucid-reward-own-guid\"}");
                var band = Create<DreamPowerStoreBandDefinition>(owned, "{\"m_guid\":\"lucid-band-guid\"}");
                Set(reward, "m_rewardDefinition", band);
                SaveDataChallengeReward own = services.AddRecord("lucid-band-track", "lucid-band-guid");
                SaveDataChallengeReward decoy = services.AddRecord("lucid-band-guid", "lucid-decoy-reward");
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                reward.Save();
                Assert.That(own.Collected, Is.True);
                Assert.That(decoy.Collected, Is.False);
            });
        }

        // GetMetaGameUnlock 0x5502F0 builds MetaGameUnlockDreamPowerStoreBand over the band, the inherited XP threshold
        // (+0x20) and the requested track.
        [Test]
        public void BandRewardUnlockCarriesTheBandThresholdAndTrack()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardDreamStoreBand>(owned, "{\"m_xpThreshold\":420}");
                var band = Create<DreamPowerStoreBandDefinition>(owned);
                Set(reward, "m_rewardDefinition", band);
                foreach (RewardTrackType track in TrackTypes)
                {
                    var unlock = (MetaGameUnlockDreamPowerStoreBand)reward.GetMetaGameUnlock(track);
                    Assert.That(unlock.Definition, Is.SameAs(band));
                    Assert.That(unlock.ChallengeXP, Is.EqualTo(420));
                    Assert.That(unlock.RewardTrackType, Is.EqualTo(track));
                }
            });
        }

        // ---- ChallengeRewardGameplayLevel ----

        private static readonly FieldInfo CoreConfiguration =
            typeof(GameplayLevelDefinition).GetField("m_coreGameConfiguration", BindingFlags.Static | BindingFlags.NonPublic);

        // Runs body with the level scene-name accessor reading an owned CoreGameConfiguration, putting the prior
        // static back afterwards.
        private static void WithSceneNames(Owned owned, bool exported, Action body)
        {
            object prior = CoreConfiguration.GetValue(null);
            var configuration = Create<CoreGameConfiguration>(owned);
            Set(configuration, "m_useExportedScenes", exported);
            CoreConfiguration.SetValue(null, configuration);
            VerificationFixtureOwnership.RunThenCleanUp(body, () => CoreConfiguration.SetValue(null, prior));
        }

        // get_RewardType 0x5506A0 returns 6; get_LevelDefinition 0x5506B0 the field (+0x28); GetRewardGUID 0x550760 the
        // level's GUID (+0x18); GetRewardName 0x550810 the level's act name (+0x24, not the display name at +0x20);
        // GetAnalyticsName 0x5506D0 the boxed reward type and the level's SceneName accessor; GetDefaultRewardSize
        // 0x5509F0 returns 0x51c014f1 (Large).
        [Test]
        public void LevelRewardAnswersFromItsLevel()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardGameplayLevel>(owned);
                var level = Create<GameplayLevelDefinition>(owned, "{\"m_guid\":\"lucid-level-guid\",\"m_sceneName\":\"lucid-scene\"," +
                    "\"m_displayName\":" + (int)Strings.MISSING + ",\"m_actName\":" + (int)Strings.CULTURE_NUMBER_BILLIONS + "}");
                Set(reward, "m_reward", level);
                Assert.That((int)reward.RewardType, Is.EqualTo(6));
                Assert.That(reward.LevelDefinition, Is.SameAs(level));
                Assert.That(reward.GetRewardGUID(), Is.EqualTo("lucid-level-guid"));
                Assert.That(reward.GetRewardName(), Is.EqualTo(Strings.CULTURE_NUMBER_BILLIONS), "the act name");
                Assert.DoesNotThrow(() => reward.Validate(null));
                Assert.That((int)Call(reward, "GetDefaultRewardSize"), Is.EqualTo(1371542769), "0x51c014f1 Large");
                WithSceneNames(owned, false, () => Assert.That(reward.GetAnalyticsName(), Is.EqualTo("GameplayLevel_lucid-scene")));
                WithSceneNames(owned, true, () => Assert.That(reward.GetAnalyticsName(), Is.EqualTo("GameplayLevel_lucid-scene_exported"),
                    "the accessor, not the raw scene name"));
            });
        }

        // GiveReward 0x550770 (through the base's static SaveManager reference) and Save 0x550850 (through
        // ProcessManager.GetSystem) both mark collected the record whose TRACK guid equals this reward's own GUID
        // (+0x18), not the level's, and request a save.
        [Test]
        public void LevelRewardGiveAndSaveCollectTheRecordWithTheRewardOwnGuid()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                var reward = NewReward<ChallengeRewardGameplayLevel>(owned, "{\"m_guid\":\"lucid-level-reward\"}");
                var level = Create<GameplayLevelDefinition>(owned, "{\"m_guid\":\"lucid-level-guid\"}");
                Set(reward, "m_reward", level);
                SaveDataChallengeReward own = services.AddRecord("lucid-level-reward", "lucid-level-reward-guid");
                SaveDataChallengeReward byLevel = services.AddRecord("lucid-level-guid", "lucid-level-other");
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                reward.GiveReward();
                Assert.That(own.Collected, Is.True);
                Assert.That(byLevel.Collected, Is.False);

                // Save is observable separately once the flag is cleared.
                own.Collected = false;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                reward.Save();
                Assert.That(own.Collected, Is.True);
                Assert.That(byLevel.Collected, Is.False);
            });
        }

        // LoadImage 0x550820 tail-calls GameplayLevelDefinition.LoadLevelImage with the second (sprite) callback and
        // UnloadImage 0x550840 tail-calls UnloadLevelImage. Without a valid loading image key the level hands null to
        // the callback and the texture callback is never used.
        [Test]
        public void LevelRewardForwardsTheSpriteCallbackToTheLevelImageLoader()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardGameplayLevel>(owned);
                var level = Create<GameplayLevelDefinition>(owned);
                Set(level, "m_loadingImageReference", new UnityEngine.AddressableAssets.AssetReferenceT<Sprite>(string.Empty));
                Set(reward, "m_reward", level);
                var textures = new List<Texture>();
                int sprites = 0;
                Sprite received = NewSprite(owned);
                reward.LoadImage(textures.Add, loaded => { sprites++; received = loaded; });
                Assert.That(sprites, Is.EqualTo(1));
                Assert.That(received, Is.Null, "no valid key: the level answers null");
                Assert.That(textures, Is.Empty);
                Assert.DoesNotThrow(() => reward.LoadImage(textures.Add, null), "the level loader tolerates a null callback");

                // UnloadImage reaches the level's retained wrapper.
                var wrapper = new ManagedAddressableAsset<Sprite>(null);
                Cache(wrapper, NewSprite(owned));
                Set(wrapper, "m_refCount", 1);
                Set(level, "m_managedLoadingImage", wrapper);
                reward.UnloadImage();
                Assert.That(Count(wrapper), Is.EqualTo(0));
                Assert.That(IsCached(wrapper), Is.False);
            });
        }

        // GetMetaGameUnlock 0x5508F0 builds MetaGameUnlockLevel over the level and ignores the track; the unlock's
        // constructor takes the App, so the App row is read.
        [Test]
        public void LevelRewardUnlockIsTheLevelUnlock()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                var reward = NewReward<ChallengeRewardGameplayLevel>(owned);
                var level = Create<GameplayLevelDefinition>(owned, "{\"m_guid\":\"lucid-level-guid\"}");
                Set(reward, "m_reward", level);
                foreach (RewardTrackType track in TrackTypes)
                {
                    IMetaGameUnlock unlock = reward.GetMetaGameUnlock(track);
                    Assert.That(unlock, Is.TypeOf<MetaGameUnlockLevel>());
                    Assert.That(unlock.GetId(), Is.EqualTo("lucid-level-guid"));
                }
            });
        }

        // GetZoneName 0x550950 asks the LevelManager's GameLevels (+0xB8) for the zone of the level and returns that
        // zone's title (+0x20); a level in no zone yields 0xb6fa1284 (Strings.NONE).
        [Test]
        public void LevelRewardZoneNameIsTheTitleOfTheLevelZone()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                // LevelManager's field initialisers read these rows.
                owned.LeaseRow<MissionManager>();
                owned.LeaseRow<AudioManager>();
                var manager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
                owned.Register(manager);
                var levels = Create<GameLevels>(owned);
                Set(manager, "<GameLevels>k__BackingField", levels);

                var zone = Create<GameplayLevelCategory>(owned, "{\"m_guid\":\"lucid-zone\",\"m_title\":" + (int)Strings.CULTURE_NUMBER_BILLIONS + "}");
                FieldInfo entries = typeof(GameplayLevelCategory).GetField("m_gameplayEntries", BindingFlags.Instance | BindingFlags.NonPublic);
                if (entries.GetValue(zone) == null) entries.SetValue(zone, new List<GameplayLevelUIEntry>());
                var inZone = Create<GameplayLevelDefinition>(owned, "{\"m_guid\":\"lucid-zone-level\"}");
                var outside = Create<GameplayLevelDefinition>(owned, "{\"m_guid\":\"lucid-loose-level\"}");
                zone.GameplayEntries.Add(inZone);
                Get<List<GameplayLevelUIEntry>>(levels, "m_gameplayEntries").Add(zone);

                var reward = NewReward<ChallengeRewardGameplayLevel>(owned);
                Set(reward, "m_reward", inZone);
                Assert.That(reward.GetZoneName(), Is.EqualTo(Strings.CULTURE_NUMBER_BILLIONS));
                Set(reward, "m_reward", outside);
                Assert.That(reward.GetZoneName(), Is.EqualTo(Strings.NONE));
            });
        }

        // ---- ChallengeRewardSkin ----

        // get_RewardType 0x5516C0 returns 7; get_Reward 0x5516D0 the field; GetRewardGUID 0x5517B0 the skin's GUID;
        // GetAnalyticsName 0x5516F0 the boxed reward type and the skin's name string (+0x48); GetRewardName 0x5517D0
        // returns the constant 0x8c035df3 whatever the skin; GiveReward 0x5517C0 and Validate 0x5516E0 are lone rets.
        [Test]
        public void SkinRewardAnswersFromItsSkin()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardSkin>(owned);
                var skin = Create<CharacterSkinDefinition>(owned,
                    "{\"m_guid\":\"lucid-skin-guid\",\"m_name\":" + (int)Strings.CULTURE_NUMBER_BILLIONS + "}");
                Set(reward, "m_reward", skin);
                Assert.That((int)reward.RewardType, Is.EqualTo(7));
                Assert.That(reward.Reward, Is.SameAs(skin));
                Assert.That(reward.GetRewardGUID(), Is.EqualTo("lucid-skin-guid"));
                Assert.That(reward.GetAnalyticsName(), Is.EqualTo("Skin_CULTURE_NUMBER_BILLIONS"));
                Assert.That((int)reward.GetRewardName(), Is.EqualTo(-1945936397), "0x8c035df3, not the skin's name");
                Assert.DoesNotThrow(reward.GiveReward);
                Assert.DoesNotThrow(() => reward.Validate(null));
                Assert.That((int)Call(reward, "GetDefaultRewardSize"), Is.EqualTo(-915112498), "the inherited Medium default");
            });
        }

        // LoadImage 0x5517E0 and UnloadImage 0x551830 use the skin's Thumbnail wrapper (+0x68) and the sprite callback.
        [Test]
        public void SkinRewardLoadsTheSkinThumbnailThroughTheSpriteCallback()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardSkin>(owned);
                var skin = Create<CharacterSkinDefinition>(owned);
                Call(skin, "OnEnable");
                Set(reward, "m_reward", skin);
                Sprite sprite = NewSprite(owned);
                Cache(skin.Thumbnail, sprite);
                var textures = new List<Texture>();
                var sprites = new List<Sprite>();
                reward.LoadImage(textures.Add, sprites.Add);
                Assert.That(sprites, Is.EqualTo(new[] { sprite }));
                Assert.That(textures, Is.Empty);
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(1));
                reward.UnloadImage();
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(0));
                Assert.That(IsCached(skin.Thumbnail), Is.False);
            });
        }

        // Save 0x551880 unlocks the skin record named by the SKIN's GUID (+0x18 of the skin), marks collected the record
        // whose track GUID is this reward's own GUID, requests a save and finally looks up the ChallengeManager (whose
        // RefreshXPRewards it calls). Nothing registers one here, so the lookup is the original fault, reached only
        // after every earlier effect.
        [Test]
        public void SkinRewardSaveUnlocksCollectsAndThenReachesForTheChallengeManager()
        {
            Owned.Run(owned =>
            {
                var services = owned.Lease(new OwnedChallengeRewardServices());
                owned.LeaseRow<ChallengeManager>();
                var reward = NewReward<ChallengeRewardSkin>(owned, "{\"m_guid\":\"lucid-skin-reward\"}");
                var skin = Create<CharacterSkinDefinition>(owned, "{\"m_guid\":\"lucid-skin-guid\"}");
                Set(reward, "m_reward", skin);
                SaveDataChallengeReward own = services.AddRecord("lucid-skin-reward", "lucid-skin-reward-guid");
                SaveDataChallengeReward bySkin = services.AddRecord("lucid-skin-guid", "lucid-skin-other");
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                var fault = Assert.Throws<NullReferenceException>(reward.Save);
                Assert.That(fault.Message, Is.EqualTo("System 'HardlightProject.ChallengeManager' is null"));
                SaveDataGame save = services.Save.CurrentSave;
                Assert.That(save.GetOrCreateSkinData("lucid-skin-guid").Unlocked, Is.True);
                Assert.That(save.GetOrCreateSkinData("lucid-skin-reward").Unlocked, Is.False, "the skin is named by the skin's GUID");
                Assert.That(own.Collected, Is.True);
                Assert.That(bySkin.Collected, Is.False);
            });
        }

        // GetMetaGameUnlock 0x551990 builds MetaGameUnlockCharacterSkin over the skin, the inherited XP threshold and
        // the requested track.
        [Test]
        public void SkinRewardUnlockCarriesTheSkinThresholdAndTrack()
        {
            Owned.Run(owned =>
            {
                LeaseRewardRows(owned);
                var reward = NewReward<ChallengeRewardSkin>(owned, "{\"m_xpThreshold\":640}");
                var skin = Create<CharacterSkinDefinition>(owned);
                Set(reward, "m_reward", skin);
                foreach (RewardTrackType track in TrackTypes)
                {
                    var unlock = (MetaGameUnlockCharacterSkin)reward.GetMetaGameUnlock(track);
                    Assert.That(unlock.CharacterSkinDefinition, Is.SameAs(skin));
                    Assert.That(unlock.ChallengeXP, Is.EqualTo(640));
                    Assert.That(unlock.RewardTrackType, Is.EqualTo(track));
                }
            });
        }
    }
}
