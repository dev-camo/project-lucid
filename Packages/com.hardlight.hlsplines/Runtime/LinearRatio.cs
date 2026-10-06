namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200006b, 0x06000289..28d; no index/factor clamping.
    public struct LinearRatio
    {
        public LinearRatio(int knotIndex, float t) { KnotIndex = knotIndex; T = t; }
        public int KnotIndex { get; set; }
        public float T { get; set; }
    }
}
