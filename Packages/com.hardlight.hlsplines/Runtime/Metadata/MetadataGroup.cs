using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class MetadataGroup : ScriptableObjectWithGuid
    {
        public abstract IReadOnlyList<Metadata> Metadata { get; }
        public bool TryGetMetadata(MetadataKeyType key, out Metadata metadata) => MetadataUtilities.TryGetMetadata(Metadata, key, out metadata);
        public bool TryGetMetadata(MetadataKeyType key, out List<Metadata> metadata) => MetadataUtilities.TryGetMetadata(Metadata, key, out metadata);
        public bool KeyTypeExists(MetadataKeyType key) => MetadataUtilities.TryGetMetadata(Metadata, key, out Metadata metadata);
        public bool TryGetValue(MetadataKeyType key, out string value) => MetadataUtilities.TryGetValue(Metadata, key, out value);
        public bool TryGetValue(MetadataKeyType key, out int value) => MetadataUtilities.TryGetValue(Metadata, key, out value);
        public bool TryGetValue(MetadataKeyType key, out float value) => MetadataUtilities.TryGetValue(Metadata, key, out value);
        public bool TryGetValue(MetadataKeyType key, out bool value) => MetadataUtilities.TryGetValue(Metadata, key, out value);
        public bool TryGetObject<TObject>(MetadataKeyType key, out TObject value) where TObject : UnityEngine.Object => MetadataUtilities.TryGetObject(Metadata, key, out value);
        public bool TryGetValue<TEnum>(MetadataKeyType key, out TEnum value) where TEnum : struct, Enum => MetadataUtilities.TryGetValue(Metadata, key, out value);
        protected MetadataGroup() { }
    }
}
