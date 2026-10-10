using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace HardlightProject
{
    // Original Game.Runtime 020006cf; ten fields and eight complete owned methods.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class VisualAssetManager : MonoBehaviour, ISystem
    {
        [SerializeField] private PerformanceAttribute m_requirementForHDUI;
        [SerializeField] private string[] m_atlasesWithVariants = Array.Empty<string>();
        [SerializeField] private DevicePerformanceGroup m_devicePerformanceGroup;
        [SerializeField] private string m_sdAtlasSuffix = "_SD";
        public const string CharactersUIAtlas = "Characters_UI_Atlas";
        private readonly Dictionary<string, Shader> m_shaderLookup = new Dictionary<string, Shader>();
        private readonly Dictionary<string, AsyncOperationHandle<SpriteAtlas>> m_atlasHandles = new Dictionary<string, AsyncOperationHandle<SpriteAtlas>>();
        private readonly List<AssetLabelReference> m_shaderLabelReferences = new List<AssetLabelReference>();
        private readonly SystemRef<AddressableManager> m_addressablesManager = ProcessManager.GetSystemRef<AddressableManager>(null, true);
        private string m_uiSuffix;

        // Original 060024ea: registration and profile selection precede event subscription.
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            ProcessManager.RegisterSystem(this, null, false, false);
            PerformanceProfile profile = VisualQualityManager_SDT.GetDefaultPerformanceProfile(m_devicePerformanceGroup.GetData(), null);
            if (profile != null && !profile.IsSupported(m_requirementForHDUI))
                m_uiSuffix = m_sdAtlasSuffix;
            SpriteAtlasManager.atlasRequested += RequestAtlas;
            m_shaderLabelReferences.Add(new AssetLabelReference { labelString = "shaders" });
            m_addressablesManager.InvokeOnValid(OnAddressableManagerLoaded);
        }

        // Original 060024eb always loads and overwrites the selected dictionary key.
        // A null callback skips completion waiting; a live callback rereads the handle.
        public void RequestAtlas(string atlasTag, Action<SpriteAtlas> callback)
        {
            if (m_atlasesWithVariants.Contains(atlasTag))
                atlasTag += m_uiSuffix;
            m_atlasHandles[atlasTag] = Addressables.LoadAssetAsync<SpriteAtlas>(atlasTag);
            if (callback != null)
                callback(m_atlasHandles[atlasTag].WaitForCompletion());
        }

        // Original 060024ec uses the actual shader label load API and Union enum value1.
        private void OnAddressableManagerLoaded(AddressableManager addressableManager)
        {
            addressableManager.LoadAssetsByLabels<Shader>(m_shaderLabelReferences, Addressables.MergeMode.Union, null, OnShaderLoaded);
        }

        // Original 060024ed preserves Dictionary.Add and its duplicate-name fault.
        private void OnShaderLoaded(Shader shader) => m_shaderLookup.Add(shader.name, shader);
        // Original 060024ee preserves the genuine original collection helper.
        public Shader Get(string shaderName) => m_shaderLookup.GetValueOrDefault(shaderName);

        // Original 060024ef releases the found handle before rereading/removing its key.
        public void UnloadAtlas(string atlasTag)
        {
            if (m_atlasHandles.TryGetValue(atlasTag, out AsyncOperationHandle<SpriteAtlas> handle))
            {
                Addressables.Release(handle);
                m_atlasHandles.Remove(atlasTag);
            }
        }

        // Original 060024f0 only unregisters. The atlas event and live handles are retained.
        private void OnDestroy() => ProcessManager.UnregisterSystem(this);
        // Original 060024f1 field initializers precede the original MonoBehaviour constructor.
        public VisualAssetManager() { }
    }
}
