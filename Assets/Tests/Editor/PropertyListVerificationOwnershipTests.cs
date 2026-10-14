using System;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // The property-list fixture must never replace or clear a foreign property store or handler.
    public sealed class PropertyListVerificationOwnershipTests
    {
        [Test]
        public void ForeignSaveHandlerSurvivesARefusedVerification()
        {
            RequireColdStore();
            HLPropertyStore.SaveHandler foreign = properties => { };
            HLPropertyStore.AddSaveHandler(foreign);
            try
            {
                Assert.Throws<InvalidOperationException>(PropertyListVerification.Run);
                Assert.That(HLPropertyStore.IsThereAnySaveHandler, Is.True);
                Assert.That(PropertyListVerification.StoreSingletonPublished(), Is.False);
            }
            finally { HLPropertyStore.RemoveSaveHandler(foreign); }
            RequireColdStore();
        }

        [Test]
        public void ForeignStoreSurvivesARefusedVerification()
        {
            RequireColdStore();
            var foreign = new HLPropertyStore("owned", 1, "owned-unused");
            try
            {
                Assert.Throws<InvalidOperationException>(PropertyListVerification.Run);
                Assert.That(typeof(HLPropertyStore).GetField("s_internalInstance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null),
                    Is.SameAs(foreign), "the foreign store stays published");
            }
            finally { foreign.Shutdown(); }
            RequireColdStore();
        }

        [Test]
        public void ColdVerificationReturnsToTheSameColdState()
        {
            RequireColdStore();
            PropertyListVerification.Run();
            PropertyListVerification.Run();
            RequireColdStore();
        }

        [Test]
        public void StorageConstructionFaultShutsDownThePublishedFixtureStoreAndAllowsRetry()
        {
            RequireColdStore();
            // The real offline storage constructor rejects this name after the original
            // constructor has already published the fixture store.
            Assert.Throws<ArgumentException>(() => PropertyListVerification.RunStoreFixture("not/portable"));
            RequireColdStore();
            PropertyListVerification.RunStoreFixture("unused");
            RequireColdStore();
        }

        private static void RequireColdStore()
        {
            Assert.That(PropertyListVerification.StoreSingletonPublished() || HLPropertyStore.IsThereAnyLoadHandler ||
                HLPropertyStore.IsThereAnySaveHandler || HLPropertyStore.IsThereAnyModifySaveHandler ||
                HLPropertyStore.IsThereAnySaveSuccessHandler || HLPropertyStore.IsThereAnyResolveConflictHandler, Is.False,
                "these cases own the property store only from a cold Editor state");
        }
    }
}
