using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020007ed; bodies translated from the 1.10.1 x86-64 binary.
    // Drives the HLSplines placement manager from the game's System time category.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ProcMeshPlacementUpdater : ITimeScaled, ISystem
    {
        public bool IsPaused { get; set; }

        private readonly ProceduralMesh_PlacementManager m_proceduralMeshPlacementManager;
        private const TimeCategory TimeCategory = HardlightProject.TimeCategory.System;
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();

        // 06002dde (0x5F6E60): the time manager reference is taken before the base constructor;
        // the updater subscribes itself and then registers its own shutdown action.
        public ProcMeshPlacementUpdater(ProceduralMesh_PlacementManager proceduralMeshPlacementManager)
        {
            m_proceduralMeshPlacementManager = proceduralMeshPlacementManager;
            m_timeManagerRef.Get().Subscribe(this, TimeCategory, UpdateOn.Update, "");
            this.SubscribeToAction(SystemAction.Shutdown, OnShutdown);
        }

        public void OnUpdate(float deltaTime)
        {
            m_proceduralMeshPlacementManager.ProcessSystemAction(SystemAction.Update);
        }

        public void OnFixedUpdate(float deltaTime)
        {
        }

        public void OnLateUpdate(float deltaTime)
        {
        }

        private void OnShutdown(object _)
        {
            if (m_timeManagerRef.TryGet(out TimeManager timeManager))
                timeManager.Unsubscribe(this, TimeCategory, UpdateOn.Update);
        }
    }
}
