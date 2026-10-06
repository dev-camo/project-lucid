using System.Collections.Generic;

namespace Hardlight
{
    public class RibbonAlignmentEqualityComparer : IEqualityComparer<RibbonAlignment>
    {
        // HLSplines.Runtime 0x06000352; original ARM64 0x1a823dc.
        public bool Equals(RibbonAlignment a, RibbonAlignment b) { return a == b; }

        // HLSplines.Runtime 0x06000353; original ARM64 0x1a823e8.
        public int GetHashCode(RibbonAlignment a) { return (int)a; }

        // HLSplines.Runtime 0x06000354; original ARM64 0x1a823f0.
        public RibbonAlignmentEqualityComparer() { }
    }
}
