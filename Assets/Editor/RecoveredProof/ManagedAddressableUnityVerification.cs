using System;
using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectLucid
{
    // Actual Unity main-thread proof only. Completed operations exercise genuine
    // package handles/callbacks/release without catalog initialization or loading.
    public static class ManagedAddressableUnityVerification
    {
        private sealed class Lease
        {
            public AsyncOperationHandle Handle;
            public bool Owned = true;
        }

        private static int checks;
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks++;
        }

        private static FieldInfo Field<T>(string name) where T : UnityEngine.Object
            => typeof(ManagedAddressableAsset<T>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static int Count<T>(ManagedAddressableAsset<T> owner) where T : UnityEngine.Object
            => (int)Field<T>("m_refCount").GetValue(owner);
        private static T Cached<T>(ManagedAddressableAsset<T> owner) where T : UnityEngine.Object
            => (T)Field<T>("m_loadedAsset").GetValue(owner);
        private static AsyncOperationHandle<T> Handle<T>(ManagedAddressableAsset<T> owner) where T : UnityEngine.Object
            => (AsyncOperationHandle<T>)Field<T>("m_assetHandle").GetValue(owner);
        private static void Bind<T>(ManagedAddressableAsset<T> owner, T cached, AsyncOperationHandle<T> handle, int count) where T : UnityEngine.Object
        {
            Field<T>("m_loadedAsset").SetValue(owner, cached);
            Field<T>("m_assetHandle").SetValue(owner, handle);
            Field<T>("m_refCount").SetValue(owner, count);
        }

        // Instantiate the real compiler-generated capture, never a substitute
        // callback implementation. This allows completion proof without a load.
        private static Action<AsyncOperationHandle<T>> Capture<T>(ManagedAddressableAsset<T> owner, Action<T> callback) where T : UnityEngine.Object
        {
            Type closure = typeof(ManagedAddressableAsset<>).GetNestedType("<>c__DisplayClass7_0", BindingFlags.NonPublic).MakeGenericType(typeof(T));
            object capture = Activator.CreateInstance(closure);
            closure.GetField("<>4__this").SetValue(capture, owner);
            closure.GetField("onLoaded").SetValue(capture, callback);
            MethodInfo method = closure.GetMethod("<LoadAsync>b__0", BindingFlags.Instance | BindingFlags.NonPublic);
            return (Action<AsyncOperationHandle<T>>)Delegate.CreateDelegate(typeof(Action<AsyncOperationHandle<T>>), capture, method);
        }

        private static Lease Keep<T>(List<Lease> leases, AsyncOperationHandle<T> handle)
        {
            var lease = new Lease { Handle = handle };
            leases.Add(lease);
            return lease;
        }

        public static void Run() => Debug.Log("Project Lucid ManagedAddressable actual engine lifecycle checks=" + RunUnity());

        public static int RunUnity()
        {
            checks = 0;
            var objects = new List<UnityEngine.Object>();
            var leases = new List<Lease>();
            Action<AsyncOperationHandle, Exception> previousHandler = ResourceManager.ExceptionHandler;
            ResourceManager manager = null;
            var exceptions = new List<Exception>();
            try
            {
                manager = Addressables.ResourceManager;
                ResourceManager.ExceptionHandler = (operation, error) => exceptions.Add(error);
                var first = new GameObject("Project Lucid managed asset first") { hideFlags = HideFlags.HideAndDontSave };
                objects.Add(first);
                var second = new GameObject("Project Lucid managed asset second") { hideFlags = HideFlags.HideAndDontSave };
                objects.Add(second);
                Check(first != null && second != null && !ReferenceEquals(first, second), "genuine separate engine objects");

                var cached = new ManagedAddressableAsset<GameObject>(null);
                Bind(cached, first, default(AsyncOperationHandle<GameObject>), 0);
                Check(ReferenceEquals(cached.Load(), first), "cached sync returns same real object");
                Check(Count(cached) == 1 && ReferenceEquals(Cached(cached), first), "cached sync increments before returning");
                Check(!Handle(cached).IsValid(), "cached sync does not create a load handle");
                int inline = 0;
                int countAtCallback = 0;
                GameObject argument = null;
                cached.LoadAsync(asset => { inline++; countAtCallback = Count(cached); argument = asset; });
                Check(inline == 1 && countAtCallback == 2, "cached async inline/count-before-callback");
                Check(ReferenceEquals(argument, first) && ReferenceEquals(Cached(cached), first), "cached async receives stored same object");
                Check(Count(cached) == 2 && !Handle(cached).IsValid(), "cached async retains invalid default handle");
                var sentinel = new InvalidOperationException("Project Lucid cached callback sentinel");
                Exception caught = null;
                try { cached.LoadAsync(asset => { countAtCallback = Count(cached); throw sentinel; }); }
                catch (Exception error) { caught = error; }
                Check(ReferenceEquals(caught, sentinel) && countAtCallback == 3, "cached throwing callback observes increased count");
                Check(Count(cached) == 3 && ReferenceEquals(Cached(cached), first), "cached callback failure retains count/cache");
                caught = null;
                try { cached.LoadAsync(null); }
                catch (Exception error) { caught = error; }
                Check(caught is NullReferenceException && Count(cached) == 4, "cached null callback fails after increasing count");
                Check(ReferenceEquals(Cached(cached), first) && !Handle(cached).IsValid(), "cached null callback preserves fields");
                Bind(cached, first, default(AsyncOperationHandle<GameObject>), int.MaxValue);
                cached.LoadAsync(asset => countAtCallback = Count(cached));
                Check(countAtCallback == int.MinValue && Count(cached) == int.MinValue, "cached async native count wrap precedes callback");

                var success = manager.CreateCompletedOperation(first, (string)null);
                Keep(leases, success);
                Check(success.IsValid() && success.IsDone && success.Status == AsyncOperationStatus.Succeeded, "genuine successful completed operation");
                Check(ReferenceEquals(success.Result, first), "successful typed Result is exact engine object");
                var completed = new ManagedAddressableAsset<GameObject>(null);
                Bind(completed, second, success, 7);
                int callbacks = 0;
                bool storedBeforeCallback = false;
                int completionCount = 0;
                success.Completed += Capture(completed, asset =>
                {
                    callbacks++;
                    argument = asset;
                    storedBeforeCallback = ReferenceEquals(Cached(completed), first);
                    completionCount = Count(completed);
                });
                Check(callbacks == 0, "real completed event registration defers callback");
                Check(ReferenceEquals(success.WaitForCompletion(), first), "actual completed wait flushes genuine package callbacks");
                Check(callbacks == 1 && storedBeforeCallback && ReferenceEquals(argument, first), "compiled capture stores Result before real completion callback");
                Check(completionCount == 7 && Count(completed) == 7 && Handle(completed).Equals(success), "completion retains wrapper count/current handle");
                Check(ReferenceEquals(Cached(completed), first), "successful completion replaces previous cache");

                int exceptionStart = exceptions.Count;
                const string failureMessage = "Project Lucid completed failure fixture";
                var failed = manager.CreateCompletedOperation(second, failureMessage);
                Keep(leases, failed);
                Check(failed.IsValid() && failed.IsDone && failed.Status == AsyncOperationStatus.Failed, "genuine failed completed operation");
                Check(exceptions.Count == exceptionStart + 1 && exceptions[exceptionStart].Message == failureMessage, "failure delivered to scoped real ExceptionHandler");
                Check(failed.OperationException.Message == failureMessage && ReferenceEquals(failed.Result, second), "failed Result retains provided real engine object");
                var failedOwner = new ManagedAddressableAsset<GameObject>(null);
                Bind(failedOwner, first, failed, 3);
                callbacks = 0;
                storedBeforeCallback = false;
                failed.Completed += Capture(failedOwner, asset =>
                {
                    callbacks++;
                    argument = asset;
                    storedBeforeCallback = ReferenceEquals(Cached(failedOwner), second);
                    completionCount = Count(failedOwner);
                });
                Check(callbacks == 0, "failed completed callback is deferred");
                Check(ReferenceEquals(failed.WaitForCompletion(), second), "failed completed wait returns real Result without status gate");
                Check(callbacks == 1 && storedBeforeCallback && ReferenceEquals(argument, second), "compiled callback stores and invokes failed nonnull Result");
                Check(completionCount == 3 && Count(failedOwner) == 3 && Handle(failedOwner).Equals(failed), "failed completion preserves wrapper count/handle");

                var throwOwner = new ManagedAddressableAsset<GameObject>(null);
                Bind(throwOwner, second, success, 5);
                caught = null;
                try { Capture(throwOwner, (GameObject asset) => { storedBeforeCallback = ReferenceEquals(Cached(throwOwner), first); throw sentinel; })(success); }
                catch (Exception error) { caught = error; }
                Check(ReferenceEquals(caught, sentinel) && storedBeforeCallback, "direct original completion capture stores before thrown callback");
                Check(Count(throwOwner) == 5 && ReferenceEquals(Cached(throwOwner), first), "throwing completion retains cache/count");
                Bind(throwOwner, second, success, 5);
                caught = null;
                try { Capture(throwOwner, (Action<GameObject>)null)(success); }
                catch (Exception error) { caught = error; }
                Check(caught is NullReferenceException && ReferenceEquals(Cached(throwOwner), first), "original null completion callback fails after Result store");
                Check(Count(throwOwner) == 5 && Handle(throwOwner).Equals(success), "null completion callback preserves count/handle");

                var released = manager.CreateCompletedOperation(first, (string)null);
                Lease releasedLease = Keep(leases, released);
                var releaseOwner = new ManagedAddressableAsset<GameObject>(null);
                Bind(releaseOwner, first, released, 1);
                int destructions = 0;
                bool clearedDuringDestroy = false;
                bool handleRetainedDuringDestroy = false;
                released.Destroyed += operation =>
                {
                    destructions++;
                    clearedDuringDestroy = Count(releaseOwner) == 0 && ReferenceEquals(Cached(releaseOwner), null);
                    handleRetainedDuringDestroy = Handle(releaseOwner).Equals(released) && Handle(releaseOwner).IsValid();
                };
                releaseOwner.Unload();
                releasedLease.Owned = false;
                Check(destructions == 1 && clearedDuringDestroy, "real operation destruction observes cleared cache/zero count");
                Check(handleRetainedDuringDestroy, "outer handle still stored and valid during destruction event");
                Check(!released.IsValid() && Handle(releaseOwner).Equals(released), "real release leaves identical stale outer handle field");
                Check(!Handle(releaseOwner).IsValid() && Count(releaseOwner) == 0 && ReferenceEquals(Cached(releaseOwner), null), "released wrapper state retains invalid stale handle");
                releaseOwner.Unload();
                Check(destructions == 1 && Count(releaseOwner) == 0 && Handle(releaseOwner).Equals(released), "extra unload skips stale-invalid release");

                var retained = manager.CreateCompletedOperation(second, (string)null);
                Lease retainedLease = Keep(leases, retained);
                var external = manager.Acquire(retained);
                Lease externalLease = Keep(leases, external);
                var retainedOwner = new ManagedAddressableAsset<GameObject>(null);
                Bind(retainedOwner, second, retained, 1);
                destructions = 0;
                retained.Destroyed += operation => destructions++;
                retainedOwner.Unload();
                retainedLease.Owned = false;
                Check(retained.IsValid() && external.IsValid() && destructions == 0, "real acquired reference keeps operation valid after zero-count unload");
                Check(Handle(retainedOwner).Equals(retained) && Handle(retainedOwner).IsValid() && ReferenceEquals(Cached(retainedOwner), null), "valid retained outer handle survives first Release copy");
                retainedOwner.Unload();
                externalLease.Owned = false;
                Check(destructions == 1 && !external.IsValid() && !retained.IsValid(), "repeated zero-count unload releases retained valid handle again");
                Check(Count(retainedOwner) == 0 && Handle(retainedOwner).Equals(retained), "repeated release preserves stale outer handle identity");

                var earlier = manager.CreateCompletedOperation(first, (string)null);
                Keep(leases, earlier);
                var later = manager.CreateCompletedOperation(second, (string)null);
                Lease laterLease = Keep(leases, later);
                var lateOwner = new ManagedAddressableAsset<GameObject>(null);
                Bind(lateOwner, second, later, 1);
                lateOwner.Unload();
                laterLease.Owned = false;
                Check(Count(lateOwner) == 0 && ReferenceEquals(Cached(lateOwner), null) && !later.IsValid(), "late callback fixture actually unloaded newer handle");
                callbacks = 0;
                earlier.Completed += Capture(lateOwner, asset => { callbacks++; argument = asset; completionCount = Count(lateOwner); });
                Check(callbacks == 0 && Handle(lateOwner).Equals(later), "earlier completion queued after later handle release");
                earlier.WaitForCompletion();
                Check(callbacks == 1 && completionCount == 0 && ReferenceEquals(argument, first), "late real completion invokes original callback at wrapper count zero");
                Check(Count(lateOwner) == 0 && ReferenceEquals(Cached(lateOwner), first), "late original capture repopulates zero-count cache");
                Check(Handle(lateOwner).Equals(later) && !Handle(lateOwner).IsValid(), "late incoming handle does not replace retained current handle");

                var dead = new GameObject("Project Lucid managed asset destroyed") { hideFlags = HideFlags.HideAndDontSave };
                objects.Add(dead);
                var deadHandle = manager.CreateCompletedOperation(dead, (string)null);
                Keep(leases, deadHandle);
                var deadOwner = new ManagedAddressableAsset<GameObject>(null);
                Bind(deadOwner, dead, deadHandle, 0);
                Check(ReferenceEquals(deadOwner.Load(), dead) && Count(deadOwner) == 1, "live object cached return before destruction");
                UnityEngine.Object.DestroyImmediate(dead);
                Check(dead == null && !ReferenceEquals(dead, null), "genuine engine destroyed-object Unity-null/raw-reference distinction");
                Check(ReferenceEquals(Cached(deadOwner), dead) && Cached(deadOwner) == null, "same cached destroyed engine object retains raw identity and becomes Unity-null");
                Check(deadHandle.IsValid() && ReferenceEquals(deadHandle.Result, dead) && deadHandle.Result == null, "real operation Result retains destroyed Unity object reference");
                callbacks = 0;
                deadHandle.Completed += Capture(deadOwner, asset => { callbacks++; argument = asset; });
                deadHandle.WaitForCompletion();
                Check(callbacks == 1 && ReferenceEquals(argument, dead) && argument == null, "completion passes exact destroyed object without a Unity-null guard");
                Check(ReferenceEquals(Cached(deadOwner), dead) && Count(deadOwner) == 1, "destroyed Result store retains same object/count");
                // Calling Load/LoadAsync again here would enter actual catalog loading;
                // the safe proof stops after observing the original Unity-null rule.

                var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                objects.Add(texture);
                var textureHandle = manager.CreateCompletedOperation(texture, (string)null);
                Lease textureLease = Keep(leases, textureHandle);
                var textureOwner = new ManagedAddressableAsset<Texture2D>(null);
                Bind(textureOwner, null, textureHandle, 1);
                Texture2D textureArgument = null;
                textureHandle.Completed += Capture(textureOwner, asset => textureArgument = asset);
                textureHandle.WaitForCompletion();
                Check(ReferenceEquals(textureArgument, texture) && ReferenceEquals(Cached(textureOwner), texture), "second genuine engine generic capture/Result instantiation");
                textureOwner.Unload();
                textureLease.Owned = false;
                Check(!textureHandle.IsValid() && Handle(textureOwner).Equals(textureHandle) && Count(textureOwner) == 0, "second generic actual typed Release retains stale field");
                Check(exceptions.Count == exceptionStart + 1, "only deliberate failed operation reached ExceptionHandler");
                return checks;
            }
            finally
            {
                Exception cleanupFailure = null;
                try
                {
                    if (manager != null)
                    {
                        // Flush package-owned deferred references before releasing our
                        // remaining caller leases, including on an assertion failure.
                        try
                        {
                            var flush = manager.CreateCompletedOperation<UnityEngine.Object>(null, (string)null);
                            try { flush.WaitForCompletion(); }
                            finally { if (flush.IsValid()) Addressables.Release(flush); }
                        }
                        catch (Exception error) { cleanupFailure = error; }
                    }
                    for (int i = leases.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            if (leases[i].Owned && leases[i].Handle.IsValid()) Addressables.Release(leases[i].Handle);
                        }
                        catch (Exception error) { if (cleanupFailure == null) cleanupFailure = error; }
                    }
                }
                finally
                {
                    ResourceManager.ExceptionHandler = previousHandler;
                    for (int i = objects.Count - 1; i >= 0; i--)
                    {
                        try { if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]); }
                        catch (Exception error) { if (cleanupFailure == null) cleanupFailure = error; }
                    }
                }
                if (cleanupFailure != null) throw new InvalidOperationException("ManagedAddressable Unity fixture cleanup failed", cleanupFailure);
            }
        }
    }
}
