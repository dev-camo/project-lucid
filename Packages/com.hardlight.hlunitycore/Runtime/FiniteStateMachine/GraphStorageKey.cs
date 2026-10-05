using System;

namespace Hardlight
{
    public readonly struct GraphStorageKey : IEquatable<GraphStorageKey>
    {
        public readonly int GraphId;
        public readonly int NodeId;
        public readonly int NameId;
        private readonly int m_hashCode;

        // HLUnityCore.Runtime.dll:Hardlight.GraphStorageKey:0x060008b0;
        // arm64 0x1af1fe0.
        public static implicit operator GraphStorageKey(int nameId) => new GraphStorageKey(nameId);

        // 0x060008b1; arm64 0x1af2114.
        public GraphStorageKey(string name, int nodeId = 0, int graphId = 0)
            : this(GraphNameLookup.ConvertNameToId(name), nodeId, graphId) { }

        // 0x060008b2; arm64 0x1af21fc. Resolve names in name/node/graph order.
        public GraphStorageKey(string name, string nodeId, string graphId)
            : this(GraphNameLookup.ConvertNameToId(name), GraphNameLookup.ConvertNameToId(nodeId),
                GraphNameLookup.ConvertNameToId(graphId)) { }

        // 0x060008b3; arm64 0x1af2070. HashCode.Combine<int,int,int> is the
        // original managed library dependency; the cached hash is not serialized.
        public GraphStorageKey(int nameId, int nodeId = 0, int graphId = 0)
        {
            GraphId = graphId;
            NodeId = nodeId;
            NameId = nameId;
            m_hashCode = HashCode.Combine(GraphId, NodeId, NameId);
        }

        // 0x060008b4; arm64 0x1af22fc. "null" represents an omitted graph/node,
        // whereas the zero name resolves to an empty string. Separator is "_".
        public override string ToString() => string.Concat(
            GraphId == 0 ? "null" : GraphNameLookup.LookupNameUsingId(GraphId), "_",
            NodeId == 0 ? "null" : GraphNameLookup.LookupNameUsingId(NodeId), "_",
            GraphNameLookup.LookupNameUsingId(NameId));

        // 0x060008b5; arm64 0x1af24e4. Equality compares IDs only.
        public bool Equals(GraphStorageKey other) => GraphId == other.GraphId &&
            NodeId == other.NodeId && NameId == other.NameId;
        // 0x060008b6; arm64 0x1af2518.
        public override bool Equals(object obj) => obj is GraphStorageKey other && Equals(other);
        // 0x060008b7; arm64 0x1af25c8.
        public override int GetHashCode() => m_hashCode;
    }
}
