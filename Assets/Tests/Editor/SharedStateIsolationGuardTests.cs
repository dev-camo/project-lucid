using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace ProjectLucid.Tests
{
    // Controls for the assembly-wide SharedStateIsolation guard. Each change is
    // made on state this test owns and undone before it ends, so the guard
    // around the test itself sees nothing.
    public sealed class SharedStateIsolationGuardTests
    {
        private sealed class Marker : ISystem { }

        private static IDictionary Registry =>
            (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static string Change(SharedStateSnapshot before) => SharedStateSnapshot.Capture().DifferenceFrom(before);

        [Test]
        public void UnchangedStateHasNoDifference()
        {
            Assert.IsNull(Change(SharedStateSnapshot.Capture()));
        }

        // An idle row the test creates and removes passes; left behind it is reported.
        [Test]
        public void CreatedRowIsReportedUntilTheTestRemovesIt()
        {
            string name = "ProjectLucid.GuardSentinel." + Guid.NewGuid().ToString("N");
            VerificationFixtureOwnership.RequireAbsent(Registry, name);
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            ProcessManager.GetSystemRef<Marker>(name);
            object row = Registry[name];
            try
            {
                StringAssert.Contains("added system row '" + name + "'", Change(before));
            }
            finally
            {
                VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row);
            }
            Assert.IsNull(Change(before));
        }

        [Test]
        public void ObserversDeferredWorkAndSystemsOnAnExistingRowAreReported()
        {
            string name = "ProjectLucid.GuardObserved." + Guid.NewGuid().ToString("N");
            VerificationFixtureOwnership.RequireAbsent(Registry, name);
            SystemRef untyped = ProcessManager.GetSystemRef(name);
            SystemRef<Marker> typed = ProcessManager.GetSystemRef<Marker>(name);
            object row = Registry[name];
            var marker = new Marker();
            int calls = 0;
            Action<ISystem> observer = _ => calls++;
            Action<Marker> typedObserver = _ => calls++;
            try
            {
                SharedStateSnapshot before = SharedStateSnapshot.Capture();
                untyped.OnSystemShutdown += observer;
                StringAssert.Contains("changed system row '" + name + "' untyped reference OnSystemShutdown observers", Change(before));
                untyped.OnSystemShutdown -= observer;
                Assert.IsNull(Change(before));

                typed.InvokeOnValid(typedObserver);
                StringAssert.Contains("changed system row '" + name + "' typed reference '" + typeof(Marker) + "' deferred InvokeOnValid work", Change(before));
                ProcessManager.RegisterSystem(marker, name);
                Assert.AreEqual(1, calls);
                StringAssert.Contains("changed system row '" + name + "' untyped reference", Change(before));
                ProcessManager.UnregisterSystem(name);
                Assert.IsNull(Change(before));
            }
            finally
            {
                untyped.OnSystemShutdown -= observer;
                ProcessManager.UnregisterSystem(name);
            }
            VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row);
        }

        // SubscribeToAction creates an absent row and unsubscribing keeps it, so an
        // unsubscribe alone still leaves a change for the guard.
        [Test]
        public void ActionSubscriptionAndAKeptEmptyRowAreReported()
        {
            var marker = new Marker();
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            using (new VerificationFixtureOwnership.ActionRows(SystemAction.AppShutdown))
            {
                marker.SubscribeToAction(SystemAction.AppShutdown, _ => { });
                StringAssert.Contains("action row AppShutdown", Change(before));
                marker.UnsubscribeFromAction(SystemAction.AppShutdown);
            }
            Assert.IsNull(Change(before));
        }

        [Test]
        public void CachedEnumMissIsReported()
        {
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            using (new VerificationFixtureOwnership.StaticDictionaryLease(typeof(HardlightProject.HardlightEnumExtensions),"LevelSetupTypesEnumToString"))
            {
                // 0x1234 is not a native LevelSetupTypes key, so the original GetString caches it.
                Assert.AreEqual("0x00001234", ((LevelSetupTypes)0x1234).GetString());
                StringAssert.Contains("changed HardlightEnumExtensions.LevelSetupTypesEnumToString entries", Change(before));
            }
            Assert.IsNull(Change(before));
        }

        [Test]
        public void GuardFailsTheTestThatLeavesAChangeAndNamesIt()
        {
            ITest current = TestExecutionContext.CurrentContext.CurrentTest;
            var guard = new SharedStateIsolationAttribute();
            Assert.AreEqual(ActionTargets.Test, guard.Targets);
            string name = "ProjectLucid.GuardLeft." + Guid.NewGuid().ToString("N");
            VerificationFixtureOwnership.RequireAbsent(Registry, name);
            guard.BeforeTest(current);
            ProcessManager.GetSystemRef(name);
            object row = Registry[name];
            try
            {
                var failure = Assert.Throws<AssertionException>(() => guard.AfterTest(current));
                StringAssert.Contains("Test left shared state changed: added system row '" + name + "'", failure.Message);
            }
            finally
            {
                VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row);
            }
            guard.BeforeTest(current);
            Assert.DoesNotThrow(() => guard.AfterTest(current));
        }
    }
}
