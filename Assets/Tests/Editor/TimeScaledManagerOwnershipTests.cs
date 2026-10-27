using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TimeScaledTestHarness;

namespace ProjectLucid.Tests
{
    public sealed class TimeScaledManagerOwnershipTests
    {
        private static SystemRef<TimeManager> OriginalAfterConstruction() => (SystemRef<TimeManager>)typeof(TimeScaledComponent)
            .GetField("m_timeManagerRef", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        [Test]
        public void ActualDescendantRetainsOriginalAssociationAfterOrderedManagerTeardown()
        {
            SystemRef<TimeManager> original = null; TimeManager manager = null; GameObject host = null;
            var order = new List<string>();
            WithManager(scene =>
            {
                manager = scene.Manager; host = scene.Owned.Host("owned association descendant", false);
                Add<TimeScaledShader>(scene, host);
                original = OriginalAfterConstruction();
                Assert.That(original, Is.SameAs(ProcessManager.GetSystemRef<TimeManager>()));
                Assert.That(original.GetSafe(), Is.SameAs(manager));
                scene.Owned.Lease(new ReleaseAction(() =>
                {
                    Assert.That(host == null, Is.True, "descendant was destroyed before unregistration");
                    Assert.That(manager != null, Is.True, "manager outlives descendants");
                    Assert.That(original.GetSafe(), Is.SameAs(manager)); order.Add("descendants");
                }));
            });
            order.Add("returned");
            CollectionAssert.AreEqual(new[] { "descendants", "returned" }, order);
            Assert.That(manager == null, Is.True);
            Assert.That(OriginalAfterConstruction(), Is.SameAs(original));
            Assert.That(ProcessManager.GetSystemRef<TimeManager>(), Is.SameAs(original));
            Assert.That(original.IsNull(), Is.True);
        }

        [Test]
        public void FailureAfterRealComponentPublicationPreservesPrimaryAndPermanentReference()
        {
            var primary = new InvalidOperationException("component publication failure");
            SystemRef<TimeManager> original = null; TimeManager manager = null; GameObject host = null;
            Exception actual = Assert.Throws<InvalidOperationException>(() => WithManager(scene =>
            {
                manager = scene.Manager; host = scene.Owned.Host("owned partial component", false);
                scene.ManagerLease.Construct<TimeScaledShader>(host, () =>
                {
                    EnemyAbilityTestSupport.AddTimeScaled<TimeScaledShader>(host);
                    original = OriginalAfterConstruction(); throw primary;
                });
            }));
            Assert.That(actual, Is.SameAs(primary));
            Assert.That(host == null && manager == null, Is.True);
            Assert.That(original, Is.Not.Null);
            Assert.That(OriginalAfterConstruction(), Is.SameAs(original));
            Assert.That(ProcessManager.GetSystemRef<TimeManager>(), Is.SameAs(original));
            Assert.That(original.IsNull(), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CleanupFailureStillUnregistersAndDestroysManager(bool bodyFailure)
        {
            var primary = new InvalidOperationException("time-scaled primary");
            var cleanup = new InvalidOperationException("time-scaled cleanup");
            TimeManager manager = null; SystemRef<TimeManager> original = null;
            Exception error = Assert.Throws<InvalidOperationException>(() => WithManager(scene =>
            {
                manager = scene.Manager;
                Add<TimeScaledShader>(scene, scene.Owned.Host("owned cleanup descendant", false));
                original = OriginalAfterConstruction();
                // Native construction logs Reset before the later cleanup warning.
                if (bodyFailure) LogAssert.Expect(LogType.Warning, new Regex("Fixture cleanup failed after an earlier failure:.*time-scaled cleanup", RegexOptions.Singleline));
                scene.Owned.Lease(new ReleaseAction(() => { throw cleanup; }));
                if (bodyFailure) throw primary;
            }));
            Assert.That(error, Is.SameAs(bodyFailure ? primary : cleanup));
            Assert.That(manager == null, Is.True);
            Assert.That(original.IsNull(), Is.True);
            Assert.That(ProcessManager.GetSystemRef<TimeManager>(), Is.SameAs(original));
        }

        [Test]
        public void NoDescendantLeavesAdmittedRegistryUnchangedWithoutRegistration()
        {
            SharedStateSnapshot before = SharedStateSnapshot.Capture(); TimeManager manager = null;
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Debug.Log("No-descendant manager entry observed: " + (registry.Contains(typeof(TimeManager).FullName) ? "borrowed row" : "absent row"));
            WithManager(scene =>
            {
                manager = scene.Manager;
                Assert.That(manager != null, Is.True);
                Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty,
                    "constructing the owned manager alone must not register it or initialize the descendant reference");
            });
            Assert.That(manager == null, Is.True);
            Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty);
        }

        [Test]
        public void RegistrationFailureAfterPublicationUnregistersItsExactManager()
        {
            var primary = new InvalidOperationException("registration publication failure");
            TimeManager manager = null;
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            Exception error = Assert.Throws<InvalidOperationException>(() => WithManager(scene =>
            {
                manager = scene.Manager;
                scene.ManagerLease.RegisterManager(() => { throw primary; });
            }));
            Assert.That(error, Is.SameAs(primary));
            Assert.That(manager == null, Is.True);
            Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty);
        }

        [Test]
        public void BorrowedRegistryObserversAreRejectedWithoutInvocationOrRemoval()
        {
            string name = typeof(TimeManager).FullName;
            var row = new VerificationFixtureOwnership.LifecycleRow(name);
            SystemRef reference = null; int calls = 0;
            Action<ISystem> observer = value => ++calls;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                reference = ProcessManager.GetSystemRef(name);
                reference.OnSystemStartup += observer;
                SharedStateSnapshot before = SharedStateSnapshot.Capture();
                Exception failure = Assert.Throws<InvalidOperationException>(() => WithManager(scene => Assert.Fail("refused admission must not call body")));
                StringAssert.Contains("retains unowned state", failure.Message);
                Assert.That(calls, Is.Zero);
                Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty);
            }, () => { if (reference != null) reference.OnSystemStartup -= observer; }, row.Dispose);
        }
    }
}
