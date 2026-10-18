using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200002c; bodies translated from the 1.10.1 x86-64 binary.
	public abstract class ProceduralMesh_PlacementOnKnotSurface : ProceduralMesh_Placement
	{
		[SerializeField]
		private int m_startKnot;

		// -1 selects the last knot of the surface.
		[SerializeField]
		private int m_endKnot = -1;

		// 060000ac (1.10.1 RVA 0x1A5ACF0)
		protected int StartKnot => m_startKnot;

		// 060000ad (1.10.1 RVA 0x1A5AD00)
		protected int EndKnot => m_endKnot;
	}
}
