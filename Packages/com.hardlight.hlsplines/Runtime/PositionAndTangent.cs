using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200006c, 0x0600028e..292; unnormalized inputs.
    public struct PositionAndTangent
    {
        public PositionAndTangent(Vector3 position, Vector3 tangent) { Position = position; Tangent = tangent; }
        public Vector3 Position { get; set; }
        public Vector3 Tangent { get; set; }
    }
}
