#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Corpus = ProjectLucid.TransformRotatorAuthoredCorpus;
using Proof = ProjectLucid.TransformRotatorBindingVerification;

namespace ProjectLucid.Tests
{
    public sealed class TransformRotatorAuthoredTests
    {
        [Test]
        public void EveryAuthoredDocumentRetainsOriginalValuesAndReferencesAcrossInactiveRoundtrip()
        { Assert.That(Proof.RunAuthored(), Is.EqualTo(9656)); }

        [Test]
        public void PreparedAssetsHaveTheExactVerifiedTransformRotatorBinding()
        { Proof.CheckBinding(); }

        [Test]
        public void OriginalLookupMissLeavesNullAcrossRealInactiveRoundtrip()
        { Proof.RunMissingKeyControl(Corpus.Read().Samples.First(s => s.Fields["m_timeCategoryObject"] == "{fileID: 0}")); }

        [Test]
        public void MissingBindingFailsAtItsOwnAdmissionBoundary()
        {
            var corpus = new Corpus.Corpus { Prepare = new Corpus.Prepare { script_bindings = new Corpus.Bindings { bindings = Array.Empty<Corpus.Binding>() } } };
            var error = Assert.Throws<InvalidOperationException>(() => Corpus.RequireBinding(corpus, Proof.ScriptGuid()));
            StringAssert.Contains("exact TransformRotator binding is absent", error.Message);
        }

        [Test]
        public void OriginalCorpusRejectsMissingExtraAndReorderedFields()
        {
            Corpus.Sample sample = Corpus.Read().Samples[0];
            Corpus.CheckFields(sample.Document);
            string missing = sample.Document.Replace("  m_Enabled: " + sample.Fields["m_Enabled"] + "\n", "");
            string extra = sample.Document + "  invented_field: 0\n";
            string wrongOrder = sample.Document.Replace("  m_rotationSpeedMin:", "  swap:").Replace("  m_rotationSpeedMax:", "  m_rotationSpeedMin:").Replace("  swap:", "  m_rotationSpeedMax:");
            foreach (string document in new[] { missing, extra, wrongOrder })
                Assert.Throws<InvalidOperationException>(() => Corpus.CheckFields(document));
        }

