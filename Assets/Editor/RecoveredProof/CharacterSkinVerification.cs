using System;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ProjectLucid
{
    public static class CharacterSkinVerification
    {
        private static int checks;
        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Character skin: " + detail);
            checks++;
        }

        private static void Set(CharacterSkinDefinition skin, string name, object value)
        {
            typeof(CharacterSkinDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skin, value);
        }

        private static void Enable(CharacterSkinDefinition skin)
        {
            typeof(CharacterSkinDefinition).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skin, null);
        }

        private static object Reference(ManagedAddressableAsset<Sprite> asset)
        {
            return typeof(ManagedAddressableAsset<Sprite>).GetField("m_assetReference", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(asset);
        }

        // This checks managed reference retention and callback replacement using
        // the genuine compiled game and Addressables types. It does not run a
        // ScriptableObject constructor, a resource load, or an engine lifecycle.
        public static int RunManaged()
        {
            checks = 0;
            var skin = (CharacterSkinDefinition)FormatterServices.GetUninitializedObject(typeof(CharacterSkinDefinition));
            Require(ReferenceEquals(skin.Prefab, null), "uninitialized prefab");
            Require(ReferenceEquals(skin.MeshPrefab, null), "uninitialized mesh prefab");
            Require(ReferenceEquals(skin.Render, null), "uninitialized render");
            Require(ReferenceEquals(skin.RenderCompanion, null), "uninitialized companion");
            Require(ReferenceEquals(skin.CardImage, null), "uninitialized card");
            Require(ReferenceEquals(skin.PfxOverrides, null), "uninitialized array is not constructor evidence");
            Require(ReferenceEquals(skin.AudioOverrides, null), "uninitialized audio array");
            Require(!skin.UnlockedByDefault, "uninitialized boolean");
            Require((int)skin.Name == 0, "uninitialized name");
            Require(ReferenceEquals(skin.Thumbnail, null), "uninitialized thumbnail");

            var prefab = new AssetReferenceT<GameObject>("00112233445566778899aabbccddeeff");
            var mesh = new AssetReferenceT<GameObject>("ffeeddccbbaa99887766554433221100");
            var render = new AssetReferenceAtlasedSprite("0123456789abcdef0123456789abcdef");
            var companion = new AssetReferenceAtlasedSprite("fedcba9876543210fedcba9876543210");
            var card = new AssetReferenceAtlasedSprite("11111111222222223333333344444444");
            var pfx = new ActorParticleEffectLookup[2];
            var audio = new ActorAudioDefinition[3];
            Set(skin, "m_prefab", prefab); Set(skin, "m_meshPrefab", mesh);
            Set(skin, "m_render", render); Set(skin, "m_renderCompanion", companion); Set(skin, "m_cardImage", card);
            Set(skin, "m_name", (Strings)unchecked((int)0xa5f00e17));
            Set(skin, "m_pfxOverrides", pfx); Set(skin, "m_audioOverrides", audio); Set(skin, "m_unlockedByDefault", true);
            Require(ReferenceEquals(skin.Prefab, prefab), "prefab retains the exact reference");
            Require(ReferenceEquals(skin.MeshPrefab, mesh), "mesh retains distinct reference");
            Require(ReferenceEquals(skin.Render, render), "render retains reference");
            Require(ReferenceEquals(skin.RenderCompanion, companion), "companion remains distinct");
            Require(ReferenceEquals(skin.CardImage, card), "card remains distinct");
            Require((int)skin.Name == unchecked((int)0xa5f00e17), "unrecognised hashed name is unfiltered");
            Require(ReferenceEquals(skin.PfxOverrides, pfx), "PFX array is not copied");
            Require(ReferenceEquals(skin.AudioOverrides, audio), "audio array is not copied");
            Require(skin.UnlockedByDefault, "unlock getter");
            pfx[0] = null; audio[1] = null;
            Require(skin.PfxOverrides.Length == 2 && ReferenceEquals(skin.PfxOverrides[0], null), "PFX null entries and length retained");
            Require(skin.AudioOverrides.Length == 3 && ReferenceEquals(skin.AudioOverrides[1], null), "audio null entries and length retained");

            Enable(skin);
            var first = skin.Thumbnail;
            Require(!ReferenceEquals(first, null), "enable constructs thumbnail wrapper");
            Require(ReferenceEquals(Reference(first), render), "thumbnail uses render rather than card image");
            Require(!ReferenceEquals(Reference(first), card), "card is not thumbnail source");
            Require(!ReferenceEquals(Reference(first), companion), "companion is not thumbnail source");
            Require(ReferenceEquals(skin.Render, render) && ReferenceEquals(skin.CardImage, card), "enable leaves authored references intact");
            Enable(skin);
            Require(!ReferenceEquals(skin.Thumbnail, first), "repeat enable replaces existing wrapper");
            Require(ReferenceEquals(Reference(skin.Thumbnail), render), "repeat enable retains current render");
            Require(ReferenceEquals(Reference(first), render), "replaced wrapper remains unchanged");
            Set(skin, "m_render", null);
            var second = skin.Thumbnail;
            Enable(skin);
            Require(!ReferenceEquals(skin.Thumbnail, second), "null render still creates a fresh wrapper");
            Require(ReferenceEquals(Reference(skin.Thumbnail), null), "null render is retained");
            Require(!skin.Thumbnail.IsValid(), "null reference wrapper is invalid without loading");
            Require(ReferenceEquals(Reference(second), render), "null render does not rewrite old wrapper");
            Set(skin, "m_pfxOverrides", null); Set(skin, "m_audioOverrides", null);
            Require(ReferenceEquals(skin.PfxOverrides, null), "null PFX not replaced by getter");
            Require(ReferenceEquals(skin.AudioOverrides, null), "null audio not replaced by getter");
            Set(skin, "m_unlockedByDefault", false); Set(skin, "m_name", (Strings)int.MinValue);
            Require(!skin.UnlockedByDefault, "unlock false retained");
            Require((int)skin.Name == int.MinValue, "minimum hashed name retained");
            return checks;
        }

        // These extra assertions require actual Unity. The host owns this single
        // created object; managed hosts must never invoke this method.
        public static int RunAll()
        {
            RunManaged();
            CharacterSkinDefinition skin = null;
            try
            {
                skin = ScriptableObject.CreateInstance<CharacterSkinDefinition>();
                Require(ReferenceEquals(skin.PfxOverrides, Array.Empty<ActorParticleEffectLookup>()), "constructor shared empty PFX");
                Require(ReferenceEquals(skin.AudioOverrides, Array.Empty<ActorAudioDefinition>()), "constructor shared empty audio");
                Require(!skin.UnlockedByDefault && (int)skin.Name == 0, "constructor zero defaults");
                Require(ReferenceEquals(skin.Prefab, null) && ReferenceEquals(skin.MeshPrefab, null), "constructor unset prefab references");
                Require(ReferenceEquals(skin.Render, null) && ReferenceEquals(skin.RenderCompanion, null) && ReferenceEquals(skin.CardImage, null), "constructor unset sprite references");
                Require(!ReferenceEquals(skin.Thumbnail, null), "actual OnEnable runs during creation");
                Require(ReferenceEquals(Reference(skin.Thumbnail), null), "actual OnEnable retains null render");
                string json = JsonUtility.ToJson(skin);
                Require(json.Contains("\"m_pfxOverrides\":[]"), "serialized PFX empty array");
                Require(json.Contains("\"m_audioOverrides\":[]"), "serialized audio empty array");
                Require(json.Contains("\"m_unlockedByDefault\":false"), "serialized private unlock field");
                JsonUtility.FromJsonOverwrite("{\"m_name\":-2147483648,\"m_unlockedByDefault\":true}", skin);
                Require((int)skin.Name == int.MinValue && skin.UnlockedByDefault, "actual private field deserialization");
                Require(ReferenceEquals(skin.PfxOverrides, Array.Empty<ActorParticleEffectLookup>()), "unmentioned PFX field preserved by overwrite");
                Require(ReferenceEquals(skin.AudioOverrides, Array.Empty<ActorAudioDefinition>()), "unmentioned audio preserved by overwrite");
            }
            finally
            {
                if (!ReferenceEquals(skin, null)) UnityEngine.Object.DestroyImmediate(skin);
            }
            return checks;
        }
    }
}
