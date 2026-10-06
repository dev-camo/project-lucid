using System;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "UIContainerIdentifier", menuName = "Hardlight/HLModernUI/UIContainerIdentifier", order = 0)]
    public sealed class UIContainerIdentifier : ScriptableObjectWithGuid
    {
        [SerializeField] private string m_name;
        [SerializeField] private UIContainer m_prefab;
        [SerializeField] private AssetReferenceT<GameObject> m_uiContainerAsset;
        [NonSerialized] private UIContainer m_loadedUIContainer;
        [NonSerialized] private AsyncOperationHandle<GameObject> m_operationHandle;
        [NonSerialized] private int m_refCount;

        // Original06000027: own field, independent of Unity Object.name.
        public string Name => m_name;

        // Original06000028: the serialized legacy m_prefab field is retained but
        // this getter loads the AssetReference and does not acquire a refcount.
        public UIContainer Prefab
        {
            get
            {
                if (m_loadedUIContainer != null) return m_loadedUIContainer;
                m_operationHandle = Addressables.LoadAssetAsync<GameObject>(m_uiContainerAsset);
                m_loadedUIContainer = m_operationHandle.WaitForCompletion().GetComponent<UIContainer>();
                return m_loadedUIContainer;
            }
        }

        // Original06000029 uses genuine Unity Object inequality.
        public bool IsLoaded => m_loadedUIContainer != null;
        // Original0600002a returns the field, including a destroyed Unity wrapper.
        public UIContainer LoadedUIContainer => m_loadedUIContainer;
        // Original0600002b retains the genuine resource helper chain.
        public static UIContainerIdentifier FindByName(string name) => ObjectUtils.FindByName<UIContainerIdentifier>(name);

        // Original0600002c: decrement first, clamp only on release route, clear
        // loaded object before handle validation, and retain the old handle field.
        public void Close()
        {
            m_refCount = unchecked(m_refCount - 1);
            if (m_refCount > 0) return;
            m_loadedUIContainer = null;
            m_refCount = 0;
            if (m_operationHandle.IsValid()) Addressables.Release(m_operationHandle);
        }

        // Original0600002d stores even null, then increments the live counter.
        public void RegisterContainer(UIContainer container)
        {
            m_loadedUIContainer = container;
            m_refCount = unchecked(m_refCount + 1);
        }

        // Original0600002e does not compare the parameter, release the handle or
        // clamp the decremented count. Preserve those original differences.
        public void UnregisterContainer(UIContainer container)
        {
            m_loadedUIContainer = null;
            m_refCount = unchecked(m_refCount - 1);
        }

        // Original0600002f captures the loaded object before acquiring, then tests
        // that captured object and reloads the return field. Loading faults retain
        // the acquisition.
        public UIContainer GetOrCreateContainer()
        {
            UIContainer loadedContainer = m_loadedUIContainer;
            m_refCount = unchecked(m_refCount + 1);
            if (loadedContainer != null) return m_loadedUIContainer;
            m_operationHandle = Addressables.LoadAssetAsync<GameObject>(m_uiContainerAsset);
            m_loadedUIContainer = m_operationHandle.WaitForCompletion().GetComponent<UIContainer>();
            return m_loadedUIContainer;
        }
        // Original06000030: implicit genuine ScriptableObjectWithGuid constructor.
    }
}
