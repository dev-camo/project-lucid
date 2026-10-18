using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000775: a platform group laid out on a grid from corner,
    // edge and centre pieces. The retail player keeps only its data.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PlatformFlexibleGroup : PlatformGroup
    {
        [SerializeField] [Range(2, 25)] protected int m_width = 2;
        [Range(2, 25)] [SerializeField] protected int m_length = 2;
        [SerializeField] protected int m_gridSize = 6;
        [SerializeField] protected Vector3 m_startingPoint = Vector3.zero;
        [SerializeField] protected bool m_useSimpleCentrePiece = true;
        [SerializeField] private PlatformPiece m_cornerBackLeft;
        [SerializeField] private PlatformPiece m_cornerBackRight;
        [SerializeField] private PlatformPiece m_cornerFrontLeft;
        [SerializeField] private PlatformPiece m_cornerFrontRight;
        [SerializeField] private PlatformPiece m_edgePiece;
        [SerializeField] private PlatformPiece m_edgePiece2;
        [SerializeField] private PlatformPiece m_edgePiece4;
        [SerializeField] private PlatformPiece m_edgePiece6;
        [SerializeField] private PlatformPiece m_centerPiece;
        [SerializeField] private List<PlatformPiece> m_constructedInstances = new List<PlatformPiece>();
    }
}