        [Test]
        public void AssignedDuplicateContentCategoriesKeepTheirDistinctAssetIdentities()
        {
            var borrowed = new Proof.BorrowedAssets();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                foreach (var pair in new[] {
                    Tuple.Create("e72b7e5a5557139428b647daf5b0de1e", "0089624ea26a0754b80f64aa90a69794"),
                    Tuple.Create("3dbf53c896b26ef4bb01a879f3bced0b", "b95a4ebacc46f6248849781cdc7fc1f2") })
                {
                    TimeCategoryObject assigned = borrowed.Get<TimeCategoryObject>(pair.Item1), mapped = borrowed.Get<TimeCategoryObject>(pair.Item2);
                    Assert.That(assigned, Is.Not.SameAs(mapped));
                    Assert.That(Proof.Identity(assigned, pair.Item1), Is.True);
                    Assert.That(Proof.Identity(mapped, pair.Item1), Is.False);
                }
            }, borrowed.Verify);
        }

        [Test]
        public void LookupAdmissionRejectsWrongClassGuidAndDestroyedCacheWithoutRepair()
        {
            var borrowed = new Proof.BorrowedAssets(); TimeCategoryLookup wrong = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                TimeCategoryLookup lookup = borrowed.Get<TimeCategoryLookup>(Corpus.LookupGuid);
                Proof.RequireCacheEntry(null, lookup); Proof.RequireCacheEntry(lookup, lookup);
                Assert.Throws<InvalidOperationException>(() => borrowed.Get<TimeCategoryObject>(Corpus.LookupGuid));
                Assert.Throws<InvalidOperationException>(() => borrowed.Get<TimeCategoryLookup>("00000000000000000000000000000001"));
                wrong = ScriptableObject.CreateInstance<TimeCategoryLookup>();
                Assert.Throws<InvalidOperationException>(() => Proof.RequireCacheEntry(wrong, lookup));
                UnityEngine.Object.DestroyImmediate(wrong);
                Assert.That(ReferenceEquals(wrong, null), Is.False);
                Assert.Throws<InvalidOperationException>(() => Proof.RequireCacheEntry(wrong, lookup));
            }, () => { if (wrong != null) UnityEngine.Object.DestroyImmediate(wrong); }, borrowed.Verify);
        }

        [Test]
        public void ConfigurationSnapshotDetectsCacheMutationWithoutRepair()
        {
            var system = new SystemConfiguration();
            int id = HLCRC32.GenerateInt("owned snapshot control");
            system.StackableData.SetBaseValue(id, 7);
            Action verify = Proof.CaptureConfiguration(system);
            Assert.DoesNotThrow(() => verify());
            Assert.That(system.StackableData.Get<int>(id), Is.EqualTo(7));
            Assert.Throws<InvalidOperationException>(() => verify());
            Assert.That(system.StackableData.Get<int>(id, false), Is.EqualTo(7), "snapshot reports rather than repairing the real cache");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OccupiedConfigurationAdmissionPreservesGenuineSystemAndObservers(bool observed)
        {
            string name = typeof(SystemConfiguration).FullName;
            var owner = new VerificationFixtureOwnership.RegistryRow(name);
            var system = new SystemConfiguration(); SystemRef reference = null; int calls = 0;
            Action<ISystem> observer = value => ++calls;
            var refused = new Proof.OwnedConfiguration();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                reference = owner.Register(system);
                if (observed) { reference.OnSystemStartup += observer; reference.OnSystemShutdown += observer; }
                SharedStateSnapshot before = SharedStateSnapshot.Capture(); Action config = Proof.CaptureConfiguration(system);
                var failure = Assert.Throws<InvalidOperationException>(() => refused.Register());
                StringAssert.Contains("Fixture requires an absent registry row", failure.Message);
                Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty);
                Assert.That(reference.GetSafe(), Is.SameAs(system)); config(); Assert.That(calls, Is.Zero);
            }, refused.Dispose,
                () => { if (reference != null) { reference.OnSystemStartup -= observer; reference.OnSystemShutdown -= observer; } }, owner.Dispose);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfigurationObserverFailureRetainsHandleCustodyAndAttemptsRelease(bool removalFailure)
        {
            var borrowed = new Proof.BorrowedAssets(); var owned = new Proof.OwnedConfiguration();
            var primary = new InvalidOperationException("owned add observer failure");
            var secondary = new InvalidOperationException("owned remove observer failure");
            var stages = new List<string>(); int notifications = 0;
            Action<int> observer = id => { ++notifications; if (notifications == 1) throw primary; if (removalFailure) throw secondary; };
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                owned.Register(); owned.System.StackableData.OnDataUpdated += observer;
                if (removalFailure) LogAssert.Expect(LogType.Warning, new Regex("Fixture cleanup failed after an earlier failure:.*owned remove observer failure", RegexOptions.Singleline));
                Exception observed = null;
                try
                {
                    VerificationFixtureOwnership.RunThenCleanUp(() => owned.Add(borrowed.Get<TimeCategoryLookup>(Corpus.LookupGuid)),
                        () => owned.Release(stages.Add));
                }
                catch (Exception error) { observed = error; }
                Assert.That(observed, Is.SameAs(primary));
                Assert.That(notifications, Is.EqualTo(2), "both actual public notifications ran");
                Assert.That(owned.OwnedHandleCount, Is.Zero);
                CollectionAssert.AreEqual(new[] { "overrides", "configuration" }, stages);
            }, () => owned.System.StackableData.OnDataUpdated -= observer, owned.Dispose, borrowed.Verify);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfigurationAddRejectsAbsentOrReleasedCustodyBeforeAutoRegistration(bool released)
        {
            var borrowed = new Proof.BorrowedAssets(); var owned = new Proof.OwnedConfiguration();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                TimeCategoryLookup lookup = borrowed.Get<TimeCategoryLookup>(Corpus.LookupGuid);
                if (released) { owned.Register(); owned.Dispose(); }
                SharedStateSnapshot before = SharedStateSnapshot.Capture(); Action unchanged = Proof.CaptureConfiguration(owned.System);
                var error = Assert.Throws<InvalidOperationException>(() => owned.Add(lookup));
                StringAssert.Contains("configuration add requires its exact owned live registration", error.Message);
                Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty); unchanged();
                Assert.That(owned.OwnedHandleCount, Is.Zero);
            }, owned.Dispose, borrowed.Verify);
        }

        [Test]
        public void RepeatedRegistrationCannotReplaceTheExistingLeaseCustody()
        {
            var owned = new Proof.OwnedConfiguration();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                owned.Register(); SharedStateSnapshot before = SharedStateSnapshot.Capture();
                var error = Assert.Throws<InvalidOperationException>(() => owned.Register());
                StringAssert.Contains("configuration lease registers only once", error.Message);
                Assert.That(SharedStateSnapshot.Capture().Differences(before), Is.Empty);
            }, owned.Dispose);
        }

        [TestCase("configuration", false)]
        [TestCase("folder", false)]
        [TestCase("import", false)]
        [TestCase("load", false)]
        [TestCase("save", false)]
        [TestCase("reload", false)]
        [TestCase("import", true)]
        [TestCase("none", true)]
        [TestCase("none", false)]
        public void OwnedRoundtripFailureAttemptsEveryReleaseAndPreservesPrimary(string stage, bool cleanupFailure)
        {
            Corpus.Sample sample = Corpus.Read().Samples.First(s => s.Fields["m_timeCategoryObject"] == "{fileID: 0}");
            var primary = new InvalidOperationException("owned roundtrip primary"); var cleanup = new InvalidOperationException("owned cleanup failure");
            var released = new List<string>(); Proof.OwnedPrefab fixture = null; Exception observed = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                if (cleanupFailure && stage != "none") LogAssert.Expect(LogType.Warning, new Regex("Fixture cleanup failed after an earlier failure:.*owned cleanup failure", RegexOptions.Singleline));
                try
                {
                    Proof.RunFailureControl(sample, point => { if (point == stage) throw primary; },
                        point => { released.Add(point); if (cleanupFailure && point == "construction") throw cleanup; }, value => fixture = value);
                }
                catch (Exception failure) { observed = failure; }
                if (stage == "none" && !cleanupFailure) Assert.That(observed, Is.Null);
                else Assert.That(observed, Is.SameAs(stage == "none" ? cleanup : primary));
                if (stage == "configuration") Assert.That(fixture, Is.Null);
                else
                {
                    CollectionAssert.AreEqual(new[] { "construction", "contents", "folder" }, released);
                    Assert.That(fixture.Root == null && fixture.Contents == null, Is.True);
                    Assert.That(Directory.Exists(fixture.Folder) || File.Exists(fixture.Folder + ".meta"), Is.False);
                }
            }, () => fixture?.Dispose());
        }
    }
}
#endif
