using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ProjectLucid.Editor
{
    public static class AddressableManagerVerification
    {
        private static readonly BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
        private static void Check(bool value, string name, ref int checks)
        {
            if (!value) throw new InvalidOperationException(name);
            checks++;
        }
        private static void Throws<T>(Action action, string name, ref int checks) where T : Exception
        {
            try { action(); }
            catch (T exception) { if (exception.GetType() != typeof(T)) throw; checks++; return; }
            throw new InvalidOperationException(name);
        }
        private static FieldInfo Field(string name) => typeof(AddressableManager).GetField(name, Own);
        private static Dictionary<string, AsyncOperationHandle> Lookup(AddressableManager value) => (Dictionary<string, AsyncOperationHandle>)Field("m_preloadedAssetHandleLookup").GetValue(value);
        private static HashSet<AsyncOperationHandle> Handles(AddressableManager value) => (HashSet<AsyncOperationHandle>)Field("m_preloadHandles").GetValue(value);
        private static AddressableManager Raw()
        {
            var value = (AddressableManager)FormatterServices.GetUninitializedObject(typeof(AddressableManager));
            Field("m_preloadedAssetHandleLookup").SetValue(value, new Dictionary<string, AsyncOperationHandle>());
            Field("m_preloadHandles").SetValue(value, new HashSet<AsyncOperationHandle>());
            return value;
        }
        private static IEnumerator Wait<T>(AddressableManager value, AsyncOperationHandle<T> handle, Action<T> complete, Action fail)
        {
            return (IEnumerator)typeof(AddressableManager).GetMethod("WaitForAsset", Own).MakeGenericMethod(typeof(T)).Invoke(value, new object[] { handle, complete, fail });
        }

        // Constructor bypass only supplies a genuine manager's managed containers.
        // No native Unity object, Addressable load, release of a valid handle or provider is executed here.
        public static int RunManaged()
        {
            int checks = 0;
            var type = typeof(AddressableManager);
            Check(type.BaseType == typeof(MonoBehaviour), "real MonoBehaviour base", ref checks);
            Check(type.GetInterfaces().SequenceEqual(new[] { typeof(ISystem) }), "original empty ISystem", ref checks);
            Check(typeof(ISystem).GetMethods().Length == 0, "no invented system contract", ref checks);
            var options = type.GetCustomAttributesData().Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
            Check(options.Length == 2, "two original options", ref checks);
            Check((int)options[0].ConstructorArguments[0].Value == 2 && !(bool)options[0].ConstructorArguments[1].Value, "array checks first false", ref checks);
            Check((int)options[1].ConstructorArguments[0].Value == 1 && !(bool)options[1].ConstructorArguments[1].Value, "null checks second false", ref checks);
            Check(type.GetFields(Own).Select(f => f.Name).SequenceEqual(new[] { "m_preloadedAssetHandleLookup", "m_preloadHandles" }), "two ordered containers", ref checks);
            Check(type.GetFields(Own).All(f => f.IsInitOnly && f.IsPrivate && f.GetCustomAttributesData().Count == 0), "readonly nonserialized containers", ref checks);
            Check(type.GetMethods(Own).Length == 25, "25 own methods plus genuine constructor", ref checks);
            Check(type.GetConstructors().Length == 1, "one original constructor", ref checks);
            var scene = type.GetMethod("LoadScene", Own).GetGenericArguments().Single();
            Check(scene.GetGenericParameterConstraints().Length == 0, "unused unconstrained scene T retained", ref checks);
            var waiting = type.GetMethod("WaitForAsset", Own).GetGenericArguments().Single();
            Check(waiting.GetGenericParameterConstraints().Length == 0, "unconstrained waiter T", ref checks);
            foreach (var method in type.GetMethods(Own).Where(m => m.IsGenericMethodDefinition && m.Name != "LoadScene" && m.Name != "WaitForAsset"))
                Check(method.GetGenericArguments()[0].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(UnityEngine.Object) }), "real object constraint " + method.Name, ref checks);

            var value = Raw();
            Check(Lookup(value).Count == 0 && Handles(value).Count == 0, "real empty managed containers", ref checks);
            Check(!value.TryGetPreloadedAsset<GameObject>("missing", out var missing) && !missing.IsValid(), "miss initializes out default", ref checks);
            Lookup(value).Add("ordinal", default);
            Check(!value.TryGetPreloadedAsset<GameObject>("ORDINAL", out missing), "default string comparer remains case sensitive", ref checks);
            Throws<ArgumentNullException>(() => value.TryGetPreloadedAsset<GameObject>(null, out missing), "null key fault", ref checks);
            Throws<Exception>(() => value.TryGetPreloadedAsset<GameObject>("ordinal", out missing), "cached invalid conversion faults instead of becoming miss", ref checks);
            Check(Lookup(value).ContainsKey("ordinal"), "failed conversion does not prune", ref checks);
            value.ReleaseHandle(default);
            Check(Lookup(value).Count == 1, "invalid release leaves cache", ref checks);
            Handles(value).Add(default);
            value.ReleasePreloadedAssets();
            Check(Handles(value).Count == 0, "invalid retained handle cleared after skipped release", ref checks);
            Check(Lookup(value).Count == 0, "cache cleared after handle loop", ref checks);

            int complete = 0, fail = 0;
            var labels = new List<AssetLabelReference>();
            var empty = value.PreloadAssetsFromLabels<GameObject>(labels, () => complete++, () => fail++);
            Check(complete == 0 && fail == 0, "iterator defers completion", ref checks);
            Check(!empty.MoveNext(), "empty labels complete without yield", ref checks);
            Check(complete == 1 && fail == 0, "empty route invokes only completion", ref checks);
            Check(empty.Current == null, "empty current stays null", ref checks);
            Check(!empty.MoveNext() && complete == 1, "terminal route never repeats callback", ref checks);
            Throws<NotSupportedException>(() => empty.Reset(), "original iterator reset fault", ref checks);
            ((IDisposable)empty).Dispose();
            Check(complete == 1 && fail == 0, "dispose has no callback", ref checks);
            Check(!value.PreloadAssetsFromLabels<GameObject>(labels).MoveNext(), "null completion allowed", ref checks);
            var throwing = value.PreloadAssetsFromLabels<GameObject>(labels, () => throw new ArgumentException("completion"), () => fail++);
            Throws<ArgumentException>(() => throwing.MoveNext(), "completion exception propagates", ref checks);
            Check(!throwing.MoveNext() && fail == 0, "throwing completion remains terminated", ref checks);
            var nullLabels = value.PreloadAssetsFromLabels<GameObject>(null, () => complete++, () => fail++);
            Throws<NullReferenceException>(() => nullLabels.MoveNext(), "null labels original fault", ref checks);
            Check(complete == 1 && fail == 0, "null labels do not invoke callbacks", ref checks);

            var waiter = Wait<int>(value, default, ignored => complete++, () => fail++);
            Check(complete == 1 && fail == 0, "waiter allocation has no callback", ref checks);
            Check(waiter.MoveNext(), "waiter always yields once before validation", ref checks);
            Check(waiter.Current is AsyncOperationHandle<int> && !((AsyncOperationHandle<int>)waiter.Current).IsValid(), "first waiter current is boxed original handle", ref checks);
            Check(complete == 1 && fail == 0, "first wait does not validate or callback", ref checks);
            ((IDisposable)waiter).Dispose();
            Check(complete == 1 && fail == 0, "waiter dispose does not validate or callback", ref checks);
            Throws<NotSupportedException>(() => waiter.Reset(), "waiter reset fault", ref checks);
            var tokenNames = type.GetNestedTypes(BindingFlags.NonPublic).Select(t => t.Name).ToArray();
            Check(tokenNames.Length == 9, "complete nine generated owners", ref checks);
            Check(tokenNames.Contains("<WaitForAsset>d__11`1") && tokenNames.Contains("<PreloadAssetsFromLabels>d__19`1"), "original iterator ordinals", ref checks);
            var preloadFields = type.GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name == "<PreloadAssetsFromLabels>d__19`1").GetFields(Own).Select(f => f.Name);
            Check(preloadFields.SequenceEqual(new[] { "<>1__state", "<>2__current", "labels", "onComplete", "<>4__this", "onFail", "<preloadLocationsHandle>5__2", "<waitingForPreloadAssets>5__3" }), "original complete preload field order", ref checks);
            var waiterFields = type.GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name == "<WaitForAsset>d__11`1").GetFields(Own).Select(f => f.Name);
            Check(waiterFields.SequenceEqual(new[] { "<>1__state", "<>2__current", "loadingHandle", "<>4__this", "onFail", "onComplete" }), "original complete waiter field order", ref checks);
            return checks;
        }

        // Separate real package API experiment. Standalone engine icall failures are reported,
        // never replaced with a fabricated AsyncOperationBase/provider.
        public static int RunResourceManagerManaged()
        {
            int checks = 0;
            using (var resourceManager = new ResourceManager())
            {
                typeof(ResourceManager).GetField("CallbackHooksEnabled", Own).SetValue(resourceManager, false);
                var handle = resourceManager.CreateCompletedOperation<GameObject>(null, null);
                try
                {
                    Check(handle.IsValid() && handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded, "genuine completed package operation", ref checks);
                    var value = Raw();
                    Lookup(value).Add("fixture", handle);
                    Check(value.TryGetPreloadedAsset<GameObject>("fixture", out var cached) && cached.Equals(handle), "real typed conversion preserves handle", ref checks);
                    Check((bool)typeValidate(value, handle), "successful validation returns before diagnostics or release", ref checks);
                    int calls = 0;
                    var reference = new AssetReferenceT<GameObject>("fixture");
                    Check(value.LoadAsset(reference, ignored => calls++, () => throw new Exception("fail")).Equals(handle) && calls == 1, "preloaded typed callback synchronous", ref checks);
                    Check(value.LoadAsset<GameObject>((AssetReference)reference, ignored => calls++).Equals(handle) && calls == 2, "preloaded untyped callback synchronous", ref checks);
                    Check(value.LoadAssetImmediate<GameObject>(reference, ignored => calls++).Equals(handle) && calls == 3, "preloaded immediate skips waiting", ref checks);
                    Check(value.LoadAsset(reference).Equals(handle), "preloaded null callback returns handle", ref checks);
                    var captured = value.LoadAsset(reference, ignored => Lookup(value).Clear());
                    Check(captured.Equals(handle) && Lookup(value).Count == 0, "callback mutation does not replace captured return handle", ref checks);
                    Lookup(value).Add("fixture", handle);
                    Throws<ArgumentException>(() => value.LoadAsset(reference, ignored => throw new ArgumentException("callback")), "preloaded callback exception propagates", ref checks);
                    Check(Lookup(value).ContainsKey("fixture") && handle.IsValid(), "throwing callback does not release or prune", ref checks);
                    var waiter = Wait(value, handle, ignored => calls++, () => throw new Exception("fail"));
                    Check(waiter.MoveNext() && calls == 3, "done operation still yields first", ref checks);
                    Check(!waiter.MoveNext() && calls == 4, "done operation completion after resume", ref checks);
                    Check(!waiter.MoveNext() && calls == 4, "waiter does not repeat completion", ref checks);
                    Check(!(bool)typeValidate(value, default), "invalid operation passes catch and unsuccessful route", ref checks);
                    var invalidWaiter = Wait<int>(value, default, ignored => throw new Exception("complete"), () => calls++);
                    Check(invalidWaiter.MoveNext() && calls == 4, "invalid handle also yields before failure", ref checks);
                    Check(!invalidWaiter.MoveNext() && calls == 5, "invalid resumed waiter invokes failure", ref checks);
                    var failure = resourceManager.CreateCompletedOperationWithException<GameObject>(null, new ArgumentException("fixture-failure"));
                    try
                    {
                        Handles(value).Add(failure);
                        Check(!(bool)typeValidate(value, failure), "failed genuine package operation rejected", ref checks);
                        Check(failure.IsValid() && Handles(value).Contains(failure), "retained failed operation is not released", ref checks);
                        var failedWaiter = Wait(value, failure, ignored => throw new Exception("complete"), () => calls++);
                        Check(failedWaiter.MoveNext() && calls == 5, "failed completed operation still yields", ref checks);
                        Check(!failedWaiter.MoveNext() && calls == 6, "failed completed operation calls failure after yield", ref checks);
                    }
                    finally { Handles(value).Remove(failure); resourceManager.Release(failure); }
                    var location = new ResourceLocationBase("alias", "fixture-id", "fixture-provider", typeof(GameObject));
                    var ownerType = typeof(AddressableManager).GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name == "<>c__DisplayClass19_0`1").MakeGenericType(typeof(GameObject));
                    var owner = Activator.CreateInstance(ownerType);
                    ownerType.GetField("resource", Own).SetValue(owner, location);
                    ownerType.GetField("<>4__this", Own).SetValue(owner, value);
                    var publish = ownerType.GetMethod("<PreloadAssetsFromLabels>b__0", Own);
                    publish.Invoke(owner, new object[] { handle });
                    Check(Lookup(value)["alias"].Equals((AsyncOperationHandle)handle), "original completed callback publishes primary key", ref checks);
                    Check(Handles(value).Contains(handle), "callback then retains handle", ref checks);
                    var duplicate = resourceManager.CreateCompletedOperation<GameObject>(null, null);
                    try
                    {
                        publish.Invoke(owner, new object[] { duplicate });
                        Check(Lookup(value)["alias"].Equals((AsyncOperationHandle)handle), "duplicate key keeps first publication", ref checks);
                        Check(Handles(value).Contains(handle) && Handles(value).Contains(duplicate), "duplicate callback still retains second handle", ref checks);
                    }
                    finally { Handles(value).Remove(duplicate); resourceManager.Release(duplicate); }
                    Handles(value).Remove(handle);
                }
                finally { resourceManager.Release(handle); }
            }
            return checks;
        }
        private static object typeValidate(AddressableManager value, AsyncOperationHandle handle) => typeof(AddressableManager).GetMethod("ValidateOperation", Own).Invoke(value, new object[] { handle });

        // Uses original concrete components and actual completed package operations only.
        // No catalog, supplied asset or authored startup load is claimed by this fixture.
        public static int RunEngine()
        {
            int checks = 0;
            var registryField = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            var oldRegistry = registryField.GetValue(null);
            var freshRegistry = Activator.CreateInstance(registryField.FieldType);
            var created = new List<GameObject>();
            ResourceManager resourceManager = null;
            var operations = new List<AsyncOperationHandle>();
            var oldPackageExceptionHandler = ResourceManager.ExceptionHandler;
            registryField.SetValue(null, freshRegistry);
            try
            {
                ResourceManager.ExceptionHandler = null;
                var root = new GameObject("AddressableManager original fixture"); created.Add(root); root.SetActive(false);
                var value = root.AddComponent<AddressableManager>();
                Check(value != null && Lookup(value).Count == 0 && Handles(value).Count == 0, "actual original constructor initializes containers", ref checks);
                typeof(AddressableManager).GetMethod("Awake", Own).Invoke(value, null);
                Check(ReferenceEquals(ProcessManager.GetSystemSafe<AddressableManager>(), value), "original Awake registers concrete manager", ref checks);
                resourceManager = new ResourceManager();
                typeof(ResourceManager).GetField("CallbackHooksEnabled", Own).SetValue(resourceManager, false);
                var prefab = new GameObject("AddressableManager real prefab"); created.Add(prefab);
                prefab.transform.position = new Vector3(1, 2, 3);
                prefab.transform.rotation = Quaternion.Euler(0, 30, 0);
                var parent = new GameObject("AddressableManager real parent"); created.Add(parent);
                parent.transform.position = new Vector3(10, 20, 30);
                var handle = resourceManager.CreateCompletedOperation(prefab, null); operations.Add(handle);
                Lookup(value).Add("fixture", handle);
                var reference = new AssetReferenceT<GameObject>("fixture");
                GameObject result = null;
                var control = UnityEngine.Object.Instantiate(prefab, parent.transform); created.Add(control);
                var returned = value.LoadAssetAsyncInstance(reference, new Vector3(99, 98, 97), Quaternion.identity, parent.transform, clone => { result = clone; created.Add(clone); });
                Check(returned.Equals(handle), "preloaded async returns original asset handle", ref checks);
                Check(result != null && result != prefab, "preloaded callback receives real clone", ref checks);
                Check(result.transform.parent == parent.transform, "async clone uses real parent", ref checks);
                Check(result.transform.position == control.transform.position && result.transform.rotation == control.transform.rotation, "preloaded pose overload retains parent-only instantiation quirk", ref checks);
                Check(result.transform.position != new Vector3(99, 98, 97), "requested pose is ignored on preloaded route", ref checks);
                var before = parent.transform.childCount;
                Check(value.LoadAssetAsyncInstance(reference, parent.transform).Equals(handle), "null async callback still returns cached handle", ref checks);
                Check(parent.transform.childCount == before, "null callback skips clone allocation", ref checks);
                var sync = value.LoadAssetInstance(reference, out var syncClone, new Vector3(99, 98, 97), Quaternion.identity, parent.transform); created.Add(syncClone);
                Check(sync.Equals(handle) && syncClone != null, "synchronous preloaded real clone", ref checks);
                Check(syncClone.transform.position == control.transform.position && syncClone.transform.rotation == control.transform.rotation, "synchronous cached pose also ignored", ref checks);
                value.LoadAssetInstance(reference, out var otherClone, parent.transform); created.Add(otherClone);
                Check(otherClone != null && otherClone.transform.parent == parent.transform, "parent-only synchronous clone", ref checks);
                GameObject callbackValue = null;
                Check(value.LoadAsset(reference, loaded => callbackValue = loaded).Equals(handle) && callbackValue == prefab, "typed load returns original asset without instantiating", ref checks);
                callbackValue = null;
                Check(value.LoadAsset<GameObject>((AssetReference)reference, loaded => callbackValue = loaded).Equals(handle) && callbackValue == prefab, "untyped reference cached load", ref checks);
                Check(value.LoadAssetImmediate<GameObject>(reference).Equals(handle), "cached immediate load", ref checks);
                AddressableManager.ReleaseHandleInManager(handle);
                Check(handle.IsValid(), "original live-manager branch returns without releasing", ref checks);
                Handles(value).Add(handle);
                value.ReleaseHandle(handle);
                Check(handle.IsValid(), "ordinary release skips retained preload", ref checks);

                // Original registered logging host, with its real public callback API.
                // Singleton startup/native-plugin initialization is deliberately outside this fixture.
                var logObject = new GameObject("AddressableManager registered logging host"); created.Add(logObject); logObject.SetActive(false);
                var host = logObject.AddComponent<HLUnityCore>();
                var logs = new List<string>(); host.AddLogErrorHandler((text, code) => logs.Add(text));
                ProcessManager.RegisterSystem(host);
                Check(!(bool)typeValidate(value, default), "invalid handle source failure route", ref checks);
                Check(logs.Count == 2, "invalid status emits catch and failure diagnostics", ref checks);
                Check(logs[0] == "Exception when accessing addressable operation handle: \"Attempting to use an invalid operation handle\"", "exact caught package error text", ref checks);
                Check(logs[1] == "Failed to load asset InvalidHandle with error None.", "exact invalid-handle failure text", ref checks);
                logs.Clear();
                var failed = resourceManager.CreateCompletedOperationWithException<GameObject>(null, new ArgumentException("fixture-failure")); operations.Add(failed);
                Handles(value).Add(failed);
                Check(!(bool)typeValidate(value, failed), "failed actual operation validation", ref checks);
                Check(logs.Count == 1 && logs[0].EndsWith(" with error System.ArgumentException: fixture-failure."), "failed operation exception text", ref checks);
                Check(failed.IsValid(), "retained failed handle survives validation release call", ref checks);
                int complete = 0, fail = 0;
                var waiter = Wait(value, failed, loaded => complete++, () => fail++);
                Check(waiter.MoveNext() && complete == 0 && fail == 0, "actual failed operation yields before callbacks", ref checks);
                Check(!waiter.MoveNext() && complete == 0 && fail == 1, "actual failed operation resumes only failure", ref checks);
                var success = Wait(value, handle, loaded => complete++, () => fail++);
                Check(success.MoveNext() && complete == 0, "actual success yields even when done", ref checks);
                Check(!success.MoveNext() && complete == 1 && fail == 1, "actual success callback after yield", ref checks);
                Check(Handles(value).Count == 2 && Lookup(value).Count == 1, "failure paths preserve retained containers", ref checks);
            }
            finally
            {
                try
                {
                    try
                    {
                        if (resourceManager != null)
                            ReleaseAll(resourceManager, operations, operations.Count - 1);
                    }
                    finally { resourceManager?.Dispose(); }
                }
                finally
                {
                    try { DestroyAll(created, created.Count - 1); }
                    finally
                    {
                        try { ResourceManager.ExceptionHandler = oldPackageExceptionHandler; }
                        finally { registryField.SetValue(null, oldRegistry); }
                    }
                }
            }
            return checks;
        }
        private static void ReleaseAll(ResourceManager manager, List<AsyncOperationHandle> values, int index)
        {
            if (index < 0) return;
            try { if (values[index].IsValid()) manager.Release(values[index]); }
            finally { ReleaseAll(manager, values, index - 1); }
        }
        private static void DestroyAll(List<GameObject> values, int index)
        {
            if (index < 0) return;
            try { if (values[index] != null) UnityEngine.Object.DestroyImmediate(values[index]); }
            finally { DestroyAll(values, index - 1); }
        }
    }
}
