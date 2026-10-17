using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Controls for SharedStateSnapshot's reporting. Each change is made on
    // state this test owns and undone before it ends.
    public sealed class SharedStateSnapshotTests
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
            bool typedQueued = false;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                SharedStateSnapshot before = SharedStateSnapshot.Capture();
                untyped.OnSystemShutdown += observer;
                StringAssert.Contains("changed system row '" + name + "' untyped reference OnSystemShutdown observers", Change(before));
                untyped.OnSystemShutdown -= observer;
                Assert.IsNull(Change(before));

                typed.InvokeOnValid(typedObserver);
                typedQueued = true;
                StringAssert.Contains("changed system row '" + name + "' typed reference '" + typeof(Marker) + "' deferred InvokeOnValid work", Change(before));
                ProcessManager.RegisterSystem(marker, name);
                Assert.AreEqual(1, calls);
                StringAssert.Contains("changed system row '" + name + "' untyped reference", Change(before));
                ProcessManager.UnregisterSystem(name);
                Assert.IsNull(Change(before));
            },
            () => untyped.OnSystemShutdown -= observer,
            // A failure before the registration leaves the typed work queued on this
            // owned row; registering the owned marker delivers it once, so the row is idle.
            () => { if (typedQueued && calls == 0) ProcessManager.RegisterSystem(marker, name); },
            () => ProcessManager.UnregisterSystem(name),
            () => VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row));
        }

        // SubscribeToAction creates an absent row and unsubscribing keeps it, so an
        // unsubscribe alone still leaves a change for the guard. The control sets
        // any borrowed AppShutdown row aside to start from an absent one, and puts
        // the same row object back.
        [Test]
        public void ActionSubscriptionAndAKeptEmptyRowAreReported()
        {
            var lookup = VerificationFixtureOwnership.ActionRows.SharedActionLookup;
            lookup.TryGetValue(SystemAction.AppShutdown, out Dictionary<ISystem, Action<object>> aside);
            lookup.Remove(SystemAction.AppShutdown);
            try
            {
                var marker = new Marker();
                SharedStateSnapshot before = SharedStateSnapshot.Capture();
                var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.AppShutdown);
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    marker.SubscribeToAction(SystemAction.AppShutdown, _ => { });
                    StringAssert.Contains("added action row AppShutdown subscribers", Change(before));
                    marker.UnsubscribeFromAction(SystemAction.AppShutdown);
                    Assert.IsTrue(lookup.TryGetValue(SystemAction.AppShutdown, out Dictionary<ISystem, Action<object>> kept) && kept.Count == 0,
                        "unsubscribing keeps the created empty row");
                    Assert.AreEqual("added action row AppShutdown; added action row AppShutdown subscribers", Change(before));
                },
                () => marker.UnsubscribeFromAction(SystemAction.AppShutdown),
                actionRows.Dispose);
                Assert.IsNull(Change(before));
            }
            finally
            {
                if (aside != null) lookup[SystemAction.AppShutdown] = aside;
            }
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
    }
}
