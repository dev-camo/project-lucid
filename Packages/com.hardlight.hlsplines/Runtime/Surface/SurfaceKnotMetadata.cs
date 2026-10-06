namespace Hardlight
{
    public readonly struct SurfaceKnotMetadata
    {
        public MetadataGroups Surface { get; }
        public MetadataGroups KnotA { get; }
        public MetadataGroups KnotB { get; }
        public float T { get; }

        // HLSplines.Runtime06000310: no knot reference is synthesized from surface.
        public SurfaceKnotMetadata(MetadataGroups surface)
        {
            Surface = surface;
            KnotA = null;
            KnotB = null;
            T = 0f;
        }

        //06000311: surface/knotA publish before null knotB and zero T.
        public SurfaceKnotMetadata(MetadataGroups surface, MetadataGroups knotA)
        {
            Surface = surface;
            KnotA = knotA;
            KnotB = null;
            T = 0f;
        }

        //06000312: direct ordered reference/Single stores, including non-finite T.
        public SurfaceKnotMetadata(MetadataGroups surface, MetadataGroups knotA, MetadataGroups knotB, float t)
        {
            Surface = surface;
            KnotA = knotA;
            KnotB = knotB;
            T = t;
        }
    }
}
