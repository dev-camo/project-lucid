using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Negative controls for the recovered-proof fixtures that borrow process-wide
    // state the registry snapshot does not cover: the OpString cache, the plugin
    // cloud notification list, a dormant SystemConfiguration row, the
    // GraphNameLookup table and the spline LUT cache. Each case plants foreign
    // state the fixture must neither depend on nor touch, runs the fixture's
    // registered entry point, and restores the planted state itself afterwards.
    // The Eggman logo case drives only the proof's release sequence (see there).
    public sealed class RecoveredFixtureBorrowedStateTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo OpStringInstance = typeof(OpString).GetField("instance", PrivateStatic);
        private static readonly FieldInfo OpStringThread = typeof(OpString).GetField("singletonThread", PrivateStatic);
        private static StringBuilder Builder(object value) => (StringBuilder)typeof(OpString).GetField("sb", Instance).GetValue(value);

        // A thread that has already finished: a foreign owner that can never read the cache.
        private static Thread FinishedThread()
        {
            var thread = new Thread(() => { });
            thread.Start();
            thread.Join();
            return thread;
        }

        // Installs a sentinel cache with the given owner, runs body, and checks the
        // sentinel's identity, owner and content survive; the real cache is put back
        // by raw reflection, independently of OpStringCacheScope.
        private static void WithSentinelCache(Thread owner, Action body)
        {
            RuntimeHelpers.RunClassConstructor(typeof(OpString).TypeHandle);
            object priorInstance = OpStringInstance.GetValue(null), priorThread = OpStringThread.GetValue(null);
            try
            {
                OpString sentinel = OpString.Create(1024) + "sentinel";
                StringBuilder builder = Builder(sentinel);
                OpStringInstance.SetValue(null, sentinel);
                OpStringThread.SetValue(null, owner);
                body();
                Assert.That(OpStringInstance.GetValue(null), Is.SameAs(sentinel), "cached instance restored");
                Assert.That(OpStringThread.GetValue(null), Is.SameAs(owner), "owner thread restored");
                Assert.That(Builder(sentinel), Is.SameAs(builder), "sentinel builder kept");
                Assert.That(builder.ToString(), Is.EqualTo("sentinel"), "sentinel content untouched");
            }
            finally
            {
                OpStringInstance.SetValue(null, priorInstance);
                OpStringThread.SetValue(null, priorThread);
            }
        }

        private static IEnumerable<int> Owners() => new[] { 0, 1, 2 };

        private static Thread Owner(int owner) => owner == 0 ? null : owner == 1 ? Thread.CurrentThread : FinishedThread();

        // Cold (no owner), current-thread and other-thread ownership.
        [TestCaseSource(nameof(Owners))]
        public void OpStringCacheScopeOwnsAFreshCacheAndRestoresEachPriorOwner(int owner)
        {
            WithSentinelCache(Owner(owner), () =>
            {
                using (var scope = new OpStringCacheScope())
                {
                    OpString cache = OpString.i;
                    Assert.That(cache, Is.SameAs(scope.Owned), "the scope's cache serves the first reader");
                    Assert.That(OpStringThread.GetValue(null), Is.SameAs(Thread.CurrentThread));
                    cache += "owned";
                    Assert.That(OpString.i.ToString(), Is.Empty, "owner reads still clear the owned cache");
                }
            });
        }

        // The ToDebugString and cloud-key fixtures each read OpString.i. Under another
        // thread's ownership their held-builder checks would read a fresh small builder.
        [TestCaseSource(nameof(Owners))]
        public void OpStringFixturesNeitherNeedNorTouchTheSharedCache(int owner)
        {
            WithSentinelCache(Owner(owner), () =>
            {
                Assert.That(Vector3FoundationVerification.Run(), Is.EqualTo(317));
                Assert.That(MovementFoundationVerification.Run(), Is.EqualTo(182));
                CloudFacadeVerification.Run();
            });
        }

        // The HLUnityCore extensions fixture's own SetUp, Format0PlaceholderTime case
        // and TearDown. Without its owned scope a cold or current-thread owner would
        // hand the sentinel to OpString.i, which clears its content.
        [TestCaseSource(nameof(Owners))]
        public void HLUnityCoreExtensionsFixtureNeitherNeedsNorTouchesTheSharedCache(int owner)
        {
            WithSentinelCache(Owner(owner), () =>
            {
                var fixture = new OriginalHLUnityCoreExtensionsTests();
                fixture.SetUp();
                try { fixture.Format0PlaceholderTimePadsAbsolutePartsWithOneLeadingSign(); }
                finally { fixture.TearDown(); }
            });
        }

        // ReceiveNativeNotification invokes the whole static m_onConnect list.
        [Test]
        public void LocalCloudNotificationsNeverReachEarlierSubscribers()
        {
            FieldInfo field = typeof(HLCloud.Plugin.Cloud).GetField("m_onConnect", PrivateStatic);
            object prior = field.GetValue(null);
            var reached = new List<string>();
            Action<string> earlier = message =>
            {
                reached.Add(message);
                throw new InvalidOperationException("earlier cloud subscriber ran");
            };
            try
            {
                field.SetValue(null, earlier);
                LocalCloudVerification.Run();
                Assert.That(reached, Is.Empty);
                Assert.That(field.GetValue(null), Is.SameAs(earlier), "the borrowed delegate is restored exactly");
            }
            finally { field.SetValue(null, prior); }
        }

        // IDictionary's own enumerator yields DictionaryEntry; Cast<> would see KeyValuePair.
        private static List<DictionaryEntry> Entries(IDictionary dictionary)
        {
            var entries = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
            return entries;
        }

        private static IDictionary Rows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", PrivateStatic).GetValue(null);

        // Runs body against a test-owned dormant SystemConfiguration row (no system),
        // with or without a startup observer, then puts the prior row and layout back.
        private static void WithDormantConfigurationRow(bool observed, Action<object, SystemRef, Func<int>> body)
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string name = ProcessManager.GetDefaultName<SystemConfiguration>();
            object retained = rows.Contains(name) ? rows[name] : null;
            int notifications = 0;
            try
            {
                rows.Remove(name);
                SystemRef reference = ProcessManager.GetSystemRef(name);
                if (observed) reference.OnSystemStartup += system => notifications++;
                body(rows[name], reference, () => notifications);
            }
            finally
            {
                rows.Remove(name);
                if (retained != null) rows[name] = retained;
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        // RegisterSystem into an existing row replaces its references and runs their
        // startup observers (0x06000e25); the time scheduler engine proof sets a dormant
        // row aside untouched and registers into a row of its own instead. The observer
        // must see no notification and the row, its reference and its typed entries keep
        // their identity, whether or not the row holds an observer.
        [TestCase(true)]
        [TestCase(false)]
        public void TimeSchedulerSetsADormantConfigurationRowAsideUntouched(bool observed)
        {
            WithDormantConfigurationRow(observed, (row, reference, notifications) =>
            {
                string name = ProcessManager.GetDefaultName<SystemConfiguration>();
                var typedCache = (IDictionary)row.GetType().GetField("SystemRefDictionary", Instance).GetValue(row);
                List<DictionaryEntry> typedEntries = Entries(typedCache);
                Assert.That(TimeSchedulerVerification.Run(), Is.EqualTo(532));
                Assert.That(notifications(), Is.Zero, "the dormant row's observer ran");
                Assert.That(Rows()[name], Is.SameAs(row));
                Assert.That(row.GetType().GetField("SystemRef", Instance).GetValue(row), Is.SameAs(reference));
                Assert.That(reference.IsNull(), Is.True, "the set-aside row never received a system");
                List<DictionaryEntry> after = Entries(typedCache);
                Assert.That(after.Count, Is.EqualTo(typedEntries.Count), "typed entries keep their count");
                for (int i = 0; i < after.Count; i++)
                {
                    Assert.That(after[i].Key, Is.SameAs(typedEntries[i].Key), "typed entry key " + i);
                    Assert.That(after[i].Value, Is.SameAs(typedEntries[i].Value), "typed entry reference " + i);
                }
            });
        }

        // Runs body with the named row absent, then removes whatever body left under
        // that name and puts the prior row and the layout back.
        private static void WithoutRow(string name, Action body)
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            bool had = rows.Contains(name);
            object retained = had ? rows[name] : null;
            try
            {
                rows.Remove(name);
                body();
            }
            finally
            {
                rows.Remove(name);
                if (had) rows[name] = retained;
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        // Fault injection: an observer the proof does not own is added to its temporary
        // configuration row, so that row's release is refused. The set-aside dormant row
        // must stay aside rather than overwrite the unreleased row (which would orphan the
        // observer), and each refusal is reported after the injected first failure.
        [Test]
        public void TimeSchedulerKeepsTheDormantRowAsideWhenItsTemporaryRowIsNotReleased()
        {
            WithDormantConfigurationRow(false, (row, reference, notifications) =>
            {
                string name = ProcessManager.GetDefaultName<SystemConfiguration>();
                var primary = new InvalidProgramException("injected after the engine checks");
                object temporary = null;
                SystemRef temporaryReference = null;
                int foreignNotifications = 0;
                VerificationFixtureOwnership.FaultInjection = reached =>
                {
                    if (reached != TimeSchedulerVerification.EngineChecksDoneSite) return;
                    temporary = Rows()[name];
                    temporaryReference = (SystemRef)temporary.GetType().GetField("SystemRef", Instance).GetValue(temporary);
                    temporaryReference.OnSystemStartup += system => foreignNotifications++;
                    // LogAssert matches expectations in queue order. The proof queues its own
                    // Reset-before-Awake exception before this site, so the cleanup warnings
                    // are queued here, after that exception and before the cleanup runs.
                    UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new Regex(
                        "^Fixture cleanup failed after an earlier failure: System.InvalidOperationException: Fixture registry row retains unowned state: " +
                        Regex.Escape(name) + "/OnSystemStartup"));
                    UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new Regex(
                        "^Fixture cleanup failed after an earlier failure: System.InvalidOperationException: Time scheduler kept the dormant configuration row aside"));
                    throw primary;
                };
                try
                {
                    var error = Assert.Throws<InvalidProgramException>(() => TimeSchedulerVerification.Run());
                    Assert.That(error, Is.SameAs(primary), "the injected failure stays the result");
                }
                finally
                {
                    VerificationFixtureOwnership.FaultInjection = null;
                }
                Assert.That(temporary, Is.Not.Null.And.Not.SameAs(row), "the proof registered into a row of its own");
                Assert.That(Rows()[name], Is.SameAs(temporary), "the unreleased temporary row is not overwritten");
                Assert.That(temporaryReference.IsNull(), Is.True, "the proof still unregistered its own configuration");
                Assert.That(foreignNotifications, Is.Zero);
                Assert.That(row.GetType().GetField("SystemRef", Instance).GetValue(row), Is.SameAs(reference));
                Assert.That(reference.IsNull(), Is.True, "the set-aside row never received a system");
            });
        }

        // Awake creates the TimeManager row when it is absent, and only its release removes
        // it again. The set-aside row and the captured layout are put back after both owned
        // rows are released, so the registry ends with its original rows, slots, free list
        // and version, with or without a dormant configuration row set aside.
        [TestCase(true)]
        [TestCase(false)]
        public void TimeSchedulerRestoresTheRegistryLayoutAfterCreatingTheTimeManagerRow(bool dormantConfiguration)
        {
            string timeManager = ProcessManager.GetDefaultName<TimeManager>();
            WithoutRow(timeManager, () =>
            {
                Action run = () =>
                {
                    IDictionary rows = Rows();
                    List<DictionaryEntry> entries = Entries(rows);
                    object[] layout = FSMManagerVerification.CaptureLayout(rows);
                    bool created = false;
                    VerificationFixtureOwnership.FaultInjection = reached =>
                    {
                        if (reached == TimeSchedulerVerification.EngineChecksDoneSite) created = rows.Contains(timeManager);
                    };
                    try { Assert.That(TimeSchedulerVerification.Run(), Is.EqualTo(532)); }
                    finally { VerificationFixtureOwnership.FaultInjection = null; }
                    Assert.That(created, Is.True, "Awake created the absent TimeManager row");
                    Assert.That(rows.Contains(timeManager), Is.False, "the created TimeManager row is released");
                    Assert.That(Entries(rows), Is.EqualTo(entries), "the original rows in their original order");
                    Assert.That(FSMManagerVerification.CaptureLayout(rows), Is.EqualTo(layout), "slots, free list and version are restored");
                };
                if (dormantConfiguration) WithDormantConfigurationRow(false, (row, reference, notifications) => run());
                else run();
            });
        }

        // The Eggman logo proof needs a GPU, so its release sequence is driven here
        // without one: a failing step must not stop the later releases. After a failed
        // proof the step failure is only logged; after a completed one the first step
        // failure propagates once every step has run. The whole-proof failure paths
        // remain covered only by the pinned-Editor GPU run.
        [TestCase(false)]
        [TestCase(true)]
        public void EggmanCleanUpRunsEveryReleaseStepAfterAFailingOne(bool completed)
        {
            MethodInfo cleanUp = typeof(EggmanLogoVerification).GetMethod("CleanUp", PrivateStatic);
            var ran = new List<string>();
            var first = new InvalidOperationException("first release step failed");
            var steps = new Action[]
            {
                () => { ran.Add("restore globals"); throw first; },
                () => { ran.Add("release targets"); throw new InvalidOperationException("second release step failed"); },
                () => ran.Add("destroy material"),
            };
            Exception thrown = null;
            try { cleanUp.Invoke(null, new object[] { completed, steps }); }
            catch (TargetInvocationException failure) { thrown = failure.InnerException; }
            Assert.That(ran, Is.EqualTo(new[] { "restore globals", "release targets", "destroy material" }));
            if (completed) Assert.That(thrown, Is.SameAs(first), "the first step failure propagates");
            else Assert.That(thrown, Is.Null, "the failed proof's own exception stays the result");
        }

        // The collision probes need an empty table: the same two names registered
        // earlier in the reverse order would swap their probe IDs.
        [Test]
        public void FSMPrimitivesNeitherNeedNorChangeEarlierGraphNames()
        {
            var table = (Dictionary<int, string>)typeof(GraphNameLookup).GetField("s_nameLookup", PrivateStatic).GetValue(null);
            var prior = new List<KeyValuePair<int, string>>(table);
            try
            {
                int reversedFirst = GraphNameLookup.ConvertNameToId("Lucid-recovery-collision-Ł");
                int reversedSecond = GraphNameLookup.ConvertNameToId("Lucid-recovery-collision-A");
                Assert.That(reversedSecond, Is.EqualTo(unchecked(reversedFirst + 1)), "the planted pair occupies the probe IDs in reverse");
                var planted = new List<KeyValuePair<int, string>>(table);

                FSMPrimitivesVerification.Run();

                Assert.That(new List<KeyValuePair<int, string>>(table), Is.EquivalentTo(planted), "the table holds exactly its earlier names");
                Assert.That(GraphNameLookup.LookupNameUsingId(reversedFirst), Is.EqualTo("Lucid-recovery-collision-Ł"));
            }
            finally
            {
                table.Clear();
                foreach (KeyValuePair<int, string> entry in prior) table.Add(entry.Key, entry.Value);
            }
        }

        // Genuine knot-length recalculation writes the shared LUT for the rest of the
        // domain; the spline proof's initializer check must not depend on it.
        [Test]
        public void SplineProofDoesNotNeedAPristineLutCache()
        {
            Type component = typeof(SplineLocation).Assembly.GetType("Hardlight.SplineRuntimeComponent", true);
            var cache = (float[])component.GetField("s_lutCache", PrivateStatic).GetValue(null);
            float[] prior = (float[])cache.Clone();
            try
            {
                cache[5] = 1.5f;
                Assert.That(ProjectLucid.Editor.SplineSharedRuntimeVerification.RunManaged(), Is.EqualTo(201));
                Assert.That(cache[5], Is.EqualTo(1.5f), "the proof leaves the shared cache as it found it");
            }
            finally { Array.Copy(prior, cache, prior.Length); }
        }
    }
}
