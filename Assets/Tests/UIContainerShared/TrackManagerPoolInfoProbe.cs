using System.Text;
using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only TrackManager whose virtual DebugGetInstancePrefabPoolsInfo appends a known marker, recording the call.
    // Editor-only assemblies cannot supply attachable components, so it lives in this runtime test assembly.
    public sealed class TrackManagerPoolInfoProbe : TrackManager
    {
        public const string Marker = "instance pool info marker";
        public int Calls;

        public override void DebugGetInstancePrefabPoolsInfo(StringBuilder stringInfoBuilder)
        {
            Calls++;
            stringInfoBuilder.Append(Marker);
        }
    }
}
