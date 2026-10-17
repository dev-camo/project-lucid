using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine;
using Owned = ProjectLucid.Verification.OriginalPerformanceDependencyVerification.OwnedDependencies;

namespace ProjectLucid.Tests
{
    public class OriginalPerformanceDependencyPreservationTests
    {
        [Test]
        public void CopyPreservesAliasesAndDestinationCallbacks()
        { Assert.That(OriginalPerformanceDependencyVerification.RunCopyOwned20(), Is.EqualTo(20)); }

        [Test]
        public void DelegateAccessorsPreserveDuplicatesAndCapturedOrder()
        { Assert.That(OriginalPerformanceDependencyVerification.RunDelegateAccessorsOwned18(), Is.EqualTo(18)); }

        [Test]
        public void ComparisonsPreserveThresholdsAndMissingFeatureFallback()
        { Assert.That(OriginalPerformanceDependencyVerification.RunComparisonsEngine44(), Is.EqualTo(44)); }

        [Test]
        public void ProfileCallbacksPreserveReentryAndFaultPrefixes()
        { Assert.That(OriginalPerformanceDependencyVerification.RunCallbacksAndEventsEngine22(), Is.EqualTo(22)); }

        [Test]
        public void ProfileSubscriptionsPreserveDuplicateAndFaultState()
        { Assert.That(OriginalPerformanceDependencyVerification.RunProfileSubscriptionsEngine18(), Is.EqualTo(18)); }

        // Failure controls for the owned-component cleanup only; they make no
        // original-behavior claim. Every host and the registry sentinel are
        // created by the control itself, so cleanup touches no foreign state.
        private sealed class InjectedFailure : Exception
        {
            public InjectedFailure(string message) : base(message) { }
        }

        private sealed class Marker : ISystem { }

        private static int Run(Owned owned, Func<Owned, int> body) => OriginalPerformanceDependencyVerification.RunOwned(owned, body);

        private static bool AllDestroyed(List<GameObject> hosts) => hosts.TrueForAll(host => host == null);

        [Test]
        public void OwnedGroupBodyFailureStaysPrimaryAndReleasesEveryHost()
        {
            var hosts = new List<GameObject>();
            var body = new InjectedFailure("body");
            var thrown = Assert.Throws<InjectedFailure>(() => Run(new Owned(hosts.Add), owned =>
            {
                owned.Create();
                owned.Create();
                throw body;
            }));
            Assert.AreSame(body, thrown, "The body failure is rethrown itself when cleanup succeeds.");
            Assert.AreEqual(2, hosts.Count);
            Assert.IsTrue(AllDestroyed(hosts));
        }

        [Test]
        public void OwnedGroupSecondHostAcquisitionFailureStaysPrimaryAndReleasesBothHosts()
        {
            var hosts = new List<GameObject>();
            var acquisition = new InjectedFailure("second host acquisition");
            bool bodyCompleted = false;
            var thrown = Assert.Throws<InjectedFailure>(() => Run(new Owned(host =>
            {
                hosts.Add(host);
                if (hosts.Count == 2) throw acquisition;
            }), owned =>
            {
                owned.Create();
                owned.Create();
                bodyCompleted = true;
                return 0;
            }));
            Assert.AreSame(acquisition, thrown);
            Assert.IsFalse(bodyCompleted);
            Assert.AreEqual(2, hosts.Count, "The second host is tracked before its failing setup.");
            Assert.IsTrue(AllDestroyed(hosts));
        }

        [Test]
        public void OwnedGroupBodyFailureStaysFirstAheadOfReleaseFailures()
        {
            var hosts = new List<GameObject>();
            var body = new InjectedFailure("body");
            var release = new InjectedFailure("first release");
            var thrown = Assert.Throws<AggregateException>(() => Run(new Owned(hosts.Add, host =>
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (ReferenceEquals(host, hosts[0])) throw release;
            }), owned =>
            {
                owned.Create();
                owned.Create();
                throw body;
            }));
            Assert.AreEqual(2, thrown.InnerExceptions.Count);
            Assert.AreSame(body, thrown.InnerExceptions[0], "The primary failure is first.");
            Assert.AreSame(release, thrown.InnerExceptions[1]);
            Assert.IsTrue(AllDestroyed(hosts), "A throwing release does not stop the later one.");
        }

        [Test]
        public void OwnedGroupThrowingReleaseStillReleasesLaterHostsAndRunsTheRegistryCheck()
        {
            IDictionary registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string name = "ProjectLucid.PerformanceCleanupSentinel." + Guid.NewGuid().ToString("N");
            var sentinel = new Marker();
            SystemRef reference = null;
            object row = null;
            var hosts = new List<GameObject>();
            var release = new InjectedFailure("first release");
            try
            {
                // The body's owned sentinel row is a registry change, so the
                // check must report it after the throwing first release.
                var thrown = Assert.Throws<AggregateException>(() => Run(new Owned(hosts.Add, host =>
                {
                    UnityEngine.Object.DestroyImmediate(host);
                    if (ReferenceEquals(host, hosts[0])) throw release;
                }), owned =>
                {
                    owned.Create();
                    owned.Create();
                    reference = ProcessManager.RegisterSystem(sentinel, name);
                    row = registry[name];
                    return 0;
                }));
                Assert.AreEqual(2, thrown.InnerExceptions.Count);
                Assert.AreSame(release, thrown.InnerExceptions[0]);
                Assert.IsInstanceOf<InvalidOperationException>(thrown.InnerExceptions[1]);
                Assert.AreEqual("Performance preservation changed registry row count", thrown.InnerExceptions[1].Message);
                Assert.IsTrue(AllDestroyed(hosts));
            }
            finally
            {
                if (reference != null)
                {
                    ProcessManager.UnregisterSystem(sentinel);
                    if (ReferenceEquals(registry[name], row) && reference.IsNull()) registry.Remove(name);
                }
            }
        }
    }
}
