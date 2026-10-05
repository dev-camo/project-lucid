using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [GraphNodeMenuFormat("Core/{0}")]
    [GraphNodeDefaultName("Always")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMTransitionAlways : FSMTransition
    {
        // HLUnityCore.Runtime.dll:0x0600045d; arm64 0x1aca8d4.
        public FSMTransitionAlways(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId) { }
        // Original token 0x0600045e; arm64 0x1acaa24. JSON is unused.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs) => new FSMTransitionAlways(fsm, transitionId);
        // Original token 0x0600045f; arm64 0x1acab04: invariant true.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context) => true;
    }
}
