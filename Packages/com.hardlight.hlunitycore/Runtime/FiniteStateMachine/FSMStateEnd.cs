using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [GraphNodeDefaultName("Finished")]
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMStateEnd : FSMState
    {
        // HLUnityCore.Runtime.dll:0x06000432; arm64 0x1ac81d0.
        public FSMStateEnd(FiniteStateMachine fsm, FSMIdentifier stateId) : base(fsm, stateId) { }
        // Original token 0x06000433; arm64 0x1ac81d4. Arguments are
        // passed unchanged to the base registration; JSON is unused.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs) => new FSMStateEnd(fsm, stateId);
        // Original tokens 0x06000434/435; arm64 0x1ac8250/8258.
        public override bool HasFinished(IGraphUser user) => true;
        public override bool IsEndState() => true;
    }
}
