using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class ControllerActionTests
    {
        // Emission-regression check of the project's own compile, not original evidence.
        [Test] public void CurrentBuildEmissionMatchesRecordedSnapshot() { ControllerActionVerification.VerifyCurrentBuildEmissionMatchesRecordedSnapshot(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void GenuineConstructorsAndPersistentSystemCache() { Assert.Greater(AfterTypeInitializer(ControllerActionVerification.VerifyDeclarationsConstructorsAndCache), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void HeldUpAndDisabledPublishBeforeBrainFaults() { Assert.Greater(AfterTypeInitializer(ControllerActionVerification.VerifyHeldUpAndDisabledPrefixes), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void RejectedModifiersAndValidatorsRetainFlags() { Assert.Greater(AfterTypeInitializer(ControllerActionVerification.VerifyRejectedModifiersAndValidatorFaults), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void PrimarySecondaryMappingCallsAndRealClosures() { Assert.Greater(AfterTypeInitializer(ControllerActionVerification.VerifySettingsMappingAndOwnedClosures), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void NeverActiveInputSystemUsesExplicitOriginalCallbacks() { Assert.Greater(AfterTypeInitializer(ControllerActionVerification.VerifyOwnedInputSystemExplicitCallbacks), 0); LogAssert.NoUnexpectedReceived(); }

        // InputActionSubscription's initializer (ldnull; ldc.i4.1; call
        // GetSystemRef<InputSystem>; stsfld m_inputSystemRef) appends the
        // permanent 'HardlightProject.InputSystem' row behind its readonly
        // reference (ISOLATION_SIDE_EFFECTS "InputActionSubscription..cctor").
        // A row absent before it runs must be its one addition, holding that
        // reference; each proof's Prepare then checks the reused row. All other
        // shared state the isolation recorder compares must end as the
        // initializer left it.
        private static int AfterTypeInitializer(Func<int> proof)
        {
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string name = ProcessManager.GetDefaultName<InputSystem>();
            bool absent = !registry.Contains(name);
            int rows = registry.Count;
            RuntimeHelpers.RunClassConstructor(typeof(InputActionSubscription).TypeHandle);
            if (absent)
            {
                Assert.That(registry.Count, Is.EqualTo(rows + 1), "The initializer adds exactly its own row.");
                object reference = typeof(InputActionSubscription).GetField("m_inputSystemRef", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(ProcessManager.GetSystemRef<InputSystem>(null, false), Is.SameAs(reference), "The new row holds the initializer's cached reference.");
            }
            SharedStateSnapshot initialized = SharedStateSnapshot.Capture();
            int checks = proof();
            Assert.That(SharedStateSnapshot.Capture().DifferenceFrom(initialized), Is.Null,
                "Rows, references, observers, deferred work, action rows and caches end as the initializer left them.");
            return checks;
        }
    }
}
