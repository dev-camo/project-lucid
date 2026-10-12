using System.Collections.Generic;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;

// A normally initialized, test-owned serialized data host. This source file
// matches its class name so its persistent YAML MonoScript identity is explicit.
public sealed class ProjectLucidFontReferenceConversionCarrier : ScriptableObject
{
    [SerializeField] private List<AssetReferenceT<FontAssetAndFallbacks>> m_fontAssetsAndFallBackCollectionRefs =
        new List<AssetReferenceT<FontAssetAndFallbacks>>();

    public IReadOnlyList<AssetReferenceT<FontAssetAndFallbacks>> CollectionReferences => m_fontAssetsAndFallBackCollectionRefs;
}
