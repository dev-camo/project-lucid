namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200006a, 0x06000284..288; mutable raw coordinates.
    public struct KnotT
    {
        public KnotT(int knotIndex, float t) { KnotIndex = knotIndex; T = t; }
        public int KnotIndex { get; set; }
        public float T { get; set; }
    }
}
