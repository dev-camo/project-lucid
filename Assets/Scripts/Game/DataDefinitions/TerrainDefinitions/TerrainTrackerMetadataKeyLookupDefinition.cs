using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005d1; complete own fields and method API.
    [CreateAssetMenu(fileName = "TerrainTrackerMetadataKeyLookupDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TerrainTrackerMetadataKeyLookupDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TerrainTrackerMetadataKeyLookupDefinition : ScriptableObject
    {
        // 0x040014be; original instance offset 0x18.
        [Tooltip("Key identifier for looking up spline angle influence within metadata.")]
        public Hardlight.MetadataKeyType SplineAngleInfluenceMetadataKey;
        // 0x040014bf; original instance offset 0x20.
        [Tooltip("Key identifier for looking up spline max turn angle within metadata.")]
        public Hardlight.MetadataKeyType SplineTurnAngleMaxMetadataKey;
        // 0x040014c0; original instance offset 0x28.
        [Tooltip("Key identifier for overriding spline turn angle within metadata.")]
        public Hardlight.MetadataKeyType SplineTurnAngleCanBeOverridenMetadataKey;
        // 0x040014c1; original instance offset 0x30.
        [Tooltip("Key identifier for overriding stick to collider force within metadata.")]
        public Hardlight.MetadataKeyType StickToColliderForceMetadataKey;

        // 0x06001f70: both native architectures tail-call the genuine base constructor.
        // No own field initialization, callback, allocation, null test or catch is present.
        public TerrainTrackerMetadataKeyLookupDefinition() { }
    }
}
