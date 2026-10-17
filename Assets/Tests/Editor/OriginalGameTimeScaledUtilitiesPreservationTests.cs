using System;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalGameTimeScaledUtilitiesPreservationTests
    {
        [Test]
        public void OriginalGameWaitDefersLookupAndRefreshesDestroyedConfiguration()
        {
            Assert.That(AfterTypeInitializer(OriginalTimeScaledUtilitiesVerification.RunOriginalGameWaitConfigurationBoundaries), Is.EqualTo(26));
        }

        [Test]
        public void OriginalGameTimingRetainsImmediateAndDeferredFaultPrefixes()
        {
            Assert.That(AfterTypeInitializer(OriginalTimeScaledUtilitiesVerification.RunOriginalGameTimingFaultPrefixes), Is.EqualTo(17));
        }

        // TimeScaledUtilities' BeforeFieldInit initializer 0x06000ecc appends
        // the permanent 'Hardlight.TimeManager' row behind its readonly
        // s_timeManagerRef (ISOLATION_SIDE_EFFECTS
        // "TimeScaledUtilities..cctor"). The proof releases its own rows, so
        // all other shared state the isolation recorder compares must end as
        // the initializer left it.
        private static int AfterTypeInitializer(Func<int> proof)
        {
            RuntimeHelpers.RunClassConstructor(typeof(TimeScaledUtilities).TypeHandle);
            SharedStateSnapshot initialized = SharedStateSnapshot.Capture();
            int checks = proof();
            Assert.That(SharedStateSnapshot.Capture().DifferenceFrom(initialized), Is.Null,
                "Rows, references, observers, deferred work, action rows and caches end as the initializer left them.");
            return checks;
        }
    }
}
