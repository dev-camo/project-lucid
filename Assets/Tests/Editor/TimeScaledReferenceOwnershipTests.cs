using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class TimeScaledReferenceOwnershipTests
    {
        private const string Name = "owned retention control";
        private static object NewRow() => Activator.CreateInstance(typeof(ProcessManager)
            .GetNestedType("SystemInfo", BindingFlags.NonPublic), new object[] { new SystemRef(Name, null) });
        private static IDictionary Cache(object row) => (IDictionary)row.GetType().GetField("SystemRefDictionary").GetValue(row);
        private static object Entry(string name) => new SystemRef<TimeManager>(name, null);
        private static object[] Keys(IDictionary map) => map.Keys.Cast<object>().ToArray();

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitRetentionPreservesExactEntryAndReleasesOnlyTemporaryEntries(bool borrowed)
        {
            var registry = new Hashtable();
            object info = NewRow(); IDictionary cache = Cache(info);
            object first = Entry("first"), second = Entry("second"), permanent = Entry("permanent");
            if (borrowed) { cache.Add("first", first); cache.Add("second", second); registry.Add(Name, info); }
            var lease = new VerificationFixtureOwnership.LifecycleRow(registry, Name);
            if (!borrowed) registry.Add(Name, info);
            cache.Add("permanent", permanent);
            lease.RetainTypedEntry("permanent", permanent);
            cache.Add("temporary", Entry("temporary"));
            lease.Dispose();
            Assert.That(registry[Name], Is.SameAs(info));
            Assert.That(Cache(info), Is.SameAs(cache));
            Assert.That(cache["permanent"], Is.SameAs(permanent));
            CollectionAssert.AreEqual(borrowed ? new[] { "first", "second", "permanent" } : new[] { "permanent" }, Keys(cache));
            if (borrowed) { Assert.That(cache["first"], Is.SameAs(first)); Assert.That(cache["second"], Is.SameAs(second)); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoRetentionKeepsTheOriginalTemporaryLeaseBehavior(bool borrowed)
        {
            var registry = new Hashtable(); object info = NewRow(); IDictionary cache = Cache(info);
            object prior = Entry("prior");
            if (borrowed) { cache.Add("prior", prior); registry.Add(Name, info); }
            var lease = new VerificationFixtureOwnership.LifecycleRow(registry, Name);
            if (!borrowed) registry.Add(Name, info);
            cache.Add("temporary", Entry("temporary")); lease.Dispose();
            if (borrowed)
            {
                Assert.That(registry[Name], Is.SameAs(info));
                CollectionAssert.AreEqual(new[] { "prior" }, Keys(cache));
                Assert.That(cache["prior"], Is.SameAs(prior));
            }
            else Assert.That(registry.Contains(Name), Is.False);
        }

        [TestCase("missing")]
        [TestCase("row")]
        [TestCase("entry")]
        [TestCase("order")]
        [TestCase("retained-order")]
        [TestCase("observer")]
        public void RetainedOwnershipChangesFailWithoutRepair(string change)
        {
            var registry = new Hashtable(); object info = NewRow(); IDictionary cache = Cache(info);
            object a = Entry("a"), b = Entry("b"), retained = Entry("retained");
            cache.Add("a", a); cache.Add("b", b);
            // The missing-row case exercises the originally absent branch.
            if (change != "missing") registry.Add(Name, info);
            var lease = new VerificationFixtureOwnership.LifecycleRow(registry, Name);
            if (change == "missing") registry.Add(Name, info);
            cache.Add("retained", retained); lease.RetainTypedEntry("retained", retained);
            object replacement = NewRow(); int calls = 0;
            Action<TimeManager> observer = _ => ++calls;
            var reference = (SystemRef<TimeManager>)retained;
            if (change == "missing") registry.Remove(Name);
            if (change == "row") registry[Name] = replacement;
            if (change == "entry") cache["retained"] = Entry("replacement");
            if (change == "order") { cache.Clear(); cache.Add("b", b); cache.Add("a", a); cache.Add("retained", retained); }
            if (change == "retained-order") { cache.Clear(); cache.Add("retained", retained); cache.Add("a", a); cache.Add("b", b); }
            if (change == "observer") reference.OnSystemStartup += observer;
            object actualRow = registry[Name], actualEntry = cache["retained"];
            object[] actualOrder = Keys(cache);
            try
            {
                Assert.Throws<InvalidOperationException>(() => lease.Dispose());
                Assert.That(registry[Name], Is.SameAs(actualRow));
                Assert.That(cache["retained"], Is.SameAs(actualEntry));
                CollectionAssert.AreEqual(actualOrder, Keys(cache));
                Assert.That(calls, Is.Zero);
            }
            finally { if (change == "observer") reference.OnSystemStartup -= observer; }
        }

        [Test]
        public void PromotionRejectsWrongIdentityAndReorderedPriorEntries()
        {
            var registry = new Hashtable(); object info = NewRow(); IDictionary cache = Cache(info);
            object a = Entry("a"), b = Entry("b"), retained = Entry("retained");
            cache.Add("a", a); cache.Add("b", b); registry.Add(Name, info);
            var lease = new VerificationFixtureOwnership.LifecycleRow(registry, Name);
            cache.Add("retained", retained);
            StringAssert.Contains("not attached", Assert.Throws<InvalidOperationException>(() => lease.RetainTypedEntry("retained", Entry("other"))).Message);
            cache.Clear(); cache.Add("b", b); cache.Add("a", a); cache.Add("retained", retained);
            StringAssert.Contains("order changed", Assert.Throws<InvalidOperationException>(() => lease.RetainTypedEntry("retained", retained)).Message);
            CollectionAssert.AreEqual(new[] { "b", "a", "retained" }, Keys(cache));
            Assert.That(cache["retained"], Is.SameAs(retained));
        }

        [Test]
        public void LaterPromotionRejectsReorderedPreviouslyRetainedEntries()
        {
            var registry = new Hashtable(); object info = NewRow(); IDictionary cache = Cache(info);
            object prior = Entry("prior"), retained = Entry("retained"), next = Entry("next");
            cache.Add("prior", prior); registry.Add(Name, info);
            var lease = new VerificationFixtureOwnership.LifecycleRow(registry, Name);
            cache.Add("retained", retained); lease.RetainTypedEntry("retained", retained);
            cache.Clear(); cache.Add("retained", retained); cache.Add("prior", prior); cache.Add("next", next);
            StringAssert.Contains("retained typed order changed",
                Assert.Throws<InvalidOperationException>(() => lease.RetainTypedEntry("next", next)).Message);
            CollectionAssert.AreEqual(new[] { "retained", "prior", "next" }, Keys(cache));
            Assert.That(cache["retained"], Is.SameAs(retained));
        }

        [Test]
        public void NestedActualConstructionPreservesTheSameReferenceThroughBothScopes()
        {
            int entryScopes = TimeScaledReferenceLease.ActiveCount;
            TimeScaledReferenceLease outer = null, inner = null;
            GameObject host = null; object original = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                outer = new TimeScaledReferenceLease();
                inner = new TimeScaledReferenceLease();
                host = new GameObject("nested real time-scaled descendant"); host.SetActive(false);
                EnemyAbilityTestSupport.AddTimeScaled<TimeScaledShader>(host);
                original = typeof(TimeScaledComponent).GetField("m_timeManagerRef", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Assert.That(ProcessManager.GetSystemRef<TimeManager>(), Is.SameAs(original));
                Assert.That(TimeScaledReferenceLease.ActiveCount, Is.EqualTo(entryScopes + 2));
            }, () => { if (host != null) UnityEngine.Object.DestroyImmediate(host); },
                () => inner?.Dispose(), () => outer?.Dispose());
            Assert.That(TimeScaledReferenceLease.ActiveCount, Is.EqualTo(entryScopes));
            Assert.That(ProcessManager.GetSystemRef<TimeManager>(), Is.SameAs(original));
        }

        [Test]
        public void FailureBeforeComponentPublicationRestoresActiveScopesAndRegistry()
        {
            int entryScopes = TimeScaledReferenceLease.ActiveCount;
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            var primary = new InvalidOperationException("before real component publication");
            GameObject host = null; TimeScaledReferenceLease lease = null;
            Exception actual = Assert.Throws<InvalidOperationException>(() => VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                host = new GameObject("failed time-scaled construction"); host.SetActive(false);
                lease = new TimeScaledReferenceLease();
                TimeScaledReferenceLease.Construct<TimeScaledShader>(host, () => { throw primary; });
            }, () => { if (host != null) UnityEngine.Object.DestroyImmediate(host); }, () => lease?.Dispose()));
            Assert.That(actual, Is.SameAs(primary));
            Assert.That(TimeScaledReferenceLease.ActiveCount, Is.EqualTo(entryScopes));
            Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty);
        }
        [Test]
        public void AdmissionAndDisposalFailuresReleaseEveryActiveScopeWithoutRemovingObservers()
        {
            int entryScopes = TimeScaledReferenceLease.ActiveCount;
            TimeScaledReferenceLease lease = null; GameObject host = null;
            SystemRef<TimeManager> original = null; int calls = 0;
            Action<TimeManager> observer = _ => ++calls;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                lease = new TimeScaledReferenceLease();
                host = new GameObject("observed retained reference"); host.SetActive(false);
                EnemyAbilityTestSupport.AddTimeScaled<TimeScaledShader>(host);
                original = ProcessManager.GetSystemRef<TimeManager>();
                original.OnSystemStartup += observer;
                SharedStateSnapshot withObserver = SharedStateSnapshot.Capture();
                Assert.Throws<InvalidOperationException>(() => new TimeScaledReferenceLease());
                Assert.That(TimeScaledReferenceLease.ActiveCount, Is.EqualTo(entryScopes + 1));
                Assert.Throws<InvalidOperationException>(() => lease.Dispose());
                Assert.That(TimeScaledReferenceLease.ActiveCount, Is.EqualTo(entryScopes));
                Assert.That(SharedStateSnapshot.Capture().Differences(withObserver), Is.Empty);
                Assert.That(ProcessManager.GetSystemRef<TimeManager>(), Is.SameAs(original));
                Assert.That(calls, Is.Zero);
            }, () => { if (original != null) original.OnSystemStartup -= observer; },
                () => { if (host != null) UnityEngine.Object.DestroyImmediate(host); }, () => lease?.Dispose());
            Assert.That(TimeScaledReferenceLease.ActiveCount, Is.EqualTo(entryScopes));
        }

        [Test]
        public void AmbiguousPublicationRetainsAndReportsThroughEveryEnclosingScope()
        {
            int entryScopes = TimeScaledReferenceLease.ActiveCount;
            TimeScaledReferenceLease outer = null, inner = null; GameObject host = null;
            object original = null;
            LogAssert.Expect(LogType.Warning, new Regex("Fixture cleanup failed after an earlier failure:.*Unqualified time-scaled reference", RegexOptions.Singleline));
            LogAssert.Expect(LogType.Warning, new Regex("Fixture cleanup failed after an earlier failure:.*Unqualified time-scaled reference", RegexOptions.Singleline));
            Exception error = Assert.Throws<InvalidOperationException>(() => VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                outer = new TimeScaledReferenceLease(); inner = new TimeScaledReferenceLease();
                host = new GameObject("ambiguous real descendant publication"); host.SetActive(false);
                // The inner calls establish genuine components and the original reference;
                // the outer construction still rejects two published components as ambiguous.
                TimeScaledReferenceLease.Construct<TimeScaledShader>(host, () =>
                {
                    TimeScaledShader first = EnemyAbilityTestSupport.AddTimeScaled<TimeScaledShader>(host);
                    EnemyAbilityTestSupport.AddTimeScaled<TimeScaledShader>(host);
                    original = ProcessManager.GetSystemRef<TimeManager>();
                    return first;
                });
            }, () => { if (host != null) UnityEngine.Object.DestroyImmediate(host); },
                () => inner?.Dispose(), () => outer?.Dispose()));
            StringAssert.Contains("unqualified time-scaled reference", error.Message);
            Assert.That(TimeScaledReferenceLease.ActiveCount, Is.EqualTo(entryScopes));
            Assert.That(original, Is.Not.Null);
            Assert.That(ProcessManager.GetSystemRef<TimeManager>(), Is.SameAs(original));
        }

    }
}
