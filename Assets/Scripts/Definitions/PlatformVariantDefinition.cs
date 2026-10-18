using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000592: the platform variant prefabs of one asset type,
    // looked up by component type.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "PlatformVariantDefinition", menuName = "HardlightProject/DefinitionData/Definitions/PlatformVariantDefinition")]
    public class PlatformVariantDefinition : ScriptableObject
    {
        [SerializeField] private PlatformAssetTypes m_assetType;
        [SerializeField] private PlatformComponentDefinition[] m_components;

        public PlatformAssetTypes AssetType => m_assetType;

        // 06001e98: the first matching entry wins; a null entry throws in the predicate.
        public PlatformVariant Get(PlatformComponentTypes componentType) =>
            Array.Find(m_components, component => component.Type == componentType)?.PrefabSource;

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class PlatformComponentDefinition
        {
            public PlatformComponentTypes Type;
            public PlatformVariant PrefabSource;
        }
    }
}
