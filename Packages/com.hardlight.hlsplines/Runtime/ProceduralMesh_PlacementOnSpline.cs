using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000034; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class ProceduralMesh_PlacementOnSpline : ProceduralMesh_PlacementOnKnotSurface
	{
		[SerializeField]
		private List<Spline> m_splines = new List<Spline>(1);

		private List<ProceduralMesh_StorageForSpline> m_splineInfo;

		// 060000e0 (1.10.1 RVA 0x1A5EE40)
		protected virtual void Reset()
		{
			Spline spline = gameObject.GetComponent<Spline>();
			if (spline != null)
				m_splines.Add(spline);
		}

		// 060000e1 (1.10.1 RVA 0x1A5EF30). A NaN MaxRatio also fails the ratio check.
		protected override List<string> GetErrorMessages()
		{
			List<string> errors = null;
			bool hasSplines = m_splines.Count > 0;
			for (int i = 0; hasSplines && i < m_splines.Count; ++i)
				hasSplines = m_splines[i] != null;
			if (!hasSplines)
				errors = AddErrorMessage(errors, "'" + name + "' has no spline assigned");
			if (!(MaxRatio > 0f))
				errors = AddErrorMessage(errors, "'" + name + "' has no mesh types with a valid ratio. Set at least one to have a value greater than zero");

			IReadOnlyList<ProceduralMesh_Type> meshTypes = GetMeshTypes();
			for (int i = 0; i < meshTypes.Count; ++i)
			{
				if (!meshTypes[i].HasValidSetup())
					errors = AddErrorMessage(errors, string.Format("'{0}' has mesh type with invalid setup (Element {1}). {2}", name, i, meshTypes[i].GetErrorMessage()));
			}
			return errors;
		}

		// 060000e2 (1.10.1 RVA 0x1A5F2D0)
		internal override void InitialiseSetup()
		{
			base.InitialiseSetup();
			m_splineInfo = new List<ProceduralMesh_StorageForSpline>(m_splines.Count);
			if (!HasValidSetup())
				return;

			ProceduralMesh_PlacementSetup.PerformanceSetup setup = Setup.GetSetup();
			bool precomputeLocations = SystemConfig.PrecomputeMeshLocations;
			bool populatePerKnot = setup.PopulatePerKnot;
			for (int i = 0; i < m_splines.Count; ++i)
			{
				var splineInfo = new ProceduralMesh_StorageForSpline(m_splines[i], StartKnot, EndKnot, populatePerKnot, setup.NumMeshesMultiplier, setup.UseRuntimeCallback);
				m_splineInfo.Add(splineInfo);
				if (precomputeLocations)
					splineInfo.GenerateLocations(this);
			}
			TotalBounds = CalculateTotalWorldSpaceBounds();
		}

		// 060000e3 (1.10.1 RVA 0x1A5F6A0)
		protected override void DespawnAllMeshes(bool onDestroy = false, bool resetStorage = false, bool canFade = false)
		{
			if (m_populatedCount == 0 || m_splineInfo == null)
				return;

			m_populatedCount = 0;
			for (int i = 0; i < m_splineInfo.Count; ++i)
				m_populatedCount += m_splineInfo[i].DespawnSurfaceMeshes(onDestroy, resetStorage, canFade);
		}

		// 060000e4 (1.10.1 RVA 0x1A5F770). The log text, with its two trailing spaces, is original.
		protected override void CheckForSpawnAndDespawn(Camera camera, bool resetStorage)
		{
			if (m_splineInfo == null)
			{
				ValidSetup = false;
				HLOutput.LogError("'" + GetPath(transform) + "' has invalid procedural mesh setup -  ");
				return;
			}

			m_populatedCount = 0;
			for (int i = 0; i < m_splineInfo.Count; ++i)
				m_populatedCount += m_splineInfo[i].CheckForSpawnAndDespawn(camera, SpawnDistanceSquare, DespawnDistanceSquare, FadeInDistanceSquare, FadeOutDistanceSquare, resetStorage, this);
		}

		// 060000e5 (1.10.1 RVA 0x1A5F9D0)
		public override Bounds CalculateBounds(ISurface surface, int startKnot, int endKnot)
		{
			var spline = (ISpline)surface;
			var bounds = new Bounds(spline.GetKnot(startKnot).Transform.Location, Vector3.zero);
			if (endKnot == -1)
				endKnot = spline.KnotCount - 1;
			for (int knot = startKnot; knot <= endKnot; ++knot)
				bounds.Encapsulate(spline.GetKnot(knot).Transform.Location);
			return bounds;
		}

		// 060000e6 (1.10.1 RVA 0x1A5FD90). No splines gives an empty bounds at the origin.
		protected override Bounds CalculateTotalWorldSpaceBounds()
		{
			Bounds bounds = default;
			if (m_splineInfo.Count > 0)
			{
				bounds = m_splineInfo[0].CalculateTotalWorldSpaceBounds();
				for (int i = 1; i < m_splineInfo.Count; ++i)
					bounds.Encapsulate(m_splineInfo[i].CalculateTotalWorldSpaceBounds());
			}
			return bounds;
		}
	}
}
