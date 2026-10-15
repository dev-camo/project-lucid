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

namespace ProjectLucid.Tests
{
    // Actual Unity Awake dispatch of the original ReverseOrderOnLanguage (Awake 0x1a365b0 tail-calls
    // Evaluate 0x1a365c0). The string table is a genuine owned StringTable: its absent registry row is
    // leased, Unity's own activation runs the original MonoSingleton Awake, and the component stays
    // disabled so Start (configuration, property store and language subscription) never runs.
    // No Language object is used: GetLanguageAsInt falls back to EnglishUS.
    public sealed class OriginalReverseOrderOnLanguagePlayTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void Invoke(object target, string method, object argument)
        {
            try { typeof(StringTable).GetMethod(method, Members).Invoke(target, new[] { argument }); }
            catch (TargetInvocationException failure) when (failure.InnerException != null) { throw failure.InnerException; }
        }

        private static string[] Order(Transform parent) => Enumerable.Range(0, parent.childCount).Select(i => parent.GetChild(i).name).ToArray();

        private static void WithOwnedStringTable(bool rightToLeft, Action body)
        {
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            string name = ProcessManager.GetDefaultName<StringTable>();
            Assert.That(ReferenceEquals(MonoSingleton<StringTable>.Instance, null), "The fixture refuses a borrowed StringTable singleton.");
            Assert.That(ReferenceEquals(MonoSingleton<Language>.Instance, null), "The fixture refuses a borrowed Language singleton; it relies on the EnglishUS fallback.");
            var foreignRows = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in registry) foreignRows.Add(entry.Key, entry.Value);
            var lease = new LifecycleSystemRowLease(registry, name);
            var owner = new GameObject("Lucid owned string table");
            owner.SetActive(false);
            StringTable table = owner.AddComponent<StringTable>();
            table.enabled = false;
            try
            {
                owner.SetActive(true);
                Assert.That(MonoSingleton<StringTable>.Instance, Is.SameAs(table), "Unity dispatched the original MonoSingleton Awake.");
                lease.CaptureCreatedRow(ProcessManager.GetSystemRef(name));
                Invoke(table, "InternalSetSupportedLanguages", new[] { new SupportedLanguage { Language = (int)Languages.EnglishUS, IsRTL = rightToLeft } });
                Invoke(table, "InternalSetStrings", new Dictionary<int, StringTable.StringEntry>
                {
                    { (int)Strings.NUMBER_SEPARATOR, new StringTable.StringEntry("NUMBER_SEPARATOR", ",", 0) },
                    { (int)Strings.DECIMAL_SEPARATOR, new StringTable.StringEntry("DECIMAL_SEPARATOR", ".", 0) },
                });
                body();
            }
            finally
            {
                // Unity dispatched Awake, so destroying the object runs the original OnDestroy, which
                // unregisters the table and clears Instance; the lease then removes the cleared row.
                Object.DestroyImmediate(owner);
                lease.Dispose();
            }
            Assert.That(lease.CleanupFailures, Is.Empty);
            Assert.That(ReferenceEquals(MonoSingleton<StringTable>.Instance, null));
            Assert.That(registry.Count, Is.EqualTo(foreignRows.Count), "No owned registry row remains.");
            foreach (KeyValuePair<object, object> entry in foreignRows)
                Assert.That(registry[entry.Key], Is.SameAs(entry.Value), "Foreign row " + entry.Key + " is unchanged.");
        }

        private static void ActivateReverser(string[] children, string[] expected)
        {
            var host = new GameObject("Lucid reverse host");
            try
            {
                host.SetActive(false);
                foreach (string child in children) new GameObject(child).transform.SetParent(host.transform, false);
                host.AddComponent<ReverseOrderOnLanguage>();
                Assert.That(Order(host.transform), Is.EqualTo(children), "nothing runs before activation");
                host.SetActive(true);
                Assert.That(Order(host.transform), Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(host); }
        }

        // Children are configured before activation; activation must run Awake -> Evaluate and reverse
        // exactly once. Catches a removed Awake-to-Evaluate call and a doubled reversal.
        [Test]
        public void ActivationAwakeReversesRightToLeftChildrenOnce()
        {
            WithOwnedStringTable(true, () => ActivateReverser(new[] { "a", "b", "c", "d", "e" }, new[] { "e", "d", "c", "b", "a" }));
        }

        // Left-to-right companion, so the case above cannot pass by an unconditional reversal.
        [Test]
        public void ActivationAwakeLeavesLeftToRightChildren()
        {
            WithOwnedStringTable(false, () => ActivateReverser(new[] { "f", "g", "h" }, new[] { "f", "g", "h" }));
        }
    }
}
