using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x020000ab, complete six methods from both native architectures.
    // Shipping getter flags are final/virtual despite this owner's empty interface list;
    // ordinary C# properties retain the behavior while exact emitted flag binding remains held.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SplineMonoBase : MonoBehaviour
    {
        [SerializeField] protected SplineType m_splineType = SplineType.CatmullRom;
        [HideInInspector, SerializeField] protected float m_length;
        [SerializeField] protected Bounds m_boundingBox;
        [SerializeField] protected internal MetadataGroups m_metadata;
        private readonly Vector3[] m_boundsCorners = new Vector3[8];

        public SplineType SplineType => m_splineType; // 0x0600059a
        public float Length => m_length; // 0x0600059b
        public Bounds BoundingBox => m_boundingBox; // 0x0600059c
        public MetadataGroups Metadata => m_metadata; // 0x0600059d

        // 0x0600059e: capture the local bounds before reading transform. The original
        // inlined corner update uses the owned eight-element array, then reads the matrix.
        public Bounds GetBoundingBox(bool inWorldSpace = false)
        {
            return inWorldSpace ? m_boundingBox.LocalToWorld(transform, m_boundsCorners) : m_boundingBox;
        }

        // 0x0600059f: CatmullRom and the corner array are initialized before the base call.
        public SplineMonoBase() { }
    }
}
