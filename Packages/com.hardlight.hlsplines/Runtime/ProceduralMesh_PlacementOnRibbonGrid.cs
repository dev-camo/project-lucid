using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000030; bodies translated from the 1.10.1 x86-64 binary.
	// Member order fixes the closure name <>c__DisplayClass27_0.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_PlacementOnRibbonGrid : ProceduralMesh_PlacementOnRibbon
	{
		[SerializeField]
		private List<ProceduralMesh_TypeGrid> m_meshTypes = new List<ProceduralMesh_TypeGrid>(1);

		[SerializeField]
		private float m_strideZ = 1f;

		[SerializeField]
		private float m_strideX = 1f;

		[SerializeField]
		private bool m_scaleStride;

		[SerializeField]
		private float m_biasMin = -1f;

		[SerializeField]
		private float m_biasMax = 1f;

		[SerializeField]
		private LocationGenerator.BiasType m_biasType;

		[SerializeField]
		private float m_gapsRatio;

		// 060000c5..060000cc (1.10.1 RVA 0x1A5D830..0x1A5D8A0)
		private List<ProceduralMesh_TypeGrid> MeshTypes => m_meshTypes;

		private float StrideZ => m_strideZ;

		private float StrideX => m_strideX;

		private bool ScaleStride => m_scaleStride;

		private float BiasMin => m_biasMin;

		private float BiasMax => m_biasMax;

		private LocationGenerator.BiasType BiasType => m_biasType;

		private float GapsRatio => m_gapsRatio;

		// 060000cd (1.10.1 RVA 0x1A5D8B0)
		protected override IReadOnlyList<ProceduralMesh_Type> GetMeshTypes()
		{
			return m_meshTypes;
		}

		// 060000ce (1.10.1 RVA 0x1A5D8C0). Strides up to float.Epsilon are rejected; NaN passes.
		protected override List<string> GetErrorMessages()
		{
			List<string> errors = base.GetErrorMessages();
			if (StrideZ <= float.Epsilon)
				errors = AddErrorMessage(errors, string.Format("'{0}' has invalid strideZ {1}", name, StrideZ));
			return errors;
		}

		// 060000cf (1.10.1 RVA 0x1A5D980)
		protected override void Reset()
		{
			base.Reset();
			m_meshTypes.Add(new ProceduralMesh_TypeGrid());
		}

		// 060000d0 (1.10.1 RVA 0x1A5DA80). Unless strides scale with the ribbon, the
		// ribbon's local scale is divided out of both strides.
		public override void GenerateLocations(ProceduralMesh_StorageForSubSurface storage, ISurface surface, Transform ribbonTransform)
		{
			IRibbon ribbon = (IRibbon)surface;
			List<ProceduralMesh_TypeGrid> meshTypes = MeshTypes;
			float strideZ = StrideZ;
			float strideX = StrideX;
			bool scaleStride = ScaleStride;
			float biasMin = BiasMin;
			float biasMax = BiasMax;
			LocationGenerator.BiasType biasType = BiasType;
			float gapsRatio = GapsRatio;
			Vector3 scale = ribbonTransform.localScale;
			float xScale = scaleStride ? 1f : 1f / scale.x;
			float zScale = scaleStride ? 1f : 1f / scale.z;
			storage.GenerateGridLocations(ribbonTransform, meshTypes, gapsRatio, this,
				(startKnot, endKnot) => LocationGenerator.GridPositions(ribbon, strideZ * zScale, strideX * xScale, biasMin, biasMax, biasType, startKnot, endKnot));
		}
	}
}
