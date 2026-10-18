using System;
using Hardlight;

// Owned MetadataValueType with no Reset of its own. The Editor sends Reset to every
// ScriptableObject made by CreateInstance, and each shipped value type's Reset stores its
// label, so only a subclass that inherits ScriptableObjectWithGuid.Reset (a single RET)
// still shows the MetadataValueType field initialisers after creation.
public sealed class OriginalMetadataValueTypeCarrier : MetadataValueType
{
    public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
    {
        throw new NotSupportedException("The carrier only observes the base initialisers.");
    }
}
