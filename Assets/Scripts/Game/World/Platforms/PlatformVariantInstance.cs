using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000779: a platform variant tagged with its asset type,
    // named after that type in the inspector.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class PlatformVariantInstance : ISerializationCallbackReceiver
    {
        [SerializeField] [HideInInspector] private string m_name;
        [SerializeField] private PlatformAssetTypes m_assetType = PlatformAssetTypes.Debug;
        public PlatformVariant PlatformVariant;

        public PlatformAssetTypes AssetType => m_assetType;

        public PlatformVariantInstance(PlatformAssetTypes assetType, PlatformVariant variant)
        {
            m_assetType = assetType;
            PlatformVariant = variant;
        }

        public void OnBeforeSerialize()
        {
            m_name = m_assetType.GetString();
        }

        public void OnAfterDeserialize()
        {
        }
    }
}
