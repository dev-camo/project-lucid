using System.Linq;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.ProgressionContainerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The progression screens that show one pending unlock: UIContainerProgressionBlueCoins (Game.Runtime 02000907, x86-64
    // 0x673590..0x6737A0), ...CharacterSkin (02000908, 0x6737B0..0x6739FB), ...DreamPower (02000909, 0x673A00..0x673C3B),
    // ...DreamPowerStoreBand (0200090a, 0x673C40..0x673FDB), ...MusicTrack (0200090b, 0x673FE0..0x6742C8), ...Ornament
    // (0200090c, 0x6742D0..0x67477B) and ...RewardTrack (0200090d, 0x674780..0x674BCB). Each is a real component on an
    // owned inactive host over the real App of OwnedCharacterManagers, with a real unlock over real definition assets whose
    // wrappers are driven through their cached-asset path (no Addressables). Awake is called directly.
    //
    // Not covered: the MusicTrack Awake and its SetBindableFields, because the track number is read from the process-wide
    // MusicTrackManager, whose constructor subscribes to the save, level and mission managers; and the Ornament Awake with an
    // unlock, which hands the ornament to the viewer and so to Addressables.
    public sealed class UIContainerProgressionUnlockTests
    {
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");

        private static Sprite Cached(Owned owned, ManagedAddressableAsset<Sprite> wrapper)
        {
            Sprite sprite = NewSprite(owned);
            Cache(wrapper, sprite);
            return sprite;
        }

        // dump.cs TypeDefIndex 2310..2314 and the DummyDll rows (fields 0x040024f9..0x0400250d, methods 0x06003437..0x0600344b).
        [Test]
        public void TheSimpleContainersMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerProgressionBlueCoins), 0x00100001,
                new (string, int)[] { ("BlueCoinsAmount", 0x0026), ("m_unlock", 0x0001) },
                new (string, int)[] { ("Awake", 0x0081), ("OnDestroy", 0x0081), ("SetBindableFields", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerProgressionBlueCoins), 1, 2);

            AssertMembers(typeof(UIContainerProgressionCharacterSkin), 0x00100001,
                new (string, int)[] { ("Name", 0x0026), ("Description", 0x0026), ("Icon", 0x0026), ("m_unlock", 0x0001) },
                new (string, int)[] { ("Awake", 0x0081), ("OnDestroy", 0x0081), (".ctor", 0x1886), ("<Awake>b__4_0", 0x0081) });
            AssertIl2CppOptions(typeof(UIContainerProgressionCharacterSkin), 1, 2);

            AssertMembers(typeof(UIContainerProgressionDreamPower), 0x00100001,
                new (string, int)[] { ("Name", 0x0026), ("Description", 0x0026), ("Icon", 0x0026), ("m_unlock", 0x0001) },
                new (string, int)[] { ("Awake", 0x0081), ("OnDestroy", 0x0081), (".ctor", 0x1886), ("<Awake>b__4_0", 0x0081) });
            AssertIl2CppOptions(typeof(UIContainerProgressionDreamPower), 1, 2);

            AssertMembers(typeof(UIContainerProgressionDreamPowerStoreBand), 0x00100001,
                new (string, int)[]
                {
                    ("Title", 0x0026), ("Body", 0x0026), ("Image", 0x0026), ("TierIndex", 0x0026),
                    ("m_dataManagerRef", 0x0021), ("m_unlock", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081), ("OnDestroy", 0x0081), ("SetBindableFields", 0x0081), (".ctor", 0x1886),
                    ("<SetBindableFields>b__8_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerProgressionDreamPowerStoreBand), 1, 2);

            AssertMembers(typeof(UIContainerProgressionMusicTrack), 0x00100001,
                new (string, int)[]
                {
                    ("TrackImage", 0x0026), ("TrackNumber", 0x0026), ("TrackName", 0x0026), ("TrackComposer", 0x0026),
                    ("m_unlock", 0x0001),
                },
                new (string, int)[] { ("Awake", 0x0081), ("OnDestroy", 0x0081), ("SetBindableFields", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerProgressionMusicTrack), 2, 1);
            foreach (System.Type type in new[]
            {
                typeof(UIContainerProgressionBlueCoins), typeof(UIContainerProgressionCharacterSkin),
                typeof(UIContainerProgressionDreamPower), typeof(UIContainerProgressionDreamPowerStoreBand),
                typeof(UIContainerProgressionMusicTrack),
            })
                Assert.That(type.BaseType, Is.EqualTo(typeof(UIContainer)), type.Name);
        }

        // dump.cs TypeDefIndex 2315/2316 and the DummyDll rows (fields 0x0400250e..0x04002523, methods 0x0600344c..0x06003455).
        [Test]
        public void TheOrnamentAndRewardTrackContainersMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerProgressionOrnament), 0x00100001,
                new (string, int)[]
                {
                    ("m_ornamentViewer", 0x0001), ("m_ornamentAnimation", 0x0001), ("m_timeCategoryObject", 0x0001),
                    ("m_achievementSprite", 0x0001), ("m_challengeSprite", 0x0001), ("m_missionSprite", 0x0001),
                    ("Name", 0x0026), ("Description", 0x0026), ("Category", 0x0026), ("CategoryImage", 0x0026),
                    ("ShowNextButton", 0x0026), ("m_unlock", 0x0001), ("m_ornamentViewerInstance", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081), ("OnDestroy", 0x0081), ("OnOrnamentLoaded", 0x0081), ("OnAnimationComplete", 0x0081),
                    ("SetBindableFields", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerProgressionOrnament), 2, 1);
            Assert.That(typeof(UIContainerProgressionOrnament).GetField("m_ornamentViewer", Declared).GetCustomAttributesData()
                .Select(data => data.AttributeType), Is.EqualTo(new[] { typeof(SerializeField) }));

            AssertMembers(typeof(UIContainerProgressionRewardTrack), 0x00100001,
                new (string, int)[]
                {
                    ("Name", 0x0026), ("Description", 0x0026), ("Overview", 0x0026), ("Icon", 0x0026), ("Thumbnail", 0x0026),
                    ("BackgroundGradientStart", 0x0026), ("BackgroundGradientEnd", 0x0026), ("ShineColor", 0x0026),
                    ("m_unlock", 0x0001),
                },
                new (string, int)[] { ("Awake", 0x0081), ("OnDestroy", 0x0081), (".ctor", 0x1886), ("<Awake>b__9_0", 0x0081) });
            AssertIl2CppOptions(typeof(UIContainerProgressionRewardTrack), 2, 1);
            Assert.That(typeof(UIContainerProgressionOrnament).BaseType, Is.EqualTo(typeof(UIContainer)));
            Assert.That(typeof(UIContainerProgressionRewardTrack).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // Awake 0x673590 reads the typed current unlock (0x6735D0) and SetBindableFields 0x673670 publishes
        // String.Format("+{0}", amount) (0x6736C7..0x6736D6) from the unlock's amount (+0x18).
        [Test]
        public void BlueCoinsShowTheAmountWithAPlusSign()
        {
            WithApp((owned, app) =>
            {
                var definition = Create<CollectableDefinition>(owned, "{\"m_guid\":\"lucid-blue-coins\"}");
                Call(definition, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                foreach ((int amount, string text) in new[] { (250, "+250"), (0, "+0"), (1234567, "+1234567") })
                {
                    SetUnlocks(app, new MetaGameUnlockBlueCoins(definition, amount, 40, RewardTrackType.Adventure));
                    var container = NewContainer<UIContainerProgressionBlueCoins>(owned);
                    Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                    Assert.That(container.BlueCoinsAmount.Value, Is.EqualTo(text));
                }
            });
        }

        // Without a blue coins unlock (0x6735D7 jumps to the end) nothing is published, and OnDestroy 0x673700 tests the
        // stored unlock (0x673708) before releasing it. A pending unlock of another kind is skipped over.
        [Test]
        public void BlueCoinsWithoutTheirUnlockPublishNothing()
        {
            WithApp((owned, app) =>
            {
                var skin = Create<CharacterSkinDefinition>(owned, "{\"m_guid\":\"lucid-other-skin\"}");
                SetUnlocks(app, new MetaGameUnlockCharacterSkin(skin, 1, RewardTrackType.Adventure));
                var container = NewContainer<UIContainerProgressionBlueCoins>(owned);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(container.BlueCoinsAmount.Value, Is.Null);
                Assert.DoesNotThrow(() => Call(container, "OnDestroy")); // Reflection: Unity sends OnDestroy; Edit Mode does not
            });
        }

        // OnDestroy 0x673700 tail-calls the unlock's ReleaseAssets (vtable +0x298), which unloads the coin icon.
        [Test]
        public void BlueCoinsReleaseTheCoinIconWhenDestroyed()
        {
            WithApp((owned, app) =>
            {
                var definition = Create<CollectableDefinition>(owned, "{\"m_guid\":\"lucid-blue-coins-icon\"}");
                Call(definition, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Cache(definition.IconAsset, NewSprite(owned));
                var unlock = new MetaGameUnlockBlueCoins(definition, 5, 0, RewardTrackType.Adventure);
                unlock.GetCustomSprite(sprite => { });
                Assert.That(Count(definition.IconAsset), Is.EqualTo(1), "set-up: the icon is held once");
                SetUnlocks(app, unlock);
                var container = NewContainer<UIContainerProgressionBlueCoins>(owned);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Assert.That(Count(definition.IconAsset), Is.EqualTo(0));
                Assert.That(IsCached(definition.IconAsset), Is.False);
            });
        }

        // Awake 0x6737B0 publishes the skin's own name (unlock +0x18 -> definition +0x48, 0x673829..0x673831) as the name,
        // but the unlock's BODY text (MetaGameUnlockBase.GetBody, 0x67384E) as the description, and hands the sprite
        // callback to GetCustomSprite (0x6738A5); OnDestroy 0x6738C0 releases the unlock's assets.
        [Test]
        public void ACharacterSkinShowsItsOwnNameAndTheUnlockBodyAndThumbnail()
        {
            WithApp((owned, app) =>
            {
                var skin = Create<CharacterSkinDefinition>(owned, "{\"m_guid\":\"lucid-skin\",\"m_name\":" + (int)Strings.MISSIONS + "}");
                Call(skin, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Sprite thumbnail = Cached(owned, skin.Thumbnail);
                var unlockText = Create<MetaGameUnlockDefinition>(owned,
                    "{\"m_title\":" + (int)Strings.MISSING + ",\"m_body\":" + (int)Strings.CHALLENGES_LABEL_XP + "}");
                ProcessManager.GetSystem<DataManager>().MetaGameUnlockDefinitions[PlayerProgressionTypes.CharacterSkin] = unlockText;
                SetUnlocks(app, new MetaGameUnlockCharacterSkin(skin, 10, RewardTrackType.Outfits));
                var container = NewContainer<UIContainerProgressionCharacterSkin>(owned);

                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(container.Name.Value, Is.EqualTo(Strings.MISSIONS), "the skin's name, not the unlock title");
                Assert.That(container.Description.Value, Is.EqualTo(Strings.CHALLENGES_LABEL_XP), "the unlock body");
                Assert.That(container.Icon.Value, Is.SameAs(thumbnail));
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(1));
                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(0));
            });
        }

        // Awake 0x673A00 asks the dream power unlock for its title (0x673A7F, the power's name) and body (0x673AA3, its
        // description) and loads the icon through GetCustomSprite (0x673AF2); OnDestroy 0x673B10 releases the icon.
        [Test]
        public void ADreamPowerShowsItsNameDescriptionAndIcon()
        {
            WithApp((owned, app) =>
            {
                var power = Create<DreamPowerDefinition>(owned,
                    "{\"m_guid\":\"lucid-power\",\"m_name\":" + (int)Strings.MISSIONS + ",\"m_description\":" + (int)Strings.MISSING + "}");
                Call(power, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Sprite icon = Cached(owned, power.IconAsset);
                SetUnlocks(app, new MetaGameUnlockDreamPowerStoreItem(power));
                var container = NewContainer<UIContainerProgressionDreamPower>(owned);

                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(container.Name.Value, Is.EqualTo(Strings.MISSIONS));
                Assert.That(container.Description.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(container.Icon.Value, Is.SameAs(icon));
                Assert.That(Count(power.IconAsset), Is.EqualTo(1));
                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Assert.That(Count(power.IconAsset), Is.EqualTo(0));
            });
        }

        // SetBindableFields 0x673CB0 publishes the literal 0x47c94108 (the dream power collections title) as the title, the
        // unlock body (MetaGameUnlockBase.GetBody, 0x673D20), the one-based index of the band among the band group's
        // elements (GetIndex 0x673D6C, inc 0x673D75, Int32.ToString 0x673D80) and the band image (GetCustomSprite 0x673DD8).
        [Test]
        public void ABandShowsTheFixedTitleItsBodyItsTierAndItsImage()
        {
            WithApp((owned, app) =>
            {
                var band = Create<DreamPowerStoreBandDefinition>(owned, "{\"m_guid\":\"lucid-band\"}");
                var first = Create<DreamPowerStoreBandDefinition>(owned, "{\"m_guid\":\"lucid-band-first\"}");
                Call(band, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Call(first, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Sprite bandImage = Cached(owned, band.ImageAsset);
                Sprite firstImage = Cached(owned, first.ImageAsset);

                var group = Create<DreamPowerStoreBandDefinitionGroup>(owned);
                group.m_elements = new[] { first, band }
                    .Select(definition => new DefinitionDataType<DreamPowerStoreBandType, DreamPowerStoreBandDefinition>
                        .DefinitionElement<DreamPowerStoreBandDefinition>(definition)).ToArray();
                var definitions = Create<DataDefinitions>(owned);
                definitions.DataDefinitionsArray = new DefinitionDataType[] { group };
                DataManager data = ProcessManager.GetSystem<DataManager>();
                // Reflection: the group table is filled by the Addressables load of the definition data, which has no
                // other route here. The DataManager belongs to the fixture and is destroyed with it.
                Set(data, "m_dataDefinitions", definitions);
                // Neither band has an authored type, so both unlocks report DreamPowerStoreBand1.
                data.MetaGameUnlockDefinitions[PlayerProgressionTypes.DreamPowerStoreBand1] =
                    Create<MetaGameUnlockDefinition>(owned, "{\"m_body\":" + (int)Strings.CHALLENGES_LABEL_XP + "}");

                SetUnlocks(app, new MetaGameUnlockDreamPowerStoreBand(band, 100, RewardTrackType.Adventure));
                var second = NewContainer<UIContainerProgressionDreamPowerStoreBand>(owned);
                Call(second, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(second.Title.Value, Is.EqualTo(Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE));
                Assert.That(second.Body.Value, Is.EqualTo(Strings.CHALLENGES_LABEL_XP));
                Assert.That(second.TierIndex.Value, Is.EqualTo("2"), "the band is the second element");
                Assert.That(second.Image.Value, Is.SameAs(bandImage));

                SetUnlocks(app, new MetaGameUnlockDreamPowerStoreBand(first, 100, RewardTrackType.Adventure));
                var firstContainer = NewContainer<UIContainerProgressionDreamPowerStoreBand>(owned);
                Call(firstContainer, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(firstContainer.TierIndex.Value, Is.EqualTo("1"), "the first element is tier one");
                Assert.That(firstContainer.Image.Value, Is.SameAs(firstImage));
                Call(second, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Assert.That(Count(band.ImageAsset), Is.EqualTo(0));
            });
        }

        // OnDestroy 0x674160 releases the unlock's track image (the texture wrapper at definition +0x60). The track
        // unlock reaches the container only through Awake, which needs the MusicTrackManager (see the class comment), so the
        // stored unlock is written directly.
        [Test]
        public void AMusicTrackReleasesItsImageWhenDestroyed()
        {
            WithApp((owned, app) =>
            {
                var track = Create<MusicTrackDefinition>(owned, "{\"m_guid\":\"lucid-track\"}");
                Call(track, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Cache(track.ImageAssetTexture, owned.Track(new Texture2D(2, 2)));
                var unlock = new MetaGameUnlockMusicTrack(track, 5, RewardTrackType.Adventure);
                unlock.GetCustomTexture(texture => { });
                Assert.That(Count(track.ImageAssetTexture), Is.EqualTo(1), "set-up: the image is held once");
                var container = NewContainer<UIContainerProgressionMusicTrack>(owned);
                Set(container, "m_unlock", unlock); // Reflection: private unlock the container keeps from its setup
                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Assert.That(Count(track.ImageAssetTexture), Is.EqualTo(0));
                Assert.That(IsCached(track.ImageAssetTexture), Is.False);
            });
        }

        // Without a music track unlock Awake 0x673FE0 does nothing (0x674022) and OnDestroy 0x674160 tests the stored unlock.
        [Test]
        public void AMusicTrackWithoutItsUnlockDoesNothing()
        {
            WithApp((owned, app) =>
            {
                var container = NewContainer<UIContainerProgressionMusicTrack>(owned);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(container.TrackImage.Value == null, Is.True);
                Assert.That(container.TrackNumber.Value, Is.Zero);
                Assert.DoesNotThrow(() => Call(container, "OnDestroy")); // Reflection: Unity sends OnDestroy; Edit Mode does not
            });
        }

        // SetBindableFields 0x674400 maps the ornament category with the keys 0xe3fd3977 (Story -> 0xca40210d
        // ORNAMENT_CAT_STORY), 0xe48f12e6 (Achievement -> 0x1edcf46a ORNAMENT_CAT_ACHIEVER) and 0x6c346841 (Challenge ->
        // 0x5cda3096 ORNAMENT_CAT_CHALLENGES), and the category image with its own comparison order: Story selects the
        // mission sprite (+0x60), Challenge the challenge sprite (+0x58), Achievement the achievement sprite (+0x50).
        // Any other category value keeps the current category text (get_Value 0x67448d, set_Value 0x6744ac) and clears the
        // image. The name and lore come from +0x38 and +0x3c of the ornament definition.
        [Test]
        public void TheOrnamentCategorySelectsTheCategoryTextAndTheCategorySprite()
        {
            WithApp((owned, app) =>
            {
                Sprite achievement = NewSprite(owned), challenge = NewSprite(owned), mission = NewSprite(owned);
                var table = new[]
                {
                    (category: -469943945, text: Strings.ORNAMENT_CAT_STORY, sprite: mission),            // Story
                    (category: -460385562, text: Strings.ORNAMENT_CAT_ACHIEVER, sprite: achievement),     // Achievement
                    (category: 1815373889, text: Strings.ORNAMENT_CAT_CHALLENGES, sprite: challenge),     // Challenge
                    (category: 12345, text: Strings.MISSING, sprite: (Sprite)null),                       // undefined keeps the text
                };
                foreach (var row in table)
                {
                    var ornament = Create<OrnamentDefinition>(owned,
                        "{\"m_guid\":\"lucid-ornament-" + row.category + "\",\"m_category\":" + row.category +
                        ",\"m_localisedName\":" + (int)Strings.MISSIONS + ",\"m_localisedUnlockedLore\":" + (int)Strings.CHALLENGES_LABEL_XP + "}");
                    var container = NewContainer<UIContainerProgressionOrnament>(owned);
                    Set(container, "m_achievementSprite", achievement); // Reflection: serialized m_achievementSprite the editor assigns
                    Set(container, "m_challengeSprite", challenge); // Reflection: serialized m_challengeSprite the editor assigns
                    Set(container, "m_missionSprite", mission); // Reflection: serialized m_missionSprite the editor assigns
                    container.Category.Value = Strings.MISSING;
                    container.CategoryImage.Value = achievement;
                    // Reflection: the unlock reaches the container through Awake, which hands the ornament to the viewer
                    // and so to Addressables.
                    Set(container, "m_unlock", new MetaGameUnlockOrnament(ornament, AchievementIdentifier.None));
                    Call(container, "SetBindableFields"); // Reflection: private SetBindableFields, which the original reaches from its own code
                    Assert.That(container.Name.Value, Is.EqualTo(Strings.MISSIONS), "category " + row.category);
                    Assert.That(container.Description.Value, Is.EqualTo(Strings.CHALLENGES_LABEL_XP), "category " + row.category);
                    Assert.That(container.Category.Value, Is.EqualTo(row.text), "category " + row.category);
                    Assert.That(container.CategoryImage.Value == row.sprite, Is.True, "category " + row.category);
                }
            });
        }

        // Awake 0x6742D0 with no ornament unlock shows the next button at once (0x6743A2); OnAnimationComplete 0x6743D0 does
        // the same when the ornament animation has finished.
        [Test]
        public void TheNextButtonIsShownWithoutAnUnlockAndWhenTheAnimationCompletes()
        {
            WithApp((owned, app) =>
            {
                var container = NewContainer<UIContainerProgressionOrnament>(owned);
                Assert.That(container.ShowNextButton.Value, Is.False);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(container.ShowNextButton.Value, Is.True);

                var other = NewContainer<UIContainerProgressionOrnament>(owned);
                Call(other, "OnAnimationComplete"); // Reflection: private OnAnimationComplete, which the original reaches from its own code
                Assert.That(other.ShowNextButton.Value, Is.True);
            });
        }

        // OnDestroy 0x674510 removes the stored unlock from the App's pending list (RemoveUnlock 0x6B88B0) and then destroys
        // the viewer instance (Object.Destroy 0x26D1F40) without a null test; Edit Mode refuses the destroy.
        [Test]
        public void AnOrnamentIsRemovedFromThePendingUnlocksAndItsViewerDestroyed()
        {
            WithApp((owned, app) =>
            {
                var ornament = Create<OrnamentDefinition>(owned, "{\"m_guid\":\"lucid-ornament-pending\"}");
                var unlock = new MetaGameUnlockOrnament(ornament, AchievementIdentifier.None);
                var other = new MetaGameUnlockOrnament(ornament, AchievementIdentifier.None);
                SetUnlocks(app, unlock, other);
                var container = NewContainer<UIContainerProgressionOrnament>(owned);
                GameObject viewer = owned.Host("Lucid ornament viewer instance", false);
                // Reflection: the unlock and the instance are filled by Awake and the viewer, which need Addressables.
                Set(container, "m_unlock", unlock);
                Set(container, "m_ornamentViewerInstance", viewer);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Assert.That(app.Storage.GetValue<System.Collections.Generic.List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, false),
                    Is.EqualTo(new IMetaGameUnlock[] { other }));
            });
        }

        // Awake 0x674780 publishes the track's name (+0x40), description (+0x44) and overview (+0x48), the icon through
        // GetCustomSprite (0x67489B), the thumbnail loaded from the wrapper at +0xA8 (0x6748BE) and the three colours from
        // +0x4C, +0x5C and +0x6C; OnDestroy 0x674960 releases the unlock's icon and then the thumbnail.
        [Test]
        public void ARewardTrackShowsItsTextsIconThumbnailAndColours()
        {
            WithApp((owned, app) =>
            {
                var track = Create<RewardTrackDefinition>(owned,
                    "{\"m_guid\":\"lucid-reward-track\",\"m_type\":-1400767250" +
                    ",\"m_trackName\":" + (int)Strings.MISSIONS + ",\"m_trackDescription\":" + (int)Strings.CHALLENGES_LABEL_XP +
                    ",\"m_trackOverview\":" + (int)Strings.MISSING +
                    ",\"m_backgroundGradientStart\":{\"r\":0.25,\"g\":0.5,\"b\":0.75,\"a\":1}" +
                    ",\"m_backgroundGradientEnd\":{\"r\":1,\"g\":0.5,\"b\":0.25,\"a\":0.5}" +
                    ",\"m_shineColour\":{\"r\":0.5,\"g\":0.25,\"b\":0.125,\"a\":0.75}}");
                Call(track, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Sprite icon = Cached(owned, track.IconAsset);
                Sprite thumbnail = Cached(owned, track.ThumbnailAsset);
                SetUnlocks(app, new MetaGameUnlockRewardTrack(track, 10, RewardTrackType.Outfits));
                var container = NewContainer<UIContainerProgressionRewardTrack>(owned);

                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(container.Name.Value, Is.EqualTo(Strings.MISSIONS));
                Assert.That(container.Description.Value, Is.EqualTo(Strings.CHALLENGES_LABEL_XP));
                Assert.That(container.Overview.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(container.Icon.Value, Is.SameAs(icon));
                Assert.That(container.Thumbnail.Value, Is.SameAs(thumbnail));
                Assert.That(container.BackgroundGradientStart.Value, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
                Assert.That(container.BackgroundGradientEnd.Value, Is.EqualTo(new Color(1f, 0.5f, 0.25f, 0.5f)));
                Assert.That(container.ShineColor.Value, Is.EqualTo(new Color(0.5f, 0.25f, 0.125f, 0.75f)));
                Assert.That(Count(track.IconAsset), Is.EqualTo(1));
                Assert.That(Count(track.ThumbnailAsset), Is.EqualTo(1));

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not
                Assert.That(Count(track.IconAsset), Is.EqualTo(0), "released through the unlock");
                Assert.That(Count(track.ThumbnailAsset), Is.EqualTo(0), "released by the container");
            });
        }

        // Without a reward track unlock Awake 0x674780 ends at once (0x6747EA) and OnDestroy 0x674960 tests the stored unlock
        // twice (0x674989, 0x6749A2) without touching anything.
        [Test]
        public void ARewardTrackWithoutItsUnlockDoesNothing()
        {
            WithApp((owned, app) =>
            {
                var container = NewContainer<UIContainerProgressionRewardTrack>(owned);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(container.Icon.Value == null, Is.True);
                Assert.That(container.Thumbnail.Value == null, Is.True);
                Assert.DoesNotThrow(() => Call(container, "OnDestroy")); // Reflection: Unity sends OnDestroy; Edit Mode does not
            });
        }
    }
}
