using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000774: one component of a platform group, holding a
    // variant for each asset type. The retail player keeps only its data and accessors.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PlatformComponent : MonoBehaviour
    {
        [SerializeField] private PlatformComponentTypes m_componentType;
        [SerializeField] private PlatformAssetTypes m_activeAssetType = PlatformAssetTypes.Debug;
        [InspectorReadOnly] [SerializeField] private int m_height = 1;
        [SerializeField] private List<PlatformVariantInstance> m_variants = new List<PlatformVariantInstance>();
        private float m_basePieceHeight;
        private float m_pieceSize;
        private PlatformAssetTypes m_cachedAssetType;

        public PlatformAssetTypes ActiveAssetType
        {
            get => m_activeAssetType;
            set => m_activeAssetType = value;
        }

        public PlatformComponentTypes ComponentType
        {
            get => m_componentType;
            set => m_componentType = value;
        }
    }
}
