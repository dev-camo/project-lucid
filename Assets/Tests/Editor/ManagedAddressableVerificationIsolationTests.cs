using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectLucid.Editor
{
    // The ManagedAddressable fixture must never pump the live Addressables manager.
    public sealed class ManagedAddressableVerificationIsolationTests
    {
        [Test]
        public void ForeignDeferredCompletionIsNotDispatchedByTheVerification()
        {
            ResourceManager live = Addressables.ResourceManager;
            var marker = new GameObject("Project Lucid foreign deferred marker") { hideFlags = HideFlags.HideAndDontSave };
            int foreignCalls = 0;
            AsyncOperationHandle<GameObject> foreign = live.CreateCompletedOperation(marker, (string)null);
            // The live manager's own update later runs this callback, which releases the handle;
            // nothing here forces that flush.
            foreign.Completed += handle => { foreignCalls++; Addressables.Release(handle); };
            int queued = DeferredCompletions(live);
            try
            {
                Assert.That(queued, Is.GreaterThan(0));
                Assert.That(ProjectLucid.ManagedAddressableUnityVerification.RunUnity(), Is.GreaterThan(0));
                Assert.That(foreignCalls, Is.Zero, "the fixture dispatched foreign deferred work");
                Assert.That(DeferredCompletions(live), Is.EqualTo(queued));
                Assert.That(foreign.IsValid(), Is.True);
            }
            finally { Object.DestroyImmediate(marker); }
        }

        // Read-only observation of the package's internal deferred-completion queue.
        private static int DeferredCompletions(ResourceManager manager) =>
            (int)typeof(ResourceManager).GetProperty("DeferredCompleteCallbacksCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
    }
}
