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
            // Only the live manager's own update may run this callback; nothing here forces that flush.
            System.Action<AsyncOperationHandle<GameObject>> onForeign = handle => { foreignCalls++; Addressables.Release(handle); };
            foreign.Completed += onForeign;
            int queued = DeferredCompletions(live);
            try
            {
                Assert.That(queued, Is.GreaterThan(0));
                // RunUnity has 53 straight-line checks; a verifier that returned early would count fewer.
                Assert.That(ProjectLucid.ManagedAddressableUnityVerification.RunUnity(), Is.EqualTo(53));
                Assert.That(foreignCalls, Is.Zero, "the fixture dispatched foreign deferred work");
                Assert.That(DeferredCompletions(live), Is.EqualTo(queued));
                Assert.That(foreign.IsValid(), Is.True);
            }
            finally
            {
                try
                {
                    // Leave no fixture callback for a later live update: detach it and drop this
                    // test's lease. The deferred queue holds and later releases its own reference.
                    if (foreignCalls == 0 && foreign.IsValid())
                    {
                        foreign.Completed -= onForeign;
                        Addressables.Release(foreign);
                    }
                }
                finally { Object.DestroyImmediate(marker); }
            }
        }

        // Read-only observation of the package's internal deferred-completion queue.
        private static int DeferredCompletions(ResourceManager manager) =>
            (int)typeof(ResourceManager).GetProperty("DeferredCompleteCallbacksCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
    }
}
