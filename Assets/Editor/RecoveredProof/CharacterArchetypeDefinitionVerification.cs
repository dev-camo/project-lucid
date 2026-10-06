using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;
using Hardlight.Enums;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectLucid
{
    public static class CharacterArchetypeDefinitionVerification
    {
        private static int checks;
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("Original character archetype definitions: " + name);
            checks++;
        }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
        private static object Read(object value, string name) => Field(value.GetType(), name).GetValue(value);
        private static void Enable(CharacterArchetypeDefinition value) => typeof(CharacterArchetypeDefinition).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(value, null);
        private static AssetReferenceT<T> Asset<T>(ManagedAddressableAsset<T> value) where T : UnityEngine.Object => (AssetReferenceT<T>)Field(typeof(ManagedAddressableAsset<T>), "m_assetReference").GetValue(value);
        private static void Wrapper<T>(ManagedAddressableAsset<T> value, AssetReferenceT<T> expected, string name) where T : UnityEngine.Object
        {
            Check(!ReferenceEquals(value, null), name + " original wrapper exists");
            Check(ReferenceEquals(Asset(value), expected), name + " exact captured authored reference");
            Check(ReferenceEquals(Read(value, "m_loadedAsset"), null) && (int)Read(value, "m_refCount") == 0 && !((AsyncOperationHandle<T>)Read(value, "m_assetHandle")).IsValid(), name + " no load or count increment");
        }
        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); throw new Exception("Expected original fault absent: " + name); }
            catch (T) { Check(true, name); }
        }
        private static DefinitionDataType<CharacterArchetype, CharacterArchetypeDefinition>.DefinitionElement<CharacterArchetypeDefinition> Row(CharacterArchetypeDefinition value)
        {
            var result = Raw<DefinitionDataType<CharacterArchetype, CharacterArchetypeDefinition>.DefinitionElement<CharacterArchetypeDefinition>>();
            result.Data = value;
            return result;
        }
        public static int RunManaged()
        {
            checks = 0;
            var definition = Raw<CharacterArchetypeDefinition>();
            Check((int)definition.CharacterArchetype == 0 && !definition.UnlockedByDefault && (int)definition.LockedMissionStringId == 0 && ReferenceEquals(definition.ProgressionUnlockWidget, null), "raw own scalar fields; no Unity constructor claim");
            Check(ReferenceEquals(definition.UIImage, null) && ReferenceEquals(definition.AlternateImage, null) && ReferenceEquals(definition.SilhouetteImage, null) && ReferenceEquals(definition.Texture, null), "raw four backing fields initially null");
            var widget = Raw<UIWidgetProgression>();
            Set(definition, "m_characterArchetype", (CharacterArchetype)(-79));
            Set(definition, "m_progressionUnlockWidget", widget);
            Set(definition, "m_unlockedByDefault", true);
            Set(definition, "m_lockedMissionStringId", (Strings)(-981));
            Check((int)definition.CharacterArchetype == -79 && definition.UnlockedByDefault && (int)definition.LockedMissionStringId == -981 && ReferenceEquals(definition.ProgressionUnlockWidget, widget), "direct authored getter identities and unknown enum values");
            Enable(definition);
            Wrapper(definition.UIImage, null, "null primary");
            Wrapper(definition.AlternateImage, null, "null alternate");
            Wrapper(definition.SilhouetteImage, null, "null silhouette");
            Wrapper(definition.Texture, null, "null texture");
            Check(!ReferenceEquals(definition.UIImage, definition.AlternateImage) && !ReferenceEquals(definition.UIImage, definition.SilhouetteImage) && !ReferenceEquals(definition.AlternateImage, definition.SilhouetteImage), "three separate sprite wrapper allocations for null references");
            var primary = new AssetReferenceAtlasedSprite("0123456789abcdef0123456789abcdef") { SubObjectName = "primary" };
            var alternate = new AssetReferenceAtlasedSprite("fedcba9876543210fedcba9876543210") { SubObjectName = "alternate" };
            var silhouette = new AssetReferenceAtlasedSprite("00112233445566778899aabbccddeeff") { SubObjectName = "silhouette" };
            var texture = new AssetReferenceT<Texture>("ffeeddccbbaa99887766554433221100");
            Set(definition, "m_uiImage", primary); Set(definition, "m_alternateImage", alternate); Set(definition, "m_silhouetteImage", silhouette); Set(definition, "m_texture", texture);
            Enable(definition);
            Wrapper(definition.UIImage, primary, "authored primary");
            Wrapper(definition.AlternateImage, alternate, "authored alternate");
            Wrapper(definition.SilhouetteImage, silhouette, "authored silhouette");
            Wrapper(definition.Texture, texture, "authored texture");
            Check((int)definition.CharacterArchetype == -79 && definition.UnlockedByDefault && (int)definition.LockedMissionStringId == -981 && ReferenceEquals(definition.ProgressionUnlockWidget, widget), "enable retains unrelated authored fields");
            var oldPrimary = definition.UIImage; var oldAlternate = definition.AlternateImage; var oldSilhouette = definition.SilhouetteImage; var oldTexture = definition.Texture;
            Sprite retained = Raw<Sprite>(); Texture retainedTexture = Raw<Texture2D>();
            Set(oldPrimary, "m_loadedAsset", retained); Set(oldPrimary, "m_refCount", 6); Set(oldTexture, "m_loadedAsset", retainedTexture); Set(oldTexture, "m_refCount", -4);
            Set(definition, "m_uiImage", silhouette); Set(definition, "m_alternateImage", null); Set(definition, "m_silhouetteImage", primary); Set(definition, "m_texture", null);
            Enable(definition);
            Check(!ReferenceEquals(oldPrimary, definition.UIImage) && !ReferenceEquals(oldAlternate, definition.AlternateImage) && !ReferenceEquals(oldSilhouette, definition.SilhouetteImage) && !ReferenceEquals(oldTexture, definition.Texture), "repeated enable replaces every published wrapper");
            Wrapper(definition.UIImage, silhouette, "changed primary"); Wrapper(definition.AlternateImage, null, "cleared alternate"); Wrapper(definition.SilhouetteImage, primary, "changed silhouette"); Wrapper(definition.Texture, null, "cleared texture");
            Check(ReferenceEquals(Asset(oldPrimary), primary) && ReferenceEquals(Asset(oldAlternate), alternate) && ReferenceEquals(Asset(oldSilhouette), silhouette) && ReferenceEquals(Asset(oldTexture), texture), "detached wrappers retain prior exact references");
            Check(ReferenceEquals(Read(oldPrimary, "m_loadedAsset"), retained) && (int)Read(oldPrimary, "m_refCount") == 6 && ReferenceEquals(Read(oldTexture, "m_loadedAsset"), retainedTexture) && (int)Read(oldTexture, "m_refCount") == -4, "enable does not release or mutate detached cached wrappers");
            foreach (string name in new[] { "UIImage", "AlternateImage", "SilhouetteImage", "Texture" })
            {
                PropertyInfo property = typeof(CharacterArchetypeDefinition).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Check(property.GetGetMethod().IsPublic && property.GetSetMethod(true).IsPrivate, "original getter/private setter " + name);
                object replacement = name == "Texture" ? (object)new ManagedAddressableAsset<Texture>(texture) : new ManagedAddressableAsset<Sprite>(primary);
                property.GetSetMethod(true).Invoke(definition, new[] { replacement });
                Check(ReferenceEquals(property.GetValue(definition), replacement), "original private setter identity " + name);
            }
            var group = Raw<CharacterArchetypeDefinitionGroup>();
            Check(group.m_elements == null, "raw group has no invented default elements");
            var comparer = typeof(CharacterArchetypeDefinitionGroup).GetMethod("GetKeyComparer", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(group, null);
            Check(ReferenceEquals(comparer, HardlightEnumComparers.CharacterArchetypeComparer), "genuine original static registry comparer");
            group.m_elements = Array.Empty<DefinitionDataType<CharacterArchetype, CharacterArchetypeDefinition>.DefinitionElement<CharacterArchetypeDefinition>>();
            var empty = group.GetData(); Check(empty.Count == 0 && ReferenceEquals(empty.Comparer, comparer), "empty real dictionary retains original comparer");
            group.m_elements = new[] { Row(definition) }; Check(ReferenceEquals(group.GetData()[(CharacterArchetype)(-79)], definition), "genuine own key getter forwards original data");
            group.m_elements = new[] { Row(definition), Row(definition) }; Throws<ArgumentException>(() => group.GetData(), "duplicate native keys not overwritten");
            group.m_elements = new DefinitionDataType<CharacterArchetype, CharacterArchetypeDefinition>.DefinitionElement<CharacterArchetypeDefinition>[] { null }; Throws<NullReferenceException>(() => group.GetData(), "null original row not skipped");
            group.m_elements = new[] { Row(null) }; Throws<NullReferenceException>(() => group.GetData(), "null original data getter faults");
            group.m_elements = null; Throws<NullReferenceException>(() => group.GetData(), "null original array faults");
            return checks;
        }
        private static T Own<T>(List<UnityEngine.Object> owned) where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            if (ReferenceEquals(value, null)) throw new Exception("Original concrete archetype creation failed: " + typeof(T).FullName);
            owned.Add(value); return value;
        }
        private static void Release(List<UnityEngine.Object> owned, int index)
        {
            if (index < 0) return;
            try { if (!ReferenceEquals(owned[index], null)) UnityEngine.Object.DestroyImmediate(owned[index]); }
            finally { Release(owned, index - 1); }
        }
        public static int RunEngine()
        {
            checks = 0; var owned = new List<UnityEngine.Object>();
            try
            {
                var definition = Own<CharacterArchetypeDefinition>(owned); var copy = Own<CharacterArchetypeDefinition>(owned);
                Check((int)definition.CharacterArchetype == 0 && !definition.UnlockedByDefault && (int)definition.LockedMissionStringId == 0 && ReferenceEquals(definition.ProgressionUnlockWidget, null), "actual base-only constructor own defaults");
                Check(definition.UIImage != null && definition.AlternateImage != null && definition.SilhouetteImage != null && definition.Texture != null && ReferenceEquals(Asset(definition.UIImage), null) && ReferenceEquals(Asset(definition.AlternateImage), null) && ReferenceEquals(Asset(definition.SilhouetteImage), null) && ReferenceEquals(Asset(definition.Texture), null), "actual automatic OnEnable constructs all null-reference wrappers");
                Check(!ReferenceEquals(definition.UIImage, definition.AlternateImage) && !ReferenceEquals(definition.AlternateImage, definition.SilhouetteImage), "actual separate sprite wrapper identities");
                var go = new GameObject("ProjectLucid.CharacterArchetypeDefinitionVerification"); owned.Add(go); go.SetActive(false); var widget = go.AddComponent<UIWidgetProgression>();
                Check(widget != null && widget.MaskedTexture != null, "actual inactive genuine progression widget constructor");
                var primary = new AssetReferenceAtlasedSprite("0123456789abcdef0123456789abcdef") { SubObjectName = "primary" };
                var alternate = new AssetReferenceAtlasedSprite("fedcba9876543210fedcba9876543210") { SubObjectName = "alternate" };
                var silhouette = new AssetReferenceAtlasedSprite("00112233445566778899aabbccddeeff") { SubObjectName = "silhouette" };
                var texture = new AssetReferenceT<Texture>("ffeeddccbbaa99887766554433221100");
                Set(definition, "m_characterArchetype", (CharacterArchetype)(-79)); Set(definition, "m_unlockedByDefault", true); Set(definition, "m_lockedMissionStringId", (Strings)(-981)); Set(definition, "m_progressionUnlockWidget", widget);
                Set(definition, "m_uiImage", primary); Set(definition, "m_alternateImage", alternate); Set(definition, "m_silhouetteImage", silhouette); Set(definition, "m_texture", texture); Enable(definition);
                Check((int)definition.CharacterArchetype == -79 && definition.UnlockedByDefault && (int)definition.LockedMissionStringId == -981 && ReferenceEquals(definition.ProgressionUnlockWidget, widget), "actual authored scalar/reference getters");
                Check(ReferenceEquals(Asset(definition.UIImage), primary) && ReferenceEquals(Asset(definition.AlternateImage), alternate) && ReferenceEquals(Asset(definition.SilhouetteImage), silhouette) && ReferenceEquals(Asset(definition.Texture), texture), "actual four authored captured references");
                Check((int)Read(definition.UIImage, "m_refCount") == 0 && !((AsyncOperationHandle<Sprite>)Read(definition.UIImage, "m_assetHandle")).IsValid() && (int)Read(definition.Texture, "m_refCount") == 0 && !((AsyncOperationHandle<Texture>)Read(definition.Texture, "m_assetHandle")).IsValid(), "actual enable does not access an asset catalog");
                var oldPrimary = definition.UIImage; var oldAlternate = definition.AlternateImage; var oldSilhouette = definition.SilhouetteImage; var oldTexture = definition.Texture;
                var held = new Texture2D(2, 2); owned.Add(held); Set(oldTexture, "m_loadedAsset", held); Set(oldTexture, "m_refCount", 11);
                Enable(definition);
                Check(!ReferenceEquals(oldPrimary, definition.UIImage) && !ReferenceEquals(oldAlternate, definition.AlternateImage) && !ReferenceEquals(oldSilhouette, definition.SilhouetteImage) && !ReferenceEquals(oldTexture, definition.Texture), "actual repeated enable replaces all four wrappers");
                Check(ReferenceEquals(Read(oldTexture, "m_loadedAsset"), held) && (int)Read(oldTexture, "m_refCount") == 11 && held != null, "actual detached wrapper is not unloaded or destroyed");
                string json = JsonUtility.ToJson(definition);
                Check(!json.Contains("k__BackingField") && !json.Contains("m_loadedAsset") && !json.Contains("m_assetHandle") && !json.Contains("m_refCount"), "actual wrapper backing/cache fields remain unserialized");
                JsonUtility.FromJsonOverwrite(json, copy);
                Check((int)copy.CharacterArchetype == -79 && copy.UnlockedByDefault && (int)copy.LockedMissionStringId == -981 && ReferenceEquals(copy.ProgressionUnlockWidget, widget), "actual original private fields and widget JSON roundtrip");
                var copyPrimary = (AssetReferenceAtlasedSprite)Read(copy, "m_uiImage"); var copyAlternate = (AssetReferenceAtlasedSprite)Read(copy, "m_alternateImage"); var copySilhouette = (AssetReferenceAtlasedSprite)Read(copy, "m_silhouetteImage"); var copyTexture = (AssetReferenceT<Texture>)Read(copy, "m_texture");
                Check(copyPrimary.AssetGUID == primary.AssetGUID && copyPrimary.SubObjectName == "primary" && copyAlternate.AssetGUID == alternate.AssetGUID && copyAlternate.SubObjectName == "alternate" && copySilhouette.AssetGUID == silhouette.AssetGUID && copySilhouette.SubObjectName == "silhouette" && copyTexture.AssetGUID == texture.AssetGUID, "actual atlas/subobject and texture GUID JSON roundtrip");
                Enable(copy); Check(ReferenceEquals(Asset(copy.UIImage), copyPrimary) && ReferenceEquals(Asset(copy.AlternateImage), copyAlternate) && ReferenceEquals(Asset(copy.SilhouetteImage), copySilhouette) && ReferenceEquals(Asset(copy.Texture), copyTexture), "actual explicit enable reloads deserialized current references");
                var group = Own<CharacterArchetypeDefinitionGroup>(owned); var groupCopy = Own<CharacterArchetypeDefinitionGroup>(owned);
                Check(group.m_elements == null, "actual original group base-only constructor");
                group.m_elements = new[] { new DefinitionDataType<CharacterArchetype, CharacterArchetypeDefinition>.DefinitionElement<CharacterArchetypeDefinition>(definition) };
                var data = group.GetData(); Check(ReferenceEquals(data[(CharacterArchetype)(-79)], definition) && ReferenceEquals(data.Comparer, HardlightEnumComparers.CharacterArchetypeComparer), "actual genuine group key/comparer/reference");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(group), groupCopy);
                Check(groupCopy.m_elements.Length == 1 && ReferenceEquals(groupCopy.m_elements[0].Data, definition) && ReferenceEquals(groupCopy.GetData()[(CharacterArchetype)(-79)], definition), "actual genuine group element/reference roundtrip");
                return checks;
            }
            finally { Release(owned, owned.Count - 1); }
        }
        public static void Run()
        {
            int managed = RunManaged(); if (managed != 60) throw new Exception("Original archetype managed check count changed: " + managed);
            int engine = RunEngine(); if (engine != 16) throw new Exception("Original archetype actual engine check count changed: " + engine);
            Debug.Log("Bounded original character archetype definition checks=" + (managed + engine) + " (managed=" + managed + ", actual Unity=" + engine + "); supplied catalog/assets, authored owners/layout/scopes/App/DataManager/startup/fullgame remain unapproved.");
        }
    }
}
