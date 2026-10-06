using System.Collections.Generic;

namespace Hardlight
{
    // Original0200007b is a complete two-field struct with zero own methods.
    public struct Collision2DInput
    {
        public Dictionary<ISurface, ISurface[]> m_world;
        public SurfaceLocation m_currentLocation;
    }
}
