using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("Finished")]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMStateFinished : FSMState
    {
        // HLUnityCore.Runtime.dll:0x06000436; arm64 0x1ac8260.
        public FSMStateFinished(FiniteStateMachine fsm, FSMIdentifier stateId) : base(fsm, stateId) { }
        // Original token 0x06000437; arm64 0x1ac8264. JSON is unused.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs) => new FSMStateFinished(fsm, stateId);
        // Original token 0x06000438; arm64 0x1ac82e0. IsEndState remains
        // the base false result; completion and end-state status differ.
        public override bool HasFinished(IGraphUser user) => true;
    }
}
