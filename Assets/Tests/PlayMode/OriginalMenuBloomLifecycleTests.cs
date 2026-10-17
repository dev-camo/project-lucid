using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class OriginalMenuBloomLifecycleTests
    {
        private static readonly FieldInfo Enabled = typeof(MenuBloomTransition).GetField(
            "m_enabled", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static readonly FieldInfo Stalled = typeof(MenuBloomTransition).GetField(
            "m_stalled", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        [UnityTest]
        public IEnumerator NonpositiveAndNaNDurationsCompleteSynchronouslyWithoutFading()
        {
            using (var owned = new OwnedBloom())
            {
                foreach (float duration in new[] { 0f, -0f, -1f, float.NaN })
                {
                    owned.Group.alpha = 0.375f;
                    owned.Bloom.StartTransition(duration);
                    Assert.That(owned.Group.alpha, Is.EqualTo(0f));
                    Assert.That(owned.IsEnabled, Is.False);
                    Assert.That(owned.IsStalled, Is.False);
                }
                yield return null;
                Assert.That(owned.Group.alpha, Is.EqualTo(0f));
            }
        }

        [UnityTest]
        public IEnumerator StallRetainsEntryAlphaRejectsReentryAndFinishOnlyReleasesIt()
        {
            using (var owned = new OwnedBloom())
            {
                owned.Group.alpha = 0.375f;
                owned.Bloom.StartTransition(-1f, true);
                Assert.That(owned.IsEnabled && owned.IsStalled, Is.True);
                Assert.That(owned.Group.alpha, Is.EqualTo(0.375f));
                yield return null;
                yield return null;
                Assert.That(owned.Group.alpha, Is.EqualTo(0.375f));
                owned.Bloom.StartTransition(0f);
                Assert.That(owned.IsEnabled && owned.IsStalled, Is.True);
                Assert.That(owned.Group.alpha, Is.EqualTo(0.375f));
                owned.Bloom.FinishTransition();
                Assert.That(owned.IsEnabled, Is.True);
                Assert.That(owned.IsStalled, Is.False);
                Assert.That(owned.Group.alpha, Is.EqualTo(0.375f));
                yield return null;
                yield return null;
                Assert.That(owned.IsEnabled, Is.False);
                Assert.That(owned.Group.alpha, Is.EqualTo(0f));
                owned.Bloom.StartTransition(float.NaN);
                Assert.That(owned.IsEnabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator PositiveFadeHoldsLastRisingSampleAndCompletesAfterRelease()
        {
            using (var owned = new OwnedBloom())
            {
                yield return null;
                Assert.That(Time.timeScale, Is.GreaterThan(0f), "scaled time must already advance");
                Assert.That(Time.deltaTime, Is.GreaterThan(0f));
                float halfDuration = Mathf.Max(0.08f, Time.deltaTime * 2.25f);
                owned.Group.alpha = 0.375f;
                owned.Bloom.StartTransition(halfDuration, true);
                Assert.That(owned.Group.alpha, Is.EqualTo(0f), "the first sample occurs before the first yield");
                Assert.That(owned.IsEnabled && owned.IsStalled, Is.True);
                // Rising loop of <DoTransition>d__7.MoveNext (x86-64 0x1b61090,
                // 0x1b61112..0x1b6116a): set alpha = clamp01(t / half) * maxAlpha, then
                // t += Time.deltaTime, then yield, while t < half. Mirror t from this
                // frame's delta and each later frame's delta; the held alpha is the last
                // t below half. Catches adding delta before sampling or another curve.
                float rising = Time.deltaTime, lastRising = 0f;
                float risingDeadline = Time.realtimeSinceStartup + 10f;
                while (rising < halfDuration)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(risingDeadline), "rising fade reaches its half duration");
                    lastRising = rising;
                    yield return null;
                    rising += Time.deltaTime;
                }
                yield return null;
                yield return null;
                float held = owned.Group.alpha;
                Assert.That(held, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f),
                    "the stall does not force the peak or change the last strict rising sample");
                Assert.That(held, Is.EqualTo(Mathf.Lerp(0f, 1f, lastRising / halfDuration)),
                    "the held alpha is the exact last rising sample");
                yield return null;
                yield return null;
                Assert.That(owned.Group.alpha, Is.EqualTo(held));
                Assert.That(owned.IsEnabled && owned.IsStalled, Is.True);
                owned.Bloom.FinishTransition();
                Assert.That(owned.Group.alpha, Is.EqualTo(held));
                Assert.That(owned.IsStalled, Is.False);
                float deadline = Time.realtimeSinceStartup + 10f;
                while (owned.IsEnabled)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "falling fade completes");
                    Assert.That(owned.Group.alpha, Is.InRange(0f, 1f));
                    yield return null;
                }
                Assert.That(owned.Group.alpha, Is.EqualTo(0f));
                owned.Bloom.StartTransition(0f);
                Assert.That(owned.IsEnabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator StoppingTheOwnedCoroutineDoesNotInventCancellationCleanup()
        {
            using (var owned = new OwnedBloom())
            {
                owned.Group.alpha = 0.375f;
                owned.Bloom.StartTransition(-1f, true);
                owned.Bloom.StopAllCoroutines();
                yield return null;
                Assert.That(owned.IsEnabled && owned.IsStalled, Is.True);
                owned.Bloom.FinishTransition();
                Assert.That(owned.IsEnabled, Is.True);
                Assert.That(owned.IsStalled, Is.False);
                owned.Bloom.StartTransition(0f);
                yield return null;
                Assert.That(owned.IsEnabled, Is.True);
                Assert.That(owned.Group.alpha, Is.EqualTo(0.375f));
            }
        }

        // Wiring control: OwnedBloom admits the shared row before activation,
        // so a dormant bloom row with a foreign startup observer is refused
        // before the singleton's Awake could register into it and run it.
        [Test]
        public void OwnedBloomRefusesAnObservedDormantRowBeforeActivation()
        {
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            string name = ProcessManager.GetDefaultName<MenuBloomTransition>();
            LifecycleSystemRowLease seed = AdmitBloomRow(registry);
            SystemRef dormant = null;
            int calls = 0;
            Action<ISystem> observer = value => ++calls;
            try
            {
                dormant = ProcessManager.GetSystemRef(name);
                seed.CaptureCreatedRow(dormant);
                dormant.OnSystemStartup += observer;
                object row = registry[name];
                StringAssert.Contains("retains unowned state", Assert.Throws<InvalidOperationException>(() => new OwnedBloom()).Message);
                Assert.AreSame(row, registry[name]);
                Assert.IsTrue(dormant.IsNull());
                Assert.That(MonoSingleton<MenuBloomTransition>.Instance == null, Is.True);
                Assert.AreEqual(0, calls, "No activation registered into the seeded row.");
            }
            finally
            {
                if (dormant != null) dormant.OnSystemStartup -= observer;
                seed.Dispose();
            }
            Assert.IsEmpty(seed.CleanupFailures);
        }

        // Field-only admission of the bloom row before activation: a null row
        // can still hold foreign observers and deferred work that the
        // singleton's registration would run and consume.
        internal static LifecycleSystemRowLease AdmitBloomRow(IDictionary registry) =>
            LifecycleSystemRowLease.AdmitAbsentOrIdle(registry, ProcessManager.GetDefaultName<MenuBloomTransition>());

        private sealed class OwnedBloom : IDisposable
        {
            private readonly LifecycleSystemRowLease row;
            private GameObject host;
            public MenuBloomTransition Bloom { get; private set; }
            public CanvasGroup Group { get; private set; }
            public bool IsEnabled { get { return (bool)Enabled.GetValue(Bloom); } }
            public bool IsStalled { get { return (bool)Stalled.GetValue(Bloom); } }

            public OwnedBloom()
            {
                row = AdmitBloomRow(LifecycleSystemRowLease.SharedRegistry);
                try
                {
                    Assert.That(ProcessManager.IsSystemNull<MenuBloomTransition>(), Is.True,
                        "the real registry must have no prior bloom; no reset is permitted");
                    Assert.That(MonoSingleton<MenuBloomTransition>.Instance == null, Is.True);
                    host = new GameObject("Project Lucid owned scheduled bloom fixture");
                    Bloom = host.AddComponent<MenuBloomTransition>();
                    row.CaptureRowIfCreated();
                    Group = host.GetComponent<CanvasGroup>();
                    Assert.That(Group, Is.Not.Null);
                    Assert.That(MonoSingleton<MenuBloomTransition>.Instance, Is.SameAs(Bloom));
                    Assert.That(ProcessManager.IsSystemNull<MenuBloomTransition>(), Is.False);
                    Assert.That(IsEnabled || IsStalled, Is.False);
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                row.RunCleanup("capture-created-row", row.CaptureRowIfCreated);
                row.RunCleanup("destroy-bloom-host", () => { if (host != null) Object.DestroyImmediate(host); });
                row.Dispose();
                Assert.That(MonoSingleton<MenuBloomTransition>.Instance == null, Is.True);
                Assert.That(ProcessManager.IsSystemNull<MenuBloomTransition>(), Is.True,
                    "normal original OnDestroy must unregister only the owned component");
                Assert.IsEmpty(row.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
                Assert.IsEmpty(row.DiagnosticFailures, "Fixture cleanup reporting failed; diagnostic failures remain retained.");
            }
        }
    }
}
