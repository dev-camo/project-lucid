using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Game.Runtime.dll 0x02000187. This is the transition shipped in the
    // supplied player; a possible pre-inlining Editor source form is unknown.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationTransitionIsUnityEditor : FSMTransition
    {
        // Original 0x0600087e; ARM64 0x63fb50, x86_64 0x665620.
        // The base constructor performs the original transition registration.
        private ApplicationTransitionIsUnityEditor(FiniteStateMachine fsm, FSMIdentifier transitionId)
            : base(fsm, transitionId) { }

        // Original 0x0600087f; ARM64 0x63fb58, x86_64 0x665630.
        // The authored factory ignores jsonCtorArgs and preserves the identifier.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new ApplicationTransitionIsUnityEditor(fsm, transitionId);
        }

        // Original 0x06000880; ARM64 0x63fbd8, x86_64 0x665690.
        // Both complete shipping bodies return false without accessing the user,
        // update context or Unity API. This is not an offline replacement.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context) => false;
    }
}
