using Hardlight;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public class ApplicationStateBootShutdown : FSMState
    {
        // Game.Runtime.dll:0x06000645; arm64 0x61d4dc.
        private ApplicationStateBootShutdown(FiniteStateMachine fsm, string stateName) : base(fsm, stateName) { }
        // 0x06000646; arm64 0x61d51c. Original identifier -> name -> identifier path;
        // the JSON argument is ignored, including invalid/empty strings.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
            => new ApplicationStateBootShutdown(fsm, transitionId);
        // 0x06000647; arm64 0x61d5c0. A shutdown exception prevents unregister;
        // no null repair, source action dispatch, or replacement store is inserted.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ProcessManager.GetSystem<HLPropertyStore>().Shutdown();
            ProcessManager.UnregisterSystem<HLPropertyStore>();
        }
    }
}
