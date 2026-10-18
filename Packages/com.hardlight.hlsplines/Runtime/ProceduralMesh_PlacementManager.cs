using System.Collections.Generic;
using Unity.Profiling;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200002b; bodies translated from the 1.10.1 x86-64 binary.
	public class ProceduralMesh_PlacementManager : ISystem
	{
		// Retail only constructs this marker; its [Conditional] Begin/End samples are compiled out.
		private readonly ProfilerMarker m_profilerMarker = new ProfilerMarker("ProceduralMeshPlacementManager");

		// 060000a5 (1.10.1 RVA 0x1A5A8F0)
		public List<ProceduralMesh_Placement> Placements { get; } = new List<ProceduralMesh_Placement>();

		// 060000a6 (1.10.1 RVA 0x1A5A900)
		public ProceduralMesh_PlacementManager()
		{
			this.SubscribeToAction(SystemAction.Update, OnUpdate);
		}

		// 060000a7 (1.10.1 RVA 0x1A59620)
		public void RegisterInstance(ProceduralMesh_Placement placement)
		{
			Placements.Add(placement);
		}

		// 060000a8 (1.10.1 RVA 0x1A5AA20)
		public void RegisterInstance(List<ProceduralMesh_Placement> placements)
		{
			Placements.AddRange(placements);
		}

		// 060000a9 (1.10.1 RVA 0x1A5AA70)
		public void InitialiseSetup()
		{
			foreach (ProceduralMesh_Placement placement in Placements)
				placement.InitialiseSetup();
		}

		// 060000aa (1.10.1 RVA 0x1A5A1D0)
		public void UnregisterInstance(ProceduralMesh_Placement placement)
		{
			Placements.Remove(placement);
		}

		// 060000ab (1.10.1 RVA 0x1A5ABB0)
		private void OnUpdate(object context = null)
		{
			foreach (ProceduralMesh_Placement placement in Placements)
				placement.DoUpdate();
		}
	}
}
