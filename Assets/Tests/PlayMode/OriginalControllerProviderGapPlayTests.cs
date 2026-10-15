using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // A real CoroutineUtils host on an owned registry row and an owned provider
    // whose name list is empty, so polling never notifies. No device is read.
    public sealed class OriginalControllerProviderGapPlayTests
    {
        // BaseControllerProvider.Initialise 0x06000056, x86-64 0x1a1b440:
        // allocate the iterator, CoroutineUtils.RunCoroutine 0x1a1b49f, store
        // the handle at +0x20 0x1a1b4a4. Nothing stops a handle already held, so
        // a second Initialise leaves the first loop running and Shutdown
        // (0x06000057) stops only the latest handle. Stopping the prior
        // coroutine before replacing the handle fails here.
        [UnityTest]
        public IEnumerator SecondInitialiseLeavesTheFirstPollingLoopRunningAfterShutdown()
        {
            string hostName = ProcessManager.GetDefaultName<CoroutineUtils>();
            var rowLease = new LifecycleSystemRowLease(LifecycleSystemRowLease.SharedRegistry, hostName);
            using (rowLease)
            {
#pragma warning disable CS0618
                Assert.IsTrue(CoroutineUtils.Instance == null, "The bounded test starts without a utility host.");
#pragma warning restore CS0618
                GameObject host = null;
                try
                {
                    rowLease.CaptureCreatedRow(ProcessManager.GetSystemRef(hostName));
                    host = new GameObject("Lucid owned controller polling host");
                    host.AddComponent<CoroutineUtils>();
                    var provider = new CountingProvider();

                    provider.Initialise();
                    provider.Initialise();
                    Assert.AreEqual(2, provider.Reads, "Each started loop reads the names once immediately.");
                    provider.Shutdown();
                    int afterShutdown = provider.Reads;
                    yield return null;
                    yield return null;
                    yield return null;
                    Assert.Greater(provider.Reads, afterShutdown, "The replaced first loop is still scheduled.");

                    provider.Shutdown();
                    int afterSecondShutdown = provider.Reads;
                    yield return null;
                    yield return null;
                    Assert.Greater(provider.Reads, afterSecondShutdown, "Shutdown had only the cleared latest handle.");
                }
                finally
                {
                    // Destroying the host ends the orphaned loop and revokes the row.
                    rowLease.RunCleanup("destroy-polling-host", () => { if (host != null) Object.DestroyImmediate(host); });
                }
            }
            Assert.IsEmpty(rowLease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
        }

        private sealed class CountingProvider : BaseControllerProvider
        {
            private static readonly string[] NoNames = new string[0];
            public int Reads;

            // A zero rate resumes each loop on the following frame.
            public CountingProvider() : base(0f) { OnControllerConnectionUpdate = new FastAction(); }

            public override IReadOnlyList<string> GetControllerNames()
            {
                ++Reads;
                return NoNames;
            }
        }
    }
}
