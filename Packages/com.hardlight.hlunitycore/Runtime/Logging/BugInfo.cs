using System;

namespace Hardlight
{
    // Original HLUnityCore.Runtime Hardlight.BugInfo 0x0200016a. The
    // identifier is the typed equality/hash key; the name is display text.
    public readonly struct BugInfo : IEquatable<BugInfo>
    {
        private readonly string m_id;
        private readonly string m_name;

        // 0x060009d7; named ARM64 range 0x1afca20..0x1afca54.
        public BugInfo(string id, string name)
        {
            m_id = id;
            m_name = name;
        }

        // 0x060009d8; 0x1afca54..0x1afca74. This is the receiver's
        // instance string.Equals call; a null receiver identifier throws.
        public bool Equals(BugInfo other) => m_id.Equals(other.m_id);

        // 0x060009d9; 0x1afca74..0x1afca98. Preserve the same null edge.
        public override int GetHashCode() => m_id.GetHashCode();

        // 0x060009da; 0x1afca98..0x1afcb08. The native four-string concat
        // treats each null field as empty and retains both fixed separators.
        public override string ToString() => string.Concat("Bug Info: ", m_id, " - ", m_name);
    }
}
