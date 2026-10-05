using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ManagedAddressableAsset<T> where T : UnityEngine.Object
    {
        private readonly AssetReferenceT<T> m_assetReference;
        private T m_loadedAsset;
        private AsyncOperationHandle<T> m_assetHandle;
        private int m_refCount;

        // Game.Runtime 0x06000502: Object constructor, then retain the same reference.
        public ManagedAddressableAsset(AssetReferenceT<T> assetReference)
        {
            m_assetReference = assetReference;
        }

        // Game.Runtime 0x06000503: virtual RuntimeKeyIsValid, independent of handle.
        public bool IsValid() => m_assetReference != null && m_assetReference.RuntimeKeyIsValid();

        // Game.Runtime 0x06000504: count increases even if loading later fails.
        // Unity object null semantics govern reuse; store the new handle before wait.
        public T Load()
        {
            m_refCount = unchecked(m_refCount + 1);
            if (m_loadedAsset != null) return m_loadedAsset;
            m_assetHandle = Addressables.LoadAssetAsync<T>(m_assetReference);
            m_loadedAsset = m_assetHandle.WaitForCompletion();
            return m_loadedAsset;
        }

        // Game.Runtime 0x06000505 and closure 0x06000507/0x06000508.
        // An unfinished earlier load is not reused. Completion stores Result before
        // invoking the original caller, without status or reference-count gates.
        public void LoadAsync(Action<T> onLoaded)
        {
            m_refCount = unchecked(m_refCount + 1);
            if (m_loadedAsset != null)
            {
                onLoaded(m_loadedAsset);
                return;
            }
            m_assetHandle = Addressables.LoadAssetAsync<T>(m_assetReference);
            m_assetHandle.Completed += handle =>
            {
                m_loadedAsset = handle.Result;
                onLoaded(m_loadedAsset);
            };
        }

        // Game.Runtime 0x06000506: decrement/clamp and clear before release.
        // Retain the original handle field; Addressables controls its validity.
        public void Unload()
        {
            m_refCount = unchecked(m_refCount - 1);
            if (m_refCount > 0) return;
            m_refCount = 0;
            m_loadedAsset = null;
            if (m_assetHandle.IsValid()) Addressables.Release(m_assetHandle);
        }
    }
}
