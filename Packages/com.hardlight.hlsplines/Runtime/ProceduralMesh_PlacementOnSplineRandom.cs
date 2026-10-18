using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000037; bodies translated from the 1.10.1 x86-64 binary.
	// Member order fixes the closure name <>c__DisplayClass16_0.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_PlacementOnSplineRandom : ProceduralMesh_PlacementOnSpline
	{
		[SerializeField]
		private List<ProceduralMesh_TypeRandom> m_meshTypes = new List<ProceduralMesh_TypeRandom>(1);

		[SerializeField]
		private NumMeshesType m_numMeshesType;

		[SerializeField]
		[FormerlySerializedAs("m_numMeshesPerKnot")]
		private float m_numMeshes;

		[SerializeField]
		private bool m_scaleNumMeshes;

		// 060000f3..060000f6 (1.10.1 RVA 0x1A60740..0x1A60770)
		private List<ProceduralMesh_TypeRandom> MeshTypes => m_meshTypes;

		private NumMeshesType NumMeshesType => m_numMeshesType;

		private float NumMeshes => m_numMeshes;

		private bool ScaleNumMeshes => m_scaleNumMeshes;

		// 060000f7 (1.10.1 RVA 0x1A60780)
		protected override IReadOnlyList<ProceduralMesh_Type> GetMeshTypes()
		{
			return m_meshTypes;
		}

		// 060000f8 (1.10.1 RVA 0x1A60790). Only negative counts are rejected; NaN passes.
		protected override List<string> GetErrorMessages()
		{
			List<string> errors = base.GetErrorMessages();
			if (NumMeshes < 0f)
				errors = AddErrorMessage(errors, string.Format("'{0}' has invalid Num Meshes {1}", name, NumMeshes));
			return errors;
		}

		// 060000f9 (1.10.1 RVA 0x1A60840). The added type's seed of -1 is set here (0x1A60896);
		// its constructor leaves it at 0.
		protected override void Reset()
		{
			base.Reset();
			m_meshTypes.Add(new ProceduralMesh_TypeRandom { Seed = -1 });
		}

		// 060000fa (1.10.1 RVA 0x1A60940). A spline has no width, so its "area" is
		// the summed length of the sub-surface's knots, end knot excluded.
		private static float CalcSplineArea(ProceduralMesh_StorageForSubSurface storage, ISpline spline)
		{
			int startKnot = storage.StartSubSurface;
			int endKnot = storage.EndSubSurface;
			float area = 0f;
			for (int i = startKnot; i < endKnot; ++i)
				area += spline.GetKnot(i).Length;
			return area;
		}

		// 060000fb (1.10.1 RVA 0x1A60AC0). PerMetreSquare scales the count by the sub-surface length.
		public override void GenerateLocations(ProceduralMesh_StorageForSubSurface storage, ISurface surface, Transform splineTransform)
		{
			ISpline spline = (ISpline)surface;
			List<ProceduralMesh_TypeRandom> meshTypes = MeshTypes;
			NumMeshesType numMeshesType = NumMeshesType;
			float numMeshes = NumMeshes;
			bool scaleNumMeshes = ScaleNumMeshes;
			if (numMeshesType == NumMeshesType.PerMetreSquare)
				numMeshes *= CalcSplineArea(storage, spline);
			storage.GenerateRandomLocations(splineTransform, meshTypes, numMeshesType, numMeshes, scaleNumMeshes, this,
				(random, numMeshesToCreate, startKnot, endKnot, meshType) => LocationGenerator.RandomPositions(random, spline, numMeshesToCreate, startKnot, endKnot));
		}
	}
}
