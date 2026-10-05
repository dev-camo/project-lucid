using System;

namespace Hardlight
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GraphNodeMenuFormatAttribute : Attribute
    {
        public readonly string Format;
        // HLUnityCore.Runtime.dll:Hardlight.GraphNodeMenuFormatAttribute:0x06000c3d;
        // arm64 0x1b0a6c0: the obsolete name argument is ignored.
        [Obsolete("Name is obsolete, use GraphDisplayNameAttribute instead.")]
        public GraphNodeMenuFormatAttribute(string format, string name) { Format = format; }
        // 0x06000c3e; arm64 0x1b0a6f4: direct field store.
        public GraphNodeMenuFormatAttribute(string format) { Format = format; }
    }
}
