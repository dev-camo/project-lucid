using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200002f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class ProceduralMesh_PlacementOnRibbon : ProceduralMesh_PlacementOnKnotSurface
	{
		[SerializeField]
		private List<Ribbon> m_ribbons = new List<Ribbon>(1);

		private List<ProceduralMesh_StorageForRibbon> m_ribbonInfo;

		// 060000bd (1.10.1 RVA 0x1A5C200)
		protected virtual void Reset()
		{
			Ribbon ribbon = gameObject.GetComponent<Ribbon>();
			if (ribbon != null)
				m_ribbons.Add(ribbon);
		}

		// 060000be (1.10.1 RVA 0x1A5C2F0). A NaN MaxRatio also fails the ratio check.
		protected override List<string> GetErrorMessages()
		{
			List<string> errors = null;
			bool hasRibbons = m_ribbons.Count > 0;
			for (int i = 0; hasRibbons && i < m_ribbons.Count; ++i)
				hasRibbons = m_ribbons[i] != null;
			if (!hasRibbons)
				errors = AddErrorMessage(errors, "'" + name + "' has no ribbon assigned");
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

		// 060000bf (1.10.1 RVA 0x1A5C690)
		internal override void InitialiseSetup()
		{
			base.InitialiseSetup();
			m_ribbonInfo = new List<ProceduralMesh_StorageForRibbon>(m_ribbons.Count);
			if (!HasValidSetup())
				return;

			ProceduralMesh_PlacementSetup.PerformanceSetup setup = Setup.GetSetup();
			bool precomputeLocations = SystemConfig.PrecomputeMeshLocations;
			bool populatePerKnot = setup.PopulatePerKnot;
			for (int i = 0; i < m_ribbons.Count; ++i)
			{
				var ribbonInfo = new ProceduralMesh_StorageForRibbon(m_ribbons[i], StartKnot, EndKnot, populatePerKnot, setup.NumMeshesMultiplier, setup.UseRuntimeCallback);
				m_ribbonInfo.Add(ribbonInfo);
				if (precomputeLocations)
					ribbonInfo.GenerateLocations(this);
			}
			TotalBounds = CalculateTotalWorldSpaceBounds();
		}

		// 060000c0 (1.10.1 RVA 0x1A5CAD0)
		protected override void DespawnAllMeshes(bool onDestroy = false, bool resetStorage = false, bool canFade = false)
		{
			if (m_populatedCount == 0 || m_ribbonInfo == null)
				return;

			m_populatedCount = 0;
			for (int i = 0; i < m_ribbonInfo.Count; ++i)
				m_populatedCount += m_ribbonInfo[i].DespawnSurfaceMeshes(onDestroy, resetStorage, canFade);
		}

		// 060000c1 (1.10.1 RVA 0x1A5CBA0). The quote placement in the log text is original.
		protected override void CheckForSpawnAndDespawn(Camera camera, bool resetStorage)
		{
			if (m_ribbonInfo == null)
			{
				ValidSetup = false;
				HLOutput.LogError("'" + GetPath(transform) + " has invalid procedural mesh setup'");
				return;
			}

			m_populatedCount = 0;
			for (int i = 0; i < m_ribbonInfo.Count; ++i)
				m_populatedCount += m_ribbonInfo[i].CheckForSpawnAndDespawn(camera, SpawnDistanceSquare, DespawnDistanceSquare, FadeInDistanceSquare, FadeOutDistanceSquare, resetStorage, this);
		}

		// 060000c2 (1.10.1 RVA 0x1A5CDE0). No ribbons gives an empty bounds at the origin.
		protected override Bounds CalculateTotalWorldSpaceBounds()
		{
			Bounds bounds = default;
			if (m_ribbonInfo.Count > 0)
			{
				bounds = m_ribbonInfo[0].CalculateTotalWorldSpaceBounds();
				for (int i = 1; i < m_ribbonInfo.Count; ++i)
					bounds.Encapsulate(m_ribbonInfo[i].CalculateTotalWorldSpaceBounds());
			}
			return bounds;
		}

		// 060000c3 (1.10.1 RVA 0x1A5D1D0). Starts from the first centre knot, then
		// takes only the left and right edge knots of every knot in the range.
		public override Bounds CalculateBounds(ISurface surface, int startKnot, int endKnot)
		{
			var ribbon = (IRibbon)surface;
			var bounds = new Bounds(ribbon.GetKnot(startKnot, RibbonAlignment.Center).Transform.Location, Vector3.zero);
			if (endKnot == -1)
				endKnot = ribbon.KnotCount - 1;
			for (int knot = startKnot; knot <= endKnot; ++knot)
			{
				bounds.Encapsulate(ribbon.GetKnot(knot, RibbonAlignment.Left).Transform.Location);
				bounds.Encapsulate(ribbon.GetKnot(knot, RibbonAlignment.Right).Transform.Location);
			}
			return bounds;
		}
	}
}
