using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original0200007d is a complete four-field struct with zero own methods.
    public struct RayHit2DInput
    {
        public List<ISurface> m_surfaces;
        public Ray m_ray;
        public float m_distance;
        public SurfaceLocation m_currentLocation;
    }
}
