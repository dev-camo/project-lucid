using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 02000140; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/Levels/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStatePostLevelLoadReset : FSMState
	{
		// The system references are field initializers, so they are acquired in this
		// order before base registration.
		private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
		private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();
		private readonly SystemRef<ProceduralMesh_PlacementManager> m_procMeshPlacementManagerRef = ProcessManager.GetSystemRef<ProceduralMesh_PlacementManager>();
		private readonly SystemRef<ProcMeshPlacementUpdater> m_procMeshUpdaterRef = ProcessManager.GetSystemRef<ProcMeshPlacementUpdater>();

		// 060007b5 (1.10.1 RVA 0x659EA0)
		public ApplicationStatePostLevelLoadReset(FiniteStateMachine fsm, FSMIdentifier stateId) : base(fsm, stateId)
		{
		}

		// 060007b6 (1.10.1 RVA 0x659FE0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			return new ApplicationStatePostLevelLoadReset(fsm, stateId);
		}

		// 060007b7 (1.10.1 RVA 0x65A040). The native body does not call FSMState.DoOnEnter;
		// ReplaceCameraManager is inlined, and the restart callback is invoked without a
		// null test.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			LevelManager levelManager = m_levelManagerRef.Get();
			LevelManagerLevel level = levelManager.GetCurrentLevelUnsafe();
			ReplaceMeshPools(levelManager, level);
			ReplaceCameraManager(levelManager, level);
			levelManager.OnLevelRestart();
		}

		// 060007b8 (1.10.1 RVA 0x65A130). The placements move to a new placement manager,
		// whose updater is shut down, unregistered and replaced; the level's prefab pool is
		// swapped before the placements are set up again.
		private void ReplaceMeshPools(LevelManager levelManager, LevelManagerLevel level)
		{
			ProceduralMesh_PlacementManager previousManager = m_procMeshPlacementManagerRef.Get();
			List<ProceduralMesh_Placement> placements = previousManager.Placements;
			levelManager.ManagedSystems.RemoveManagedSystem(previousManager);
			ProceduralMesh_PlacementManager placementManager = new ProceduralMesh_PlacementManager();
			levelManager.ManagedSystems.AddManagedSystem(placementManager);
			placementManager.RegisterInstance(placements);
			m_procMeshUpdaterRef.Get().ProcessSystemAction(SystemAction.Shutdown);
			ProcessManager.UnregisterSystem<ProcMeshPlacementUpdater>();
			ProcessManager.RegisterSystem(new ProcMeshPlacementUpdater(placementManager));
			LevelSetupDefinition setup = m_dataManagerRef.Get().LevelSetupDefinitions[level.LevelDefinition.LevelSetupType];
			levelManager.ManagedSystems.ReplacePrefab(setup.PrefabPoolManager);
			placementManager.InitialiseSetup();
		}

		// 060007b9 (1.10.1 RVA 0x65A330)
		private void ReplaceCameraManager(LevelManager levelManager, LevelManagerLevel level)
		{
			LevelSetupDefinition setup = m_dataManagerRef.Get().LevelSetupDefinitions[level.LevelDefinition.LevelSetupType];
			levelManager.ManagedSystems.ReplacePrefab(setup.CinemachineCameraManager);
		}
	}
}
