using System;
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
    // MetaGameUnlockCharacterSkin (0x5D81A0..0x5D82B0), MetaGameUnlockDreamPowerStore (0x5D8300..0x5D83B0),
    // MetaGameUnlockDreamPowerStoreBand (0x5D83D0..0x5D8510) and MetaGameUnlockDreamPowerStoreItem
    // (0x5D8560..0x5D8720), x86-64. Every unlock is built by its real constructor over real definition
    // assets; the asset wrappers are driven through their cached-asset path, which needs no Addressables.
    public sealed class MetaGameUnlockSkinAndStoreTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        // dump.cs TypeDefIndex 1938..1941 and the DummyDll rows: the skin unlock is open with
        // auto-property backing fields, the store and band unlocks are sealed, the item unlock is
        // a plain class over IMetaGameUnlock whose methods are all final interface slots.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(MetaGameUnlockCharacterSkin), 0x00100001,
                new (string, int)[]
                {
                    ("<CharacterSkinDefinition>k__BackingField", 0x0021),
                    ("<ChallengeXP>k__BackingField", 0x0021),
                    ("<RewardTrackType>k__BackingField", 0x0021),
                },
                new (string, int)[]
                {
                    ("get_CharacterSkinDefinition", 0x0886),
                    ("get_ChallengeXP", 0x0886),
                    ("get_RewardTrackType", 0x0886),
                    (".ctor", 0x1886),
                    ("GetId", 0x00c6),
                    ("GetProgressionType", 0x00c6),
                    ("GetWidget", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetCustomSprite", 0x00c6),
                    ("ReleaseAssets", 0x00c6),
                });
            AssertMembers(typeof(MetaGameUnlockDreamPowerStore), 0x00100101,
                new (string, int)[]
                {
                    ("m_storeDefinition", 0x0021),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("GetId", 0x00c6),
                    ("GetProgressionType", 0x00c6),
                    ("GetWidget", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetCustomTexture", 0x00c6),
                });
            AssertMembers(typeof(MetaGameUnlockDreamPowerStoreBand), 0x00100101,
                new (string, int)[]
                {
                    ("Definition", 0x0026),
                    ("ChallengeXP", 0x0026),
                    ("RewardTrackType", 0x0026),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("GetId", 0x00c6),
                    ("GetProgressionType", 0x00c6),
                    ("GetWidget", 0x00c6),
                    ("Save", 0x00c6),
                    ("GetCustomTexture", 0x00c6),
                    ("GetCustomSprite", 0x00c6),
                    ("ReleaseAssets", 0x00c6),
                });
            AssertMembers(typeof(MetaGameUnlockDreamPowerStoreItem), 0x00100001,
                new (string, int)[]
                {
                    ("Definition", 0x0026),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("GetId", 0x01e6),
                    ("GetProgressionType", 0x01e6),
                    ("GetWidget", 0x01e6),
                    ("GetTitle", 0x01e6),
                    ("GetBody", 0x01e6),
                    ("GetAudio", 0x01e6),
                    ("Save", 0x01e6),
                    ("GetCustomTexture", 0x01e6),
                    ("GetCustomSprite", 0x01e6),
                    ("ReleaseAssets", 0x01e6),
                    ("CanExitToMenu", 0x01e6),
                    ("UsesAlternativeExitToMenuEvent", 0x01e6),
                });
            AssertIl2CppOptions(typeof(MetaGameUnlockCharacterSkin), 1, 2);
            AssertIl2CppOptions(typeof(MetaGameUnlockDreamPowerStore), 1, 2);
            AssertIl2CppOptions(typeof(MetaGameUnlockDreamPowerStoreBand), 2, 1);
            AssertIl2CppOptions(typeof(MetaGameUnlockDreamPowerStoreItem), 1, 2);
        }

        // Constructor 0x5D81D0 stores the three arguments unvalidated; GetId 0x5D8220 reads the skin's GUID
        // (+0x18); GetProgressionType 0x5D8230 returns the key 0xdd0b5760; GetWidget 0x5D8240 returns null
        // (xor eax, eax) however the skin is authored; Save 0x5D8250 is a lone ret that touches nothing.
        [Test]
        public void SkinUnlockReportsItsSkinAndTheCharacterSkinKey()
        {
            Owned.Run(owned =>
            {
                var skin = Create<CharacterSkinDefinition>(owned, "{\"m_guid\":\"lucid-skin-guid\"}");
                foreach (RewardTrackType track in new[] { RewardTrackType.Adventure, RewardTrackType.Outfits, RewardTrackType.Shadow })
                {
                    var unlock = new MetaGameUnlockCharacterSkin(skin, 1250, track);
                    Assert.That(unlock.CharacterSkinDefinition, Is.SameAs(skin));
                    Assert.That(unlock.ChallengeXP, Is.EqualTo(1250));
                    Assert.That(unlock.RewardTrackType, Is.EqualTo(track));
                    Assert.That(unlock.GetId(), Is.EqualTo("lucid-skin-guid"));
                    Assert.That((int)unlock.GetProgressionType(), Is.EqualTo(-586459296), "0xdd0b5760");
                    Assert.That(unlock.GetWidget(), Is.Null);
                    Assert.DoesNotThrow(() => unlock.Save(null), "the save body is empty and never reads the manager");
                    Assert.That(unlock.CanExitToMenu(), Is.False);
                    Assert.That(unlock.UsesAlternativeExitToMenuEvent(), Is.False);
                }
                // The constructor stores a null skin and a negative XP unchanged.
                var unvalidated = new MetaGameUnlockCharacterSkin(null, -5, RewardTrackType.Shadow);
                Assert.That(unvalidated.CharacterSkinDefinition, Is.Null);
                Assert.That(unvalidated.ChallengeXP, Is.EqualTo(-5));
            });
        }

        // GetCustomSprite 0x5D8260 tail-calls LoadAsync on the skin's wrapper at +0x68 (the Thumbnail), passing the
        // callback unchanged; ReleaseAssets 0x5D82B0 tail-calls Unload on the same wrapper. A cached asset reaches
        // the callback at once and each load counts; Unload clamps at zero and clears the cached asset.
        [Test]
        public void SkinUnlockLoadsAndReleasesTheSkinThumbnail()
        {
            Owned.Run(owned =>
            {
                var skin = Create<CharacterSkinDefinition>(owned);
                Call(skin, "OnEnable");
                Sprite sprite = NewSprite(owned);
                Cache(skin.Thumbnail, sprite);
                var unlock = new MetaGameUnlockCharacterSkin(skin, 1, RewardTrackType.Adventure);
                var received = new System.Collections.Generic.List<Sprite>();
                unlock.GetCustomSprite(received.Add);
                Assert.That(received, Is.EqualTo(new[] { sprite }));
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(1));
                unlock.GetCustomSprite(received.Add);
                Assert.That(received.Count, Is.EqualTo(2));
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(2));
                unlock.ReleaseAssets();
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(1));
                Assert.That(Get<Sprite>(skin.Thumbnail, "m_loadedAsset"), Is.SameAs(sprite), "a positive count keeps the asset");
                unlock.ReleaseAssets();
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(0));
                Assert.That(Get<Sprite>(skin.Thumbnail, "m_loadedAsset") == null, Is.True);
                unlock.ReleaseAssets();
                Assert.That(Count(skin.Thumbnail), Is.EqualTo(0), "an extra release clamps at zero");
            });
        }

        // GetId 0x5D8330 reads the store's GUID (+0x18); GetProgressionType 0x5D8340 returns 0xc6fe4e40; GetWidget
        // 0x5D8350 reads the store's widget (+0x28); GetCustomTexture 0x5D83B0 calls the delegate with null at once
        // (mov rdi,[rsi+0x40]; xor esi,esi; jmp [rsi+0x18]): it never loads the store image and has no null guard.
        [Test]
        public void StoreUnlockReportsTheStoreAndAnswersTexturesWithNull()
        {
            Owned.Run(owned =>
            {
                var store = Create<DreamPowerStoreDefinition>(owned, "{\"m_guid\":\"lucid-store-guid\"}");
                var widget = owned.Host("Lucid store widget", false).AddComponent<UIWidgetProgression>();
                Set(store, "m_progressionUnlockWidget", widget);
                Call(store, "OnEnable");
                var unlock = new MetaGameUnlockDreamPowerStore(store);
                Assert.That(unlock.GetId(), Is.EqualTo("lucid-store-guid"));
                Assert.That((int)unlock.GetProgressionType(), Is.EqualTo(-956412352), "0xc6fe4e40");
                Assert.That(unlock.GetWidget(), Is.SameAs(widget));

                var calls = new System.Collections.Generic.List<Texture>();
                Texture marker = owned.Track(new Texture2D(1, 1));
                calls.Add(marker);
                unlock.GetCustomTexture(calls.Add);
                Assert.That(calls, Is.EqualTo(new Texture[] { marker, null }), "invoked once, with null");
                Assert.That(Count(store.ImageAsset), Is.EqualTo(0), "the store image is not loaded");
                Assert.Throws<NullReferenceException>(() => unlock.GetCustomTexture(null), "no null guard before the call");
                Assert.DoesNotThrow(unlock.ReleaseAssets, "the inherited release is empty");
                Assert.That(Count(store.ImageAsset), Is.EqualTo(0));
            });
        }

        // Save 0x5D8360 stores 0xb7834d4b (DreamPowerStoreIsNewState.IsNew) into the store save record (+0xE0 of
        // the open save, field +0x20), marks it dirty and then requests a save. The store is flagged new whatever
        // it held: a record already Seen is overwritten.
        [Test]
        public void StoreUnlockSaveFlagsTheStoreAsNewEvenWhenItWasSeen()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                var saveManager = ProcessManager.GetSystem<SaveManager>();
                var store = Create<DreamPowerStoreDefinition>(owned);
                var unlock = new MetaGameUnlockDreamPowerStore(store);
                SaveDataDreamPowerStore record = saveManager.CurrentSave.DreamPowerStore;
                Assert.That((int)record.IsNewState, Is.EqualTo(0));
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                unlock.Save(saveManager);
                Assert.That((int)record.IsNewState, Is.EqualTo(-1216131765), "0xb7834d4b");

                record.IsNewState = (DreamPowerStoreIsNewState)1832392787;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                unlock.Save(saveManager);
                Assert.That((int)record.IsNewState, Is.EqualTo(-1216131765), "a seen store is flagged new again");
            });
        }

        // Constructor 0x5D83D0; GetId 0x5D8420 reads the band's GUID (+0x18); GetWidget 0x5D8480 reads +0x30; Save
        // 0x5D8490 is a lone ret; GetCustomTexture 0x5D84A0 calls the delegate with null at once, like the store's.
        [Test]
        public void BandUnlockKeepsItsConstructorValuesAndAnswersTexturesWithNull()
        {
            Owned.Run(owned =>
            {
                var band = Create<DreamPowerStoreBandDefinition>(owned, "{\"m_guid\":\"lucid-band-guid\"}");
                var widget = owned.Host("Lucid band widget", false).AddComponent<UIWidgetProgression>();
                Set(band, "m_progressionUnlockWidget", widget);
                var unlock = new MetaGameUnlockDreamPowerStoreBand(band, 330, RewardTrackType.Outfits);
                Assert.That(unlock.Definition, Is.SameAs(band));
                Assert.That(unlock.ChallengeXP, Is.EqualTo(330));
                Assert.That(unlock.RewardTrackType, Is.EqualTo(RewardTrackType.Outfits));
                Assert.That(unlock.GetId(), Is.EqualTo("lucid-band-guid"));
                Assert.That(unlock.GetWidget(), Is.SameAs(widget));
                Assert.DoesNotThrow(() => unlock.Save(null));
                var calls = new System.Collections.Generic.List<Texture>();
                unlock.GetCustomTexture(calls.Add);
                Assert.That(calls, Is.EqualTo(new Texture[] { null }));
                Assert.Throws<NullReferenceException>(() => unlock.GetCustomTexture(null));
            });
        }

        // GetProgressionType 0x5D8430 reads the band type (+0x20) and compares it with 0xf7e3e2ed (Band4),
        // 0x1e8047d8 (Band2) and 0x6987774e (Band3); each match selects the like-numbered key (0xcffcb35a,
        // 0x269f166f, 0x519826f9) and every other value, Band1 and None included, selects 0xbf9647d5
        // (DreamPowerStoreBand1). Native-derived truth table: (band type, expected key).
        [Test]
        public void BandUnlockMapsTheBandTypeToItsProgressionKey()
        {
            var table = new[]
            {
                (type: -136060179, key: -805522598),   // Band4 0xf7e3e2ed -> 0xcffcb35a
                (type: 511723480, key: 647960175),     // Band2 0x1e8047d8 -> 0x269f166f
                (type: 1770485582, key: 1368925945),   // Band3 0x6987774e -> 0x519826f9
                (type: -2021058974, key: -1080670251), // Band1 -> 0xbf9647d5
                (type: 0, key: -1080670251),           // None -> 0xbf9647d5
                (type: 12345, key: -1080670251),       // undefined -> 0xbf9647d5
                (type: -1, key: -1080670251),          // undefined -> 0xbf9647d5
            };
            Owned.Run(owned =>
            {
                var band = Create<DreamPowerStoreBandDefinition>(owned);
                var unlock = new MetaGameUnlockDreamPowerStoreBand(band, 0, RewardTrackType.Adventure);
                foreach (var row in table)
                {
                    JsonUtility.FromJsonOverwrite("{\"m_type\":" + row.type + "}", band);
                    Assert.That((int)unlock.GetProgressionType(), Is.EqualTo(row.key), "band type " + row.type);
                }
            });
        }

        // GetCustomSprite 0x5D84C0 and ReleaseAssets 0x5D8510 tail-call LoadAsync and Unload on the band's wrapper at
        // +0x48 (ImageAsset), not the locked-background wrapper at +0x50.
        [Test]
        public void BandUnlockLoadsAndReleasesTheBandImageNotTheLockedBackground()
        {
            Owned.Run(owned =>
            {
                var band = Create<DreamPowerStoreBandDefinition>(owned);
                Call(band, "OnEnable");
                Sprite image = NewSprite(owned);
                Sprite background = NewSprite(owned);
                Cache(band.ImageAsset, image);
                Cache(band.SlotLockedBackground, background);
                var unlock = new MetaGameUnlockDreamPowerStoreBand(band, 0, RewardTrackType.Adventure);
                var received = new System.Collections.Generic.List<Sprite>();
                unlock.GetCustomSprite(received.Add);
                Assert.That(received, Is.EqualTo(new[] { image }));
                Assert.That(Count(band.ImageAsset), Is.EqualTo(1));
                Assert.That(Count(band.SlotLockedBackground), Is.EqualTo(0));
                unlock.ReleaseAssets();
                Assert.That(Count(band.ImageAsset), Is.EqualTo(0));
                Assert.That(Get<Sprite>(band.ImageAsset, "m_loadedAsset") == null, Is.True);
                Assert.That(Get<Sprite>(band.SlotLockedBackground, "m_loadedAsset"), Is.SameAs(background));
            });
        }

        // Item constructor 0x5D8560 then GetId 0x5D8590 (GUID +0x18); GetProgressionType 0x5D85A0 returns 0x11e244f7;
        // GetWidget 0x5D85B0 and GetAudio 0x5D85E0 return zero; GetTitle 0x5D85C0 and GetBody 0x5D85D0 read the dream
        // power's name (+0x44) and description (+0x48); Save 0x5D85F0 is a lone ret; both exit flags 0x5D8710 and
        // 0x5D8720 return zero.
        [Test]
        public void ItemUnlockReportsTheDreamPowerWithoutAWidgetOrAudio()
        {
            Owned.Run(owned =>
            {
                var power = Create<DreamPowerDefinition>(owned,
                    "{\"m_guid\":\"lucid-power-guid\",\"m_name\":" + (int)Strings.NONE + ",\"m_description\":" + (int)Strings.MISSING + "}");
                var unlock = new MetaGameUnlockDreamPowerStoreItem(power);
                Assert.That(unlock.Definition, Is.SameAs(power));
                Assert.That(unlock.GetId(), Is.EqualTo("lucid-power-guid"));
                Assert.That((int)unlock.GetProgressionType(), Is.EqualTo(300041463), "0x11e244f7");
                Assert.That(unlock.GetWidget(), Is.Null);
                Assert.That(unlock.GetTitle(), Is.EqualTo(Strings.NONE), "the name");
                Assert.That(unlock.GetBody(), Is.EqualTo(Strings.MISSING), "the description");
                Assert.That(unlock.GetAudio(), Is.EqualTo(HLAudioClipIdentifier.None));
                Assert.DoesNotThrow(() => unlock.Save(null));
                Assert.That(unlock.CanExitToMenu(), Is.False);
                Assert.That(unlock.UsesAlternativeExitToMenuEvent(), Is.False);
                // Swapped authoring swaps the answers, so title and body are not interchangeable.
                JsonUtility.FromJsonOverwrite("{\"m_name\":" + (int)Strings.MISSING + ",\"m_description\":" + (int)Strings.NONE + "}", power);
                Assert.That(unlock.GetTitle(), Is.EqualTo(Strings.MISSING));
                Assert.That(unlock.GetBody(), Is.EqualTo(Strings.NONE));
            });
        }

        // GetCustomTexture 0x5D8600 calls Load on the dream power's icon wrapper (+0x50), takes Sprite.texture and
        // passes it to the callback; GetCustomSprite 0x5D8660 passes the loaded sprite itself; ReleaseAssets 0x5D86C0
        // unloads the same wrapper. Both loads are synchronous and count.
        [Test]
        public void ItemUnlockLoadsTheIconSynchronouslyAndPassesTheTextureOrTheSprite()
        {
            Owned.Run(owned =>
            {
                var power = Create<DreamPowerDefinition>(owned);
                Call(power, "OnEnable");
                Sprite icon = NewSprite(owned);
                Cache(power.IconAsset, icon);
                var unlock = new MetaGameUnlockDreamPowerStoreItem(power);
                Texture texture = null;
                unlock.GetCustomTexture(loaded => texture = loaded);
                Assert.That(texture, Is.SameAs(icon.texture));
                Assert.That(Count(power.IconAsset), Is.EqualTo(1));
                Sprite sprite = null;
                unlock.GetCustomSprite(loaded => sprite = loaded);
                Assert.That(sprite, Is.SameAs(icon));
                Assert.That(Count(power.IconAsset), Is.EqualTo(2));
                unlock.ReleaseAssets();
                Assert.That(Count(power.IconAsset), Is.EqualTo(1));
                unlock.ReleaseAssets();
                Assert.That(Count(power.IconAsset), Is.EqualTo(0));
                Assert.That(Get<Sprite>(power.IconAsset, "m_loadedAsset") == null, Is.True);
                // The icon is loaded again before the callback is called (0x5D8660), so it is cached once more to keep
                // the load off the addressables system.
                Cache(power.IconAsset, icon);
                Assert.Throws<NullReferenceException>(() => unlock.GetCustomSprite(null), "the callback has no null guard");
            });
        }
    }
}
