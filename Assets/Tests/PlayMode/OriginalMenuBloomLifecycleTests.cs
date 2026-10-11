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
                yield return new WaitForSeconds(halfDuration * 2f + 0.1f);
                float held = owned.Group.alpha;
                Assert.That(held, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f),
                    "the stall does not force the peak or change the last strict rising sample");
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

        private sealed class OwnedBloom : IDisposable
        {
            private GameObject host;
            public MenuBloomTransition Bloom { get; private set; }
            public CanvasGroup Group { get; private set; }
            public bool IsEnabled { get { return (bool)Enabled.GetValue(Bloom); } }
            public bool IsStalled { get { return (bool)Stalled.GetValue(Bloom); } }

            public OwnedBloom()
            {
                Assert.That(ProcessManager.IsSystemNull<MenuBloomTransition>(), Is.True,
                    "the real registry must have no prior bloom; no reset is permitted");
                Assert.That(MonoSingleton<MenuBloomTransition>.Instance == null, Is.True);
                try
                {
                    host = new GameObject("Project Lucid owned scheduled bloom fixture");
                    Bloom = host.AddComponent<MenuBloomTransition>();
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
                if (host != null) Object.DestroyImmediate(host);
                Assert.That(MonoSingleton<MenuBloomTransition>.Instance == null, Is.True);
                Assert.That(ProcessManager.IsSystemNull<MenuBloomTransition>(), Is.True,
                    "normal original OnDestroy must unregister only the owned component");
            }
        }
    }
}
