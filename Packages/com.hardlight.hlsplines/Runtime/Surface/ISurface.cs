using System;
using UnityEngine;

namespace Hardlight
{
    // Original02000069 declares these ten genuine contracts; zero body credit.
    public interface ISurface
    {
        DateTime GetTimestamp();
        MetadataGroups GetSurfaceMetadata();
        SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition);
        SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition);
        float Length { get; }
        float LengthInverse { get; }
        Bounds GetBoundingBox(bool inWorldSpace = false);
        Vector3 TransformPoint(Vector3 point);
        Vector3 InverseTransformPoint(Vector3 point);
        bool IsActive();
    }
}
