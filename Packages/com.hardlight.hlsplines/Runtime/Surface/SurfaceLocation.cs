using UnityEngine;

namespace Hardlight
{
    // Original0200007f has six public fields and no declared methods/constructors.
    public struct SurfaceLocation
    {
        public ISurface m_surface;
        public Vector3 m_localPosition;
        public Vector3 m_worldPosition;
        public Quaternion m_worldRotation;
        public PositionBoundsInfo m_positionBoundsInfo;
        public SurfaceKnotMetadata m_metadata;
    }
}
