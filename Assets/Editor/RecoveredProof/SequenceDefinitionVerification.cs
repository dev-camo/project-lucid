using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;
using HardlightEnumComparers = HardlightProject.HardlightEnumComparers;

namespace ProjectLucid
{
    // Source-derived definition subset proof. Actual Unity object allocation and
    // serialization are separate from raw managed field/key/default checks.
    public static class SequenceDefinitionVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original sequence definitions: " + label); checks++; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type owner, string name) => owner.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
        private static object Comparer(object value) => value.GetType().GetMethod("GetKeyComparer", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(value, null);
        private static void Throws<T>(Action action, string label) where T : Exception { try { action(); throw new Exception("Expected fault absent: " + label); } catch (T) { Check(true, label); } }
        private static DefinitionDataType<TKey,TData>.DefinitionElement<TData> Row<TKey,TData>(TData data) where TData : ScriptableObject
        { var row = Raw<DefinitionDataType<TKey,TData>.DefinitionElement<TData>>(); row.Data = data; return row; }
        private static void Group<TKey,TData>(DefinitionDataType<TKey,TData> group, TData value, TKey first, TKey second, Action mutate, IEqualityComparer<TKey> comparer) where TData : ScriptableObject
        {
            Check(ReferenceEquals(Comparer(group), comparer), "exact original readonly registry comparer " + typeof(TData).Name);
            group.m_elements = Array.Empty<DefinitionDataType<TKey,TData>.DefinitionElement<TData>>();
            var empty = group.GetData(); Check(empty.Count == 0 && ReferenceEquals(empty.Comparer, comparer), "empty array retains genuine key comparer");
            group.m_elements = new[] { Row<TKey,TData>(value) };
            var firstData = group.GetData(); Check(firstData.Count == 1 && ReferenceEquals(firstData[first], value), "getter feeds exact authored key/value");
            mutate(); var secondData = group.GetData();
            Check(secondData.Count == 1 && ReferenceEquals(secondData[second], value) && firstData.ContainsKey(first) && !ReferenceEquals(firstData, secondData), "each call rebuilds dictionary from current authored field");
            group.m_elements = new[] { Row<TKey,TData>(value), Row<TKey,TData>(value) }; Throws<ArgumentException>(() => group.GetData(), "duplicate keys preserve Dictionary.Add fault");
            group.m_elements = new DefinitionDataType<TKey,TData>.DefinitionElement<TData>[] { null }; Throws<NullReferenceException>(() => group.GetData(), "null row retains dereference fault");
            group.m_elements = new[] { Row<TKey,TData>(null) }; Throws<NullReferenceException>(() => group.GetData(), "null data is not skipped by getter");
            group.m_elements = null; Throws<NullReferenceException>(() => group.GetData(), "null array is not replaced with empty");
        }
        public static int RunManaged()
        {
            checks = 0;
            var intro = Raw<IntroSequenceDefinition>();
            Check((int)intro.IntroSequenceIdentifier == 0 && ReferenceEquals(intro.IntroSequence, null), "intro native field-zero defaults");
            var introRef = new AssetReferenceT<GameObject>("0123456789abcdef0123456789abcdef");
            Set(intro, "m_identifier", (IntroSequenceIdentifier)(-41)); Set(intro, "m_introSequence", introRef);
            Check((int)intro.IntroSequenceIdentifier == -41 && ReferenceEquals(intro.IntroSequence, introRef), "intro getters retain arbitrary enum and exact AA reference");
            Set(intro, "m_introSequence", null); Check(ReferenceEquals(intro.IntroSequence, null), "intro null reference not synthesized or loaded");
            var outro = Raw<OutroSequenceDefinition>();
            Check((int)outro.Identifier == 0 && ReferenceEquals(outro.OutroSequence, null), "outro native field-zero defaults");
            var outroRef = new AssetReferenceT<GameObject>("fedcba9876543210fedcba9876543210");
            Set(outro, "m_identifier", (OutroSequenceIdentifier)73); Set(outro, "m_outroSequence", outroRef);
            Check((int)outro.Identifier == 73 && ReferenceEquals(outro.OutroSequence, outroRef), "outro getters retain arbitrary enum and exact AA reference");
            Set(outro, "m_outroSequence", null); Check(ReferenceEquals(outro.OutroSequence, null), "outro null reference not synthesized or loaded");
            var meta = Raw<MetaGameUnlockDefinition>();
            Check((int)meta.Type == 0 && (int)meta.Title == 0 && (int)meta.Body == 0 && (int)meta.UnlockAudio == 0, "meta four native field-zero defaults");
            Set(meta, "m_type", (PlayerProgressionTypes)(-29)); Set(meta, "m_title", (Strings)31); Set(meta, "m_body", (Strings)(-32)); Set(meta, "m_unlockAudio", (HLAudioClipIdentifier)33);
            Check((int)meta.Type == -29 && (int)meta.Title == 31 && (int)meta.Body == -32 && (int)meta.UnlockAudio == 33, "meta distinct title/body/audio/type values not normalized or interchanged");
            Group(Raw<IntroSequenceDefinitionGroup>(), intro, (IntroSequenceIdentifier)(-41), (IntroSequenceIdentifier)42, () => Set(intro, "m_identifier", (IntroSequenceIdentifier)42), HardlightEnumComparers.IntroSequenceIdentifierComparer);
            Group(Raw<OutroSequenceDefinitionGroup>(), outro, (OutroSequenceIdentifier)73, (OutroSequenceIdentifier)(-74), () => Set(outro, "m_identifier", (OutroSequenceIdentifier)(-74)), HardlightEnumComparers.OutroSequenceIdentifierComparer);
            Group(Raw<MetaGameUnlockDefinitionGroup>(), meta, (PlayerProgressionTypes)(-29), (PlayerProgressionTypes)30, () => Set(meta, "m_type", (PlayerProgressionTypes)30), HardlightEnumComparers.PlayerProgressionTypesComparer);
            return checks;
        }
        public static int RunEngine()
        {
            checks = 0; var owned = new List<UnityEngine.Object>();
            try
            {
                var intro = ScriptableObject.CreateInstance<IntroSequenceDefinition>(); owned.Add(intro);
                var introCopy = ScriptableObject.CreateInstance<IntroSequenceDefinition>(); owned.Add(introCopy);
                var introGroup = ScriptableObject.CreateInstance<IntroSequenceDefinitionGroup>(); owned.Add(introGroup);
                var outro = ScriptableObject.CreateInstance<OutroSequenceDefinition>(); owned.Add(outro);
                var outroCopy = ScriptableObject.CreateInstance<OutroSequenceDefinition>(); owned.Add(outroCopy);
                var outroGroup = ScriptableObject.CreateInstance<OutroSequenceDefinitionGroup>(); owned.Add(outroGroup);
                var meta = ScriptableObject.CreateInstance<MetaGameUnlockDefinition>(); owned.Add(meta);
                var metaCopy = ScriptableObject.CreateInstance<MetaGameUnlockDefinition>(); owned.Add(metaCopy);
                var metaGroup = ScriptableObject.CreateInstance<MetaGameUnlockDefinitionGroup>(); owned.Add(metaGroup);
                Check(intro != null && introCopy != null && introGroup != null && outro != null && outroCopy != null && outroGroup != null && meta != null && metaCopy != null && metaGroup != null, "all nine real concrete definitions/groups create");
                Check((int)intro.IntroSequenceIdentifier == 0 && ReferenceEquals(intro.IntroSequence, null), "actual intro constructor defaults");
                Check((int)outro.Identifier == 0 && ReferenceEquals(outro.OutroSequence, null), "actual outro constructor defaults");
                Check((int)meta.Type == 0 && (int)meta.Title == 0 && (int)meta.Body == 0 && (int)meta.UnlockAudio == 0, "actual meta constructor defaults");
                Check(intro.GetGUID() == "" && outro.GetGUID() == "" && meta.GetGUID() == "", "actual original GUID base defaults retained");
                Check(ReferenceEquals(introGroup.m_elements, null) && ReferenceEquals(outroGroup.m_elements, null) && ReferenceEquals(metaGroup.m_elements, null), "actual group constructors leave inherited array null");
                intro.name = "Intro authored fixture"; outro.name = "Outro authored fixture"; meta.name = "Meta authored fixture";
                Set(intro, "m_identifier", (IntroSequenceIdentifier)(-91)); Set(intro, "m_introSequence", new AssetReferenceT<GameObject>("0123456789abcdef0123456789abcdef"));
                string introJson = JsonUtility.ToJson(intro); JsonUtility.FromJsonOverwrite(introJson, introCopy);
                Check((int)introCopy.IntroSequenceIdentifier == -91, "actual intro private identifier serializes");
                Check(introCopy.IntroSequence != null && introCopy.IntroSequence.AssetGUID == intro.IntroSequence.AssetGUID && !ReferenceEquals(introCopy.IntroSequence, intro.IntroSequence), "actual intro nested AA reference roundtrip without asset load");
                Check(introJson.Contains("m_identifier") && introJson.Contains("m_introSequence") && introJson.Contains("m_AssetGUID"), "actual intro JSON preserves own and original package field names");
                Set(outro, "m_identifier", (OutroSequenceIdentifier)92); Set(outro, "m_outroSequence", new AssetReferenceT<GameObject>("fedcba9876543210fedcba9876543210"));
                string outroJson = JsonUtility.ToJson(outro); JsonUtility.FromJsonOverwrite(outroJson, outroCopy);
                Check((int)outroCopy.Identifier == 92, "actual outro private identifier serializes");
                Check(outroCopy.OutroSequence != null && outroCopy.OutroSequence.AssetGUID == outro.OutroSequence.AssetGUID && !ReferenceEquals(outroCopy.OutroSequence, outro.OutroSequence), "actual outro nested AA reference roundtrip without asset load");
                Check(outroJson.Contains("m_identifier") && outroJson.Contains("m_outroSequence") && outroJson.Contains("m_AssetGUID"), "actual outro JSON preserves own and original package field names");
                Set(meta, "m_type", (PlayerProgressionTypes)(-93)); Set(meta, "m_title", (Strings)94); Set(meta, "m_body", (Strings)(-95)); Set(meta, "m_unlockAudio", (HLAudioClipIdentifier)96);
                string metaJson = JsonUtility.ToJson(meta); JsonUtility.FromJsonOverwrite(metaJson, metaCopy);
                Check((int)metaCopy.Type == -93 && (int)metaCopy.Title == 94 && (int)metaCopy.Body == -95 && (int)metaCopy.UnlockAudio == 96, "actual meta four private enums roundtrip distinctly");
                Check(metaJson.Contains("m_type") && metaJson.Contains("m_title") && metaJson.Contains("m_body") && metaJson.Contains("m_unlockAudio"), "actual meta exact private field names serialized");
                introGroup.m_elements = new[] { new DefinitionDataType<IntroSequenceIdentifier,IntroSequenceDefinition>.DefinitionElement<IntroSequenceDefinition>(introCopy) };
                outroGroup.m_elements = new[] { new DefinitionDataType<OutroSequenceIdentifier,OutroSequenceDefinition>.DefinitionElement<OutroSequenceDefinition>(outroCopy) };
                metaGroup.m_elements = new[] { new DefinitionDataType<PlayerProgressionTypes,MetaGameUnlockDefinition>.DefinitionElement<MetaGameUnlockDefinition>(metaCopy) };
                var intros = introGroup.GetData(); Check(ReferenceEquals(intros[(IntroSequenceIdentifier)(-91)], introCopy) && ReferenceEquals(intros.Comparer, HardlightEnumComparers.IntroSequenceIdentifierComparer), "actual intro dictionary exact key/comparer/reference");
                var outros = outroGroup.GetData(); Check(ReferenceEquals(outros[(OutroSequenceIdentifier)92], outroCopy) && ReferenceEquals(outros.Comparer, HardlightEnumComparers.OutroSequenceIdentifierComparer), "actual outro dictionary exact key/comparer/reference");
                var metas = metaGroup.GetData(); Check(ReferenceEquals(metas[(PlayerProgressionTypes)(-93)], metaCopy) && ReferenceEquals(metas.Comparer, HardlightEnumComparers.PlayerProgressionTypesComparer), "actual meta dictionary exact key/comparer/reference");
                Check(introGroup.m_elements[0].Name == introCopy.name && outroGroup.m_elements[0].Name == outroCopy.name && metaGroup.m_elements[0].Name == metaCopy.name, "actual inherited row constructor engine names");
                return checks;
            }
            finally
            { for (int i = owned.Count - 1; i >= 0; --i) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]); }
        }
        public static void Run()
        { int managed = RunManaged(); int engine = RunEngine(); Debug.Log("PASS original Intro/Outro/Meta definitions: " + managed + " managed + " + engine + " engine; supplied sequence assets and App startup unverified."); }
    }
}
