using Hardlight;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplicationStateBootCleanup : FSMState
    {
        // Game.Runtime.dll:0x0600063f; arm64 0x512d20.
        private ApplicationStateBootCleanup(FiniteStateMachine fsm, string stateName) : base(fsm, stateName) { }
        // 0x06000640; arm64 0x512d60. Preserve original ID/name conversions;
        // JSON is not parsed or used.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
            => new ApplicationStateBootCleanup(fsm, transitionId);
        // 0x06000641; arm64 0x512e04. Store lookup precedes graph storage read.
        // Retained bootstrap properties default first-boot=true when key is absent;
        // every successful path then removes that snapshot from current user.Storage.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            HLPropertyStore store = ProcessManager.GetSystem<HLPropertyStore>();
            if (!user.Storage.TryGetValue(AppFSMKeys.BootHLPropertyStoreList, out HLPropertyList properties)
                || properties.AsBool("HL_First_Boot", true))
            {
                store.AddProperty("HL_First_Boot", false);
                HLPropertyStore.SaveImmediate(false);
            }
            user.Storage.RemoveValue<HLPropertyList>(AppFSMKeys.BootHLPropertyStoreList);
        }
    }
}
