using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MeshSurface : ISurface
    {
        // HLSplines.Runtime 06000072: sole readonly original auto-property.
        public MeshCollider Mesh { get; }

        // 06000073: Object base initialization precedes this captured assignment.
        public MeshSurface(MeshCollider meshCollider)
        {
            Mesh = meshCollider;
        }

        // 06000074: the supplied original genuinely returns null metadata.
        public MetadataGroups GetSurfaceMetadata() => null;

        // 06000075..78: these four original native bodies throw parameterless
        // NotImplementedException. They are native behavior, not recovery placeholders.
        public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition)
            => throw new NotImplementedException();
        public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition)
            => throw new NotImplementedException();
        [Obsolete("MeshSurface Does not support Length")]
        public float Length => throw new NotImplementedException();
        [Obsolete("MeshSurface Does not support LengthInverse")]
        public float LengthInverse => throw new NotImplementedException();

        // 06000079: world uses Collider.bounds; local uses sharedMesh.bounds.
        public Bounds GetBoundingBox(bool inWorldSpace = false)
            => inWorldSpace ? Mesh.bounds : Mesh.sharedMesh.bounds;

        // 0600007a..7c: genuine Component/Transform/GameObject engine routes.
        public Vector3 TransformPoint(Vector3 point) => Mesh.transform.TransformPoint(point);
        public Vector3 InverseTransformPoint(Vector3 point) => Mesh.transform.InverseTransformPoint(point);
        public bool IsActive() => Mesh.gameObject.activeInHierarchy;

        // 0600007d: current local DateTime.Now, not a stored collider timestamp.
        public DateTime GetTimestamp() => DateTime.Now;
    }
}
