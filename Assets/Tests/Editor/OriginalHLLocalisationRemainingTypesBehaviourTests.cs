using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SupportedLanguage = ClientDataAPI.SupportedLanguage;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using StringTable = Hardlight.Localisation.StringTable;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Behaviour of OptionallyLocalisedString's table route, ReverseOrderOnLanguage.Evaluate and the
    // FontAssetLanguageFallbackData getters from the original 1.10.1 HLLocalisation.Runtime.
    // The string table is a genuine owned StringTable: its row is acquired through the identity-checked
    // VerificationFixtureOwnership.RegistryRow by the original MonoSingleton Awake, and its data goes in
    // through the original InternalSetSupportedLanguages/InternalSetStrings. No Language object is used:
    // Language.GetLanguageAsInt falls back to EnglishUS when no Language instance exists.
    public sealed class OriginalHLLocalisationRemainingTypesBehaviourTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        private static void Invoke(Type declaring, object target, string method, params object[] arguments)
        {
            try { declaring.GetMethod(method, Members).Invoke(target, arguments); }
            catch (TargetInvocationException failure) when (failure.InnerException != null) { throw failure.InnerException; }
        }

        private static Strings Id(string key) => (Strings)HLCRC32.GenerateInt(key);

        private static StringTable.StringEntry Entry(string id, string content) => new StringTable.StringEntry(id, content, 0);

        private static SupportedLanguage[] Supported(Languages language, bool rightToLeft) =>
            new[] { new SupportedLanguage { Language = (int)language, IsRTL = rightToLeft } };

        // A genuine owned StringTable for the duration of one case. Construction refuses borrowed
        // state before any mutation; Dispose removes only what it created and proves that every
        // foreign registry row, the property-store handlers and the language event are unchanged.
        private sealed class OwnedStringTable : IDisposable
        {
            private static readonly IDictionary Registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            private readonly string name = ProcessManager.GetDefaultName<StringTable>();
            private readonly Dictionary<object, object> foreignRows = new Dictionary<object, object>();
            private readonly object loadHandlers, saveHandlers, languageEvent;
            private readonly VerificationFixtureOwnership.RegistryRow row;
            private readonly GameObject owner;
            private bool awoken;
            internal readonly StringTable Table;

            internal OwnedStringTable(SupportedLanguage[] languages, Dictionary<int, StringTable.StringEntry> strings)
            {
                Assert.That(ReferenceEquals(MonoSingleton<StringTable>.Instance, null), "The fixture refuses a borrowed StringTable singleton.");
                Assert.That(ReferenceEquals(MonoSingleton<Language>.Instance, null), "The fixture refuses a borrowed Language singleton; it relies on the EnglishUS fallback.");
                foreach (DictionaryEntry entry in Registry) foreignRows.Add(entry.Key, entry.Value);
                loadHandlers = StaticField(typeof(HLPropertyStore), "LoadHandlers");
                saveHandlers = StaticField(typeof(HLPropertyStore), "SaveHandlers");
                languageEvent = StaticField(typeof(Language), "OnLanguageChanged");
                row = new VerificationFixtureOwnership.RegistryRow(name);
                owner = new GameObject("Lucid owned string table");
                owner.SetActive(false);
                Table = owner.AddComponent<StringTable>();
                try
                {
                    // Edit Mode never dispatches Awake here, so the original MonoSingleton Awake
                    // (0x06000e09) is invoked once: it registers this table and publishes Instance.
                    row.RegisterUsing(Table, () => { awoken = true; Invoke(typeof(MonoSingleton<StringTable>), Table, "Awake"); });
                    Assert.That(MonoSingleton<StringTable>.Instance, Is.SameAs(Table));
                    Invoke(typeof(StringTable), Table, "InternalSetSupportedLanguages", new object[] { languages });
                    Invoke(typeof(StringTable), Table, "InternalSetStrings", strings);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private static object StaticField(Type type, string field) =>
                type.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);

            public void Dispose()
            {
                var failures = new List<Exception>();
                void Step(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                // The original StringTable.OnDestroy (0x06000059) removes handlers it never added, then
                // MonoSingleton.OnDestroy unregisters this table and clears Instance.
                if (awoken) Step(() => Invoke(typeof(StringTable), Table, "OnDestroy"));
                Step(() => row.Dispose());
                Step(() => Object.DestroyImmediate(owner));
                Step(() => Assert.That(ReferenceEquals(MonoSingleton<StringTable>.Instance, null), "Instance is cleared."));
                Step(() =>
                {
                    Assert.That(Registry.Count, Is.EqualTo(foreignRows.Count), "No owned registry row remains.");
                    foreach (KeyValuePair<object, object> entry in foreignRows)
                        Assert.That(Registry[entry.Key], Is.SameAs(entry.Value), "Foreign row " + entry.Key + " is unchanged.");
                });
                Step(() => Assert.That(StaticField(typeof(HLPropertyStore), "LoadHandlers"), Is.SameAs(loadHandlers)));
                Step(() => Assert.That(StaticField(typeof(HLPropertyStore), "SaveHandlers"), Is.SameAs(saveHandlers)));
                Step(() => Assert.That(StaticField(typeof(Language), "OnLanguageChanged"), Is.SameAs(languageEvent)));
                if (failures.Count == 1) throw failures[0];
                if (failures.Count > 1) throw new AggregateException(failures);
            }
        }

        // InternalSetStrings (0x06000062) feeds both separators to NumberFormatInfo, and an empty
        // decimal separator throws, so every owned table carries them.
        private static Dictionary<int, StringTable.StringEntry> Table(params KeyValuePair<Strings, string>[] entries)
        {
            var strings = new Dictionary<int, StringTable.StringEntry>
            {
                { (int)Strings.NUMBER_SEPARATOR, Entry("NUMBER_SEPARATOR", ",") },
                { (int)Strings.DECIMAL_SEPARATOR, Entry("DECIMAL_SEPARATOR", ".") },
            };
            foreach (KeyValuePair<Strings, string> entry in entries) strings.Add((int)entry.Key, Entry(entry.Key.ToString(), entry.Value));
            return strings;
        }

        private static KeyValuePair<Strings, string> Row(Strings id, string content) => new KeyValuePair<Strings, string>(id, content);

        // ToString (0x1a363f0) looks table content up through StringTable.GetString. Both constructors:
        // ctor(string, true) hashes its key with HLCRC32 and ctor(Strings) stores the id; neither stores
        // content, so a lookup bypass would return null instead of the owned entry.
        [Test]
        public void TableContentToStringReturnsTheOwnedStringTableEntryForBothConstructors()
        {
            using (new OwnedStringTable(Supported(Languages.EnglishUS, false),
                Table(Row(Id("Lucid owned table key"), "Lucid hashed entry"), Row(Strings.MISSING, "Lucid MISSING entry"))))
            {
                Assert.That(new OptionallyLocalisedString("Lucid owned table key", true).ToString(), Is.EqualTo("Lucid hashed entry"));
                Assert.That(new OptionallyLocalisedString(Strings.MISSING).ToString(), Is.EqualTo("Lucid MISSING entry"));
            }
        }

        // The looked-up table text is the format string; nested parameters (one literal, one table)
        // are converted before String.Format. Catches formatting skipped on the table route.
        [Test]
        public void TableContentFormatsTheLookedUpTextWithNestedParameters()
        {
            using (new OwnedStringTable(Supported(Languages.EnglishUS, false),
                Table(Row(Id("Lucid format key"), "{0} of {1}"), Row(Strings.CULTURE_NUMBER_THOUSANDS, "five"))))
            {
                var text = new OptionallyLocalisedString("Lucid format key", true);
                text.AddParameter("3", false);
                text.AddParameter(new OptionallyLocalisedString(Strings.CULTURE_NUMBER_THOUSANDS));
                Assert.That(text.ToString(), Is.EqualTo("3 of five"));
            }
        }

        // ToString catches only FormatException from formatting and returns the looked-up text.
        // Catches an uncaught exception and a fallback to content or empty text.
        [Test]
        public void TableFormatMismatchKeepsTheLookedUpText()
        {
            using (new OwnedStringTable(Supported(Languages.EnglishUS, false), Table(Row(Id("Lucid mismatch key"), "{0} {1} {2}"))))
            {
                var text = new OptionallyLocalisedString("Lucid mismatch key", true);
                text.AddParameter("only one", false);
                Assert.That(text.ToString(), Is.EqualTo("{0} {1} {2}"));
            }
        }

        // HasContent (0x1a36080) compares a table id with NONE (0xb6fa1284) and never looks it up:
        // true for a missing entry and for an empty entry, false for NONE. Catches HasContent
        // derived from the lookup result or from the literal content field.
        [Test]
        public void TableHasContentDependsOnTheIdNotTheLookup()
        {
            using (new OwnedStringTable(Supported(Languages.EnglishUS, false), Table(Row(Id("Lucid empty key"), string.Empty))))
            {
                var missing = new OptionallyLocalisedString("Lucid absent key", true);
                var empty = new OptionallyLocalisedString("Lucid empty key", true);
                Assert.That(missing.ToString(), Is.Empty, "a missing entry looks up as empty");
                Assert.That(empty.ToString(), Is.Empty);
                Assert.That(missing.HasContent(), Is.True);
                Assert.That(empty.HasContent(), Is.True);
                Assert.That(new OptionallyLocalisedString(Strings.NONE).HasContent(), Is.False);
            }
        }

        private Transform Parent(params string[] children)
        {
            var parent = new GameObject("Lucid reverse parent");
            parent.SetActive(false);
            owned.Add(parent);
            foreach (string child in children) new GameObject(child).transform.SetParent(parent.transform, false);
            return parent.transform;
        }

        private static string[] Order(Transform parent) => Enumerable.Range(0, parent.childCount).Select(i => parent.GetChild(i).name).ToArray();

        private ReverseOrderOnLanguage Reverser(Transform parent) => parent.gameObject.AddComponent<ReverseOrderOnLanguage>();

        // Evaluate (0x1a365c0) returns when GetCurrentLanguageData is null. With no Language instance the
        // current language is EnglishUS, and this owned table supports only Japanese. Catches an
        // unconditional reversal.
        [Test]
        public void EvaluateLeavesChildrenWhenNoSupportedLanguageMatches()
        {
            using (new OwnedStringTable(Supported(Languages.Japanese, true), Table()))
            {
                Transform parent = Parent("a", "b", "c");
                Invoke(typeof(ReverseOrderOnLanguage), Reverser(parent), "Evaluate");
                Assert.That(Order(parent), Is.EqualTo(new[] { "a", "b", "c" }));
            }
        }

        // A matching left-to-right language leaves the order. Catches an inverted IsRTL gate.
        [Test]
        public void EvaluateLeavesChildrenForALeftToRightLanguage()
        {
            using (new OwnedStringTable(Supported(Languages.EnglishUS, false), Table()))
            {
                Transform parent = Parent("d", "e", "f", "g");
                Invoke(typeof(ReverseOrderOnLanguage), Reverser(parent), "Evaluate");
                Assert.That(Order(parent), Is.EqualTo(new[] { "d", "e", "f", "g" }));
            }
        }

        // A matching right-to-left language reverses once. Catches a suppressed or doubled reversal.
        [Test]
        public void EvaluateReversesChildrenForARightToLeftLanguage()
        {
            using (new OwnedStringTable(Supported(Languages.EnglishUS, true), Table()))
            {
                Transform parent = Parent("h", "i", "j", "k", "l");
                Invoke(typeof(ReverseOrderOnLanguage), Reverser(parent), "Evaluate");
                Assert.That(Order(parent), Is.EqualTo(new[] { "l", "k", "j", "i", "h" }));
            }
        }

        // ReverseChildren (0x1a36660) moves child 0 to childCount - i for i = 1..childCount-1.
        // Catches off-by-one loop bounds at the small and odd sizes.
        [Test]
        public void ReverseChildrenHandlesZeroOneThreeFourAndFiveChildren()
        {
            foreach (int count in new[] { 0, 1, 3, 4, 5 })
            {
                string[] names = Enumerable.Range(0, count).Select(i => "child" + i).ToArray();
                Transform parent = Parent(names);
                Invoke(typeof(ReverseOrderOnLanguage), Reverser(parent), "ReverseChildren");
                Assert.That(Order(parent), Is.EqualTo(names.Reverse().ToArray()), count + " children");
            }
        }

        // The getters (0x1a33d70/0x1a33d80) return their serialized backing lists. Catches a copy or null.
        [Test]
        public void FallbackDataGettersReturnTheirSerializedBackingLists()
        {
            var data = ScriptableObject.CreateInstance<FontAssetLanguageFallbackData>();
            owned.Add(data);
            var assets = new List<FontAssetLanguageFallbackData.FontAssetAndFallBacks>();
            var all = new List<FontAssetLanguageFallbackData.AllLanguageFallBacks>();
            typeof(FontAssetLanguageFallbackData).GetField("m_fontAssetsAndFallBackCollection", Members).SetValue(data, assets);
            typeof(FontAssetLanguageFallbackData).GetField("m_allLanguageFallBacks", Members).SetValue(data, all);
            Assert.That(data.FontAssetsAndFallBackCollection, Is.SameAs(assets));
            Assert.That(data.AllLangaugeFallBacksCollection, Is.SameAs(all));
        }
    }
}
