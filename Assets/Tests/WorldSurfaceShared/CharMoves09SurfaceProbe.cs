using System;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // A surface component whose nearest location is a fixed one, for the transporter proximity auto-attach path, which
    // only needs the stored surface to be a Component and to answer FindNearestSurfaceLocationFromWorld.
    public sealed class CharMoves09SurfaceProbe : MonoBehaviour, ISurface
    {
        public bool ReturnsItself = true;
        public Vector3 NearestPosition;
        public Quaternion NearestRotation = Quaternion.identity;

        public float Length => 0f;
        public float LengthInverse => 0f;

        public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => new SurfaceLocation
        {
            m_surface = ReturnsItself ? this : null,
            m_worldPosition = NearestPosition,
            m_worldRotation = NearestRotation,
        };

        public DateTime GetTimestamp() => default;
        public MetadataGroups GetSurfaceMetadata() => null;
        public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => default;
        public Bounds GetBoundingBox(bool inWorldSpace = false) => default;
        public Vector3 TransformPoint(Vector3 point) => point;
        public Vector3 InverseTransformPoint(Vector3 point) => point;
        public bool IsActive() => true;
    }
}
