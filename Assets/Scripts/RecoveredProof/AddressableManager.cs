using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
    // Reconstructed original Game.Runtime type 0x02000089, methods 0x060004c8
    // through 0x060004e1, including its constructor. The supplied ARM64 and x86_64
    // bodies establish the cache, callback, iterator and release ordering below.
    // Preserve observed quirks here; any portable loading policy belongs at a
    // separate integration boundary. Actual catalog and scene loading still need
    // validation against the reconstructed content.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class AddressableManager : MonoBehaviour, ISystem
    {
        private readonly Dictionary<string, AsyncOperationHandle> m_preloadedAssetHandleLookup = new Dictionary<string, AsyncOperationHandle>();
        private readonly HashSet<AsyncOperationHandle> m_preloadHandles = new HashSet<AsyncOperationHandle>();

        private void Awake()
        {
            ProcessManager.RegisterSystem(this);
        }

        public AsyncOperationHandle<GameObject> LoadAssetAsyncInstance(AssetReferenceT<GameObject> assetReference, Transform parent, Action<GameObject> onComplete = null)
        {
            if (TryGetPreloadedAsset<GameObject>(assetReference.AssetGUID, out var handle))
            {
                // The original null callback also skips evaluating Instantiate.
                onComplete?.Invoke(UnityEngine.Object.Instantiate(handle.Result, parent));
                return handle;
            }
            handle = Addressables.InstantiateAsync(assetReference, parent);
            StartCoroutine(WaitForAsset(handle, prefab => onComplete?.Invoke(prefab), () => LoadingOperationFailed(assetReference, parent)));
            return handle;
        }

        // Cached instances use the parent overload and ignore the requested pose.
        public AsyncOperationHandle<GameObject> LoadAssetAsyncInstance(AssetReferenceT<GameObject> assetReference, Vector3 position, Quaternion rotation, Transform parent = null, Action<GameObject> onComplete = null)
        {
            if (TryGetPreloadedAsset<GameObject>(assetReference.AssetGUID, out var handle))
            {
                onComplete?.Invoke(UnityEngine.Object.Instantiate(handle.Result, parent));
                return handle;
            }
            handle = Addressables.InstantiateAsync(assetReference, position, rotation, parent);
            StartCoroutine(WaitForAsset(handle, prefab => onComplete?.Invoke(prefab), () => LoadingOperationFailed(assetReference, parent)));
            return handle;
        }

        // The synchronous cache route also retains the original pose behavior.
        public AsyncOperationHandle<GameObject> LoadAssetInstance(AssetReferenceT<GameObject> assetReference, out GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (TryGetPreloadedAsset<GameObject>(assetReference.AssetGUID, out var handle))
            {
                prefab = UnityEngine.Object.Instantiate(handle.Result, parent);
                return handle;
            }
            handle = Addressables.InstantiateAsync(assetReference, position, rotation, parent);
            prefab = handle.WaitForCompletion();
            return handle;
        }

        public AsyncOperationHandle<GameObject> LoadAssetInstance(AssetReferenceT<GameObject> assetReference, out GameObject prefab, Transform parent)
        {
            if (TryGetPreloadedAsset<GameObject>(assetReference.AssetGUID, out var handle))
            {
                prefab = UnityEngine.Object.Instantiate(handle.Result, parent);
                return handle;
            }
            handle = Addressables.InstantiateAsync(assetReference, parent);
            prefab = handle.WaitForCompletion();
            return handle;
        }

        public AsyncOperationHandle<T> LoadAsset<T>(AssetReferenceT<T> assetReference, Action<T> onComplete = null, Action onFail = null) where T : UnityEngine.Object
        {
            if (TryGetPreloadedAsset<T>(assetReference.AssetGUID, out var handle))
            {
                onComplete?.Invoke(handle.Result);
                return handle;
            }
            if (onFail == null)
                onFail = () => LoadingOperationFailed(assetReference);
            else
                onFail += () => LoadingOperationFailed(assetReference);
            handle = Addressables.LoadAssetAsync<T>(assetReference);
            StartCoroutine(WaitForAsset(handle, onComplete, onFail));
            return handle;
        }

        public AsyncOperationHandle<T> LoadAsset<T>(AssetReference assetReference, Action<T> onComplete = null, Action onFail = null) where T : UnityEngine.Object
        {
            if (TryGetPreloadedAsset<T>(assetReference.AssetGUID, out var handle))
            {
                onComplete?.Invoke(handle.Result);
                return handle;
            }
            if (onFail == null)
                onFail = () => LoadingOperationFailed<T>(assetReference);
            else
                onFail += () => LoadingOperationFailed<T>(assetReference);
            handle = Addressables.LoadAssetAsync<T>(assetReference);
            StartCoroutine(WaitForAsset(handle, onComplete, onFail));
            return handle;
        }

        public AsyncOperationHandle<T> LoadAssetImmediate<T>(AssetReference assetReference, Action<T> onComplete = null, Action onFail = null) where T : UnityEngine.Object
        {
            if (TryGetPreloadedAsset<T>(assetReference.AssetGUID, out var handle))
            {
                onComplete?.Invoke(handle.Result);
                return handle;
            }
            if (onFail == null)
                onFail = () => LoadingOperationFailed<T>(assetReference);
            else
                onFail += () => LoadingOperationFailed<T>(assetReference);
            handle = Addressables.LoadAssetAsync<T>(assetReference);
            handle.WaitForCompletion();
            if (ValidateOperation(handle))
                onComplete?.Invoke(handle.Result);
            else
                onFail?.Invoke();
            return handle;
        }

        // Original T is unconstrained and unused; scenes load additively without
        // activation. Subscribe even when the completion delegate is null.
        public AsyncOperationHandle<SceneInstance> LoadScene<T>(string assetAddress, Action<AsyncOperationHandle<SceneInstance>> onComplete = null)
        {
            var handle = Addressables.LoadSceneAsync(assetAddress, LoadSceneMode.Additive, false);
            handle.Completed += onComplete;
            return handle;
        }

        private IEnumerator WaitForAsset<T>(AsyncOperationHandle<T> loadingHandle, Action<T> onComplete, Action onFail = null)
        {
            // Even a completed handle yields once before validation and callbacks.
            yield return loadingHandle;
            if (!ValidateOperation(loadingHandle))
            {
                onFail?.Invoke();
                yield break;
            }
            onComplete?.Invoke(loadingHandle.Result);
        }

        public AsyncOperationHandle<T> LoadAssetGroup<T>(IResourceLocation location, Action<T> onComplete) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(location);
            StartCoroutine(WaitForAsset(handle, onComplete));
            return handle;
        }

        public AsyncOperationHandle<IList<T>> LoadAssetsByLabels<T>(List<AssetLabelReference> labels, Addressables.MergeMode labelMergeMode, Action<IList<T>> onComplete, Action<T> assetLoadedCallback = null) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetsAsync(labels, assetLoadedCallback, labelMergeMode);
            StartCoroutine(WaitForAsset(handle, onComplete));
            return handle;
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadAssetsByLocation(List<AssetLabelReference> labels, Addressables.MergeMode labelMergeMode, Action<IList<IResourceLocation>> onComplete = null)
        {
            var handle = Addressables.LoadResourceLocationsAsync(labels, labelMergeMode);
            StartCoroutine(WaitForAsset(handle, onComplete));
            return handle;
        }

        public AsyncOperationHandle LoadSceneDependencies(string sceneName, Action<AsyncOperationHandle> onComplete)
        {
            var handle = Addressables.DownloadDependenciesAsync(sceneName);
            handle.Completed += onComplete;
            return handle;
        }

        private void Internal_ReleaseHandle(AsyncOperationHandle handle, bool forceRelease = false)
        {
            if (handle.IsValid() && (forceRelease || !m_preloadHandles.Contains(handle)))
                Addressables.Release(handle);
        }

        public void ReleaseHandle(AsyncOperationHandle handle)
        {
            Internal_ReleaseHandle(handle);
        }

        public static void ReleaseHandleInManager(AsyncOperationHandle handle)
        {
            var manager = ProcessManager.GetSystemSafe<AddressableManager>();
            // Original presence check is inverted: a registered manager exits,
            // while an absent manager reaches the dereference. Do not repair it
            // inside the preserved method.
            if (manager != null)
                return;
            manager.Internal_ReleaseHandle(handle);
        }

        public IEnumerator PreloadAssetsFromLabels<T>(List<AssetLabelReference> labels, Action onComplete = null, Action onFail = null) where T : UnityEngine.Object
        {
            if (labels.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }
            var preloadLocationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union, typeof(T));
            m_preloadHandles.Add(preloadLocationsHandle);
            yield return preloadLocationsHandle;
            if (!ValidateOperation(preloadLocationsHandle))
            {
                var label = labels[0].labelString;
                HLOutput.LogError(string.Format("{0} has failed to load {1} assets with label \"{2}\".", "PreloadAssetsFromLabels", labels.Count, label));
                onFail?.Invoke();
                yield break;
            }
            if (preloadLocationsHandle.Result.Count != 0)
            {
                foreach (var resource in preloadLocationsHandle.Result)
                {
                    var assetHandle = Addressables.LoadAssetAsync<T>(resource);
                    assetHandle.Completed += handle =>
                    {
                        // The first primary-key publication wins, but every
                        // completed handle remains in the retained set.
                        m_preloadedAssetHandleLookup.TryAdd(resource.PrimaryKey, handle);
                        m_preloadHandles.Add(handle);
                    };
                }
                bool waitingForPreloadAssets = true;
                while (waitingForPreloadAssets)
                {
                    waitingForPreloadAssets = false;
                    // Scan the entire set using bitwise OR, then yield even on
                    // the final pass where all operations have completed.
                    foreach (var handle in m_preloadHandles)
                        waitingForPreloadAssets |= !handle.IsDone;
                    yield return null;
                }
            }
            onComplete?.Invoke();
        }


        public void ReleasePreloadedAssets()
        {
            foreach (var handle in m_preloadHandles)
                Internal_ReleaseHandle(handle, true);
            m_preloadHandles.Clear();
            m_preloadedAssetHandleLookup.Clear();
        }

        public bool TryGetPreloadedAsset<T>(string id, out AsyncOperationHandle<T> asyncOperationHandle) where T : UnityEngine.Object
        {
            if (m_preloadedAssetHandleLookup.TryGetValue(id, out var handle))
            {
                asyncOperationHandle = handle.Convert<T>();
                return true;
            }
            asyncOperationHandle = default;
            return false;
        }

        public static AsyncOperationHandle<SceneInstance> UnloadSceneAndClearResources(AsyncOperationHandle assetHandle)
        {
            var handle = Addressables.UnloadSceneAsync(assetHandle);
            handle.Completed += operation => Resources.UnloadUnusedAssets();
            return handle;
        }

        private bool ValidateOperation(AsyncOperationHandle asyncOperationHandle)
        {
            // Only reading Status is protected. Exception access, diagnostics and
            // releasing an unretained handle remain outside that catch.
            try
            {
                if (asyncOperationHandle.Status == AsyncOperationStatus.Succeeded)
                    return true;
            }
            catch (Exception exception)
            {
                HLOutput.LogError("Exception when accessing addressable operation handle: \"" + exception.Message + "\"");
            }
            string error = "None";
            if (asyncOperationHandle.IsValid() && asyncOperationHandle.OperationException != null)
                error = asyncOperationHandle.OperationException.ToString();
            HLOutput.LogError(string.Concat("Failed to load asset ", asyncOperationHandle.DebugName, " with error ", error, "."));
            Internal_ReleaseHandle(asyncOperationHandle);
            return false;
        }

        private void LoadingOperationFailed(AssetReferenceT<GameObject> assetReference, Transform parent)
        {
            HLOutput.LogError(string.Format("GUID {0} and RuntimeKey {1} failed to load into parent {2}.", assetReference.AssetGUID, assetReference.RuntimeKey, parent));
        }

        private void LoadingOperationFailed<T>(AssetReferenceT<T> assetReference) where T : UnityEngine.Object
        {
            HLOutput.LogError(string.Format("GUID {0} and RuntimeKey {1} failed to load.", assetReference.AssetGUID, assetReference.RuntimeKey));
        }

        private void LoadingOperationFailed<T>(AssetReference assetReference) where T : UnityEngine.Object
        {
            HLOutput.LogError(string.Format("GUID {0} and RuntimeKey {1} failed to load.", assetReference.AssetGUID, assetReference.RuntimeKey));
        }
    }
}
