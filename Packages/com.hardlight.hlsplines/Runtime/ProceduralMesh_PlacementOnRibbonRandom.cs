using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000032; bodies translated from the 1.10.1 x86-64 binary.
	// Member order fixes the closure name <>c__DisplayClass16_0.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_PlacementOnRibbonRandom : ProceduralMesh_PlacementOnRibbon
	{
		[SerializeField]
		private List<ProceduralMesh_TypeRibbonRandom> m_meshTypes = new List<ProceduralMesh_TypeRibbonRandom>(1);

		[SerializeField]
		private NumMeshesType m_numMeshesType;

		[SerializeField]
		[FormerlySerializedAs("m_numMeshesPerKnot")]
		private float m_numMeshes;

		[SerializeField]
		private bool m_scaleNumMeshes;

		// 060000d4..060000d7 (1.10.1 RVA 0x1A5DF00..0x1A5DF30)
		private List<ProceduralMesh_TypeRibbonRandom> MeshTypes => m_meshTypes;

		private NumMeshesType NumMeshesType => m_numMeshesType;

		private float NumMeshes => m_numMeshes;

		private bool ScaleNumMeshes => m_scaleNumMeshes;

		// 060000d8 (1.10.1 RVA 0x1A5DF40)
		protected override IReadOnlyList<ProceduralMesh_Type> GetMeshTypes()
		{
			return m_meshTypes;
		}

		// 060000d9 (1.10.1 RVA 0x1A5DF50). Only negative counts are rejected; NaN passes.
		protected override List<string> GetErrorMessages()
		{
			List<string> errors = base.GetErrorMessages();
			if (NumMeshes < 0f)
				errors = AddErrorMessage(errors, string.Format("'{0}' has invalid Num Meshes {1}", name, NumMeshes));
			return errors;
		}

		// 060000da (1.10.1 RVA 0x1A5E000). The added type's seed of -1 is set here (0x1A5E06B);
		// its constructor leaves it at 0.
		protected override void Reset()
		{
			base.Reset();
			m_meshTypes.Add(new ProceduralMesh_TypeRibbonRandom { Seed = -1 });
		}

		// 060000db (1.10.1 RVA 0x1A5E120). Trapezoid rule over each sub-surface knot
		// span: the mean left and right half-widths of a span times the span's
		// centre-knot length.
		private static float CalculateRibbonArea(ProceduralMesh_StorageForSubSurface storage, IRibbon ribbon)
		{
			int startKnot = storage.StartSubSurface;
			int endKnot = storage.EndSubSurface;
			IRibbonKnot knot = ribbon.GetKnot(startKnot, RibbonAlignment.Center);
			IRibbonKnot leftKnot = ribbon.GetKnot(startKnot, RibbonAlignment.Left);
			IRibbonKnot rightKnot = ribbon.GetKnot(startKnot, RibbonAlignment.Right);
			Vector3 position = knot.Transform.Location;
			float leftWidth = Vector3.Distance(position, leftKnot.Transform.Location);
			float rightWidth = Vector3.Distance(position, rightKnot.Transform.Location);
			float area = 0f;
			for (int i = startKnot; i < endKnot; ++i)
			{
				IRibbonKnot nextKnot = ribbon.GetKnot(i + 1, RibbonAlignment.Center);
				IRibbonKnot nextLeftKnot = ribbon.GetKnot(i + 1, RibbonAlignment.Left);
				IRibbonKnot nextRightKnot = ribbon.GetKnot(i + 1, RibbonAlignment.Right);
				Vector3 nextPosition = nextKnot.Transform.Location;
				float nextLeftWidth = Vector3.Distance(nextPosition, nextLeftKnot.Transform.Location);
				float nextRightWidth = Vector3.Distance(nextPosition, nextRightKnot.Transform.Location);
				area += (leftWidth + nextLeftWidth) * 0.5f * knot.Length;
				area += (rightWidth + nextRightWidth) * 0.5f * knot.Length;
				knot = nextKnot;
				leftWidth = nextLeftWidth;
				rightWidth = nextRightWidth;
			}
			return area;
		}

		// 060000dc (1.10.1 RVA 0x1A5EB00). PerMetreSquare scales the count by the sub-surface area.
		public override void GenerateLocations(ProceduralMesh_StorageForSubSurface storage, ISurface surface, Transform ribbonTransform)
		{
			IRibbon ribbon = (IRibbon)surface;
			List<ProceduralMesh_TypeRibbonRandom> meshTypes = MeshTypes;
			NumMeshesType numMeshesType = NumMeshesType;
			float numMeshes = NumMeshes;
			bool scaleNumMeshes = ScaleNumMeshes;
			if (numMeshesType == NumMeshesType.PerMetreSquare)
				numMeshes *= CalculateRibbonArea(storage, ribbon);
			storage.GenerateRandomLocations(ribbonTransform, meshTypes, numMeshesType, numMeshes, scaleNumMeshes, this,
				(random, numMeshesToCreate, startKnot, endKnot, meshType) => LocationGenerator.RandomPositions(random, ribbon, numMeshesToCreate,
					meshType.BiasMin, meshType.BiasMax, meshType.BiasType, meshType.BiasDensity, meshType.InverseBias, startKnot, endKnot));
		}
	}
}
