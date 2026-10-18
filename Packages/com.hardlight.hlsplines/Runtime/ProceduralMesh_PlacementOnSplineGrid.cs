using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000035; bodies translated from the 1.10.1 x86-64 binary.
	// Member order fixes the closure name <>c__DisplayClass15_0.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_PlacementOnSplineGrid : ProceduralMesh_PlacementOnSpline
	{
		[SerializeField]
		private List<ProceduralMesh_TypeGrid> m_meshTypes = new List<ProceduralMesh_TypeGrid>(1);

		[SerializeField]
		private float m_strideZ = 1f;

		[SerializeField]
		private bool m_scaleStride;

		[SerializeField]
		private float m_gapsRatio;

		// 060000e8..060000eb (1.10.1 RVA 0x1A60220..0x1A60250)
		private List<ProceduralMesh_TypeGrid> MeshTypes => m_meshTypes;

		private float StrideZ => m_strideZ;

		private bool ScaleStride => m_scaleStride;

		private float GapsRatio => m_gapsRatio;

		// 060000ec (1.10.1 RVA 0x1A60260)
		protected override IReadOnlyList<ProceduralMesh_Type> GetMeshTypes()
		{
			return m_meshTypes;
		}

		// 060000ed (1.10.1 RVA 0x1A60270). Strides up to float.Epsilon are rejected; NaN passes.
		protected override List<string> GetErrorMessages()
		{
			List<string> errors = base.GetErrorMessages();
			if (StrideZ <= float.Epsilon)
				errors = AddErrorMessage(errors, string.Format("'{0}' has invalid strideZ {1}", name, StrideZ));
			return errors;
		}

		// 060000ee (1.10.1 RVA 0x1A60330)
		protected override void Reset()
		{
			base.Reset();
			m_meshTypes.Add(new ProceduralMesh_TypeGrid());
		}

		// 060000ef (1.10.1 RVA 0x1A60400). Unless the stride scales with the spline,
		// the spline's local z scale is divided out of it.
		public override void GenerateLocations(ProceduralMesh_StorageForSubSurface storage, ISurface surface, Transform splineTransform)
		{
			ISpline spline = (ISpline)surface;
			List<ProceduralMesh_TypeGrid> meshTypes = MeshTypes;
			float strideZ = StrideZ;
			bool scaleStride = ScaleStride;
			float gapsRatio = GapsRatio;
			Vector3 scale = splineTransform.localScale;
			float zScale = scaleStride ? 1f : 1f / scale.z;
			storage.GenerateGridLocations(splineTransform, meshTypes, gapsRatio, this,
				(startKnot, endKnot) => LocationGenerator.GridPositions(spline, strideZ * zScale, startKnot, endKnot));
		}
	}
}
