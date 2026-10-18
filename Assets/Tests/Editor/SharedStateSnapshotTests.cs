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
                StringAssert.Contains("changed HardlightProject.HardlightEnumExtensions.LevelSetupTypesEnumToString entries", Change(before));
            }
            Assert.IsNull(Change(before));
        }

        // One control per newly captured owner. Each probe is outside the native Add rows
        // of its .cctor (0x19E4410, 0x19E6200, 0x19DD6C0, 0x19F1640), so GetString caches it.
        [Test]
        public void CachedMissInEachHLAutoGeneratedTableOwnerIsReported()
        {
            Type core = typeof(BuildFeatures).Assembly.GetType("Hardlight.HardlightEnumExtensions", true);
            CheckCachedMiss(core, "GameInputEnumToString", () => ((GameInput)0x1F).GetString(),
                "Hardlight.HardlightEnumExtensions.GameInputEnumToString");
            CheckCachedMiss(typeof(Hardlight.HardlightEnumExtentions), "HLAudioTypesEnumToString", () => ((HLAudioTypes)0x1F).GetString(),
                "Hardlight.HardlightEnumExtentions.HLAudioTypesEnumToString");
            CheckCachedMiss(typeof(global::Enums.EnumExtentions), "ErrorTypesEnumToString",
                () => global::Enums.EnumExtentions.GetString((global::Enums.ErrorTypes)0x1F),
                "Enums.EnumExtentions.ErrorTypesEnumToString");
            CheckCachedMiss(typeof(HardlightProject.HardlightEnumExtentions), "CharacterStatsEnumToString", () => ((CharacterStats)0x1F).GetString(),
                "HardlightProject.HardlightEnumExtentions.CharacterStatsEnumToString");
        }

        // The remaining owners. 0x1F is outside the Add rows of each .cctor or table
        // builder: Analytics 0x19E7540 (0, 0xeca8303d, 0xbb963fb6, 0x3e270050),
        // GenerateDefaultLanguagesEnumStrings 0x19E66E0, HLUnityCore EnumExtensions
        // 0x1B1B1A0 (0x9869c05, 0x4f5a935c, 0xb6fa1284, 0x97220d01) and HLUnityUI
        // 0x1B4FFD0, so GetString formats and caches it.
        [Test]
        public void CachedMissInEachRemainingEnumTableOwnerIsReported()
        {
            CheckCachedMiss(typeof(Hardlight.Analytics.HardlightEnumExtensions), "AnalyticsConsentStateEnumToString",
                () => Hardlight.Analytics.HardlightEnumExtensions.GetString((Hardlight.Analytics.AnalyticsConsentState)0x1F),
                "Hardlight.Analytics.HardlightEnumExtensions.AnalyticsConsentStateEnumToString");
            CheckCachedMiss(typeof(Hardlight.Enums.EnumExtentions), "LanguagesEnumToString",
                () => Hardlight.Enums.EnumExtentions.GetString((Hardlight.Enums.Languages)0x1F),
                "Hardlight.Enums.EnumExtentions.LanguagesEnumToString");
            CheckCachedMiss(typeof(EnumExtensions), "UpdateOnEnumToString", () => ((UpdateOn)0x1F).GetString(),
                "Hardlight.EnumExtensions.UpdateOnEnumToString");
            CheckCachedMiss(typeof(HardlightUIEnumComparers).Assembly.GetType("Hardlight.HardlightEnumExtensions", true),
                "UIInputTypeEnumToString", () => ((UIInputType)0x1F).GetString(),
                "Hardlight.HardlightEnumExtensions.UIInputTypeEnumToString");
        }

        private static void CheckCachedMiss(Type owner, string field, Func<string> getString, string path)
        {
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            using (new VerificationFixtureOwnership.StaticDictionaryLease(owner, field))
            {
                Assert.AreEqual("0x0000001F", getString(), path);
                Assert.AreEqual("changed " + path + " entries", Change(before));
            }
            Assert.IsNull(Change(before), path);
        }

        [Test]
        public void CloudConnectSubscriberIsReportedUntilRemoved()
        {
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            Action<string> subscriber = message => { };
            HLCloud.Plugin.Cloud.SubscribeOnConnect(subscriber);
            try
            {
                StringAssert.Contains("changed HLCloud.Plugin.Cloud.m_onConnect", Change(before));
            }
            finally { HLCloud.Plugin.Cloud.UnsubscribeOnConnect(subscriber); }
            Assert.IsNull(Change(before));
        }

        [Test]
        public void ReplacedOpStringCacheIsReportedUntilRestored()
        {
            SharedStateSnapshot before = SharedStateSnapshot.Capture();
            using (new OpStringCacheScope())
                StringAssert.Contains("changed OpString.instance", Change(before));
            Assert.IsNull(Change(before));
        }
    }
}
