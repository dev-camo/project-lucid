using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public class ApplicationStateUnloadBoot : FSMState
    {
        // Game.Runtime.dll0x0600076c; ARM64 0x62efd4.
        private ApplicationStateUnloadBoot(FiniteStateMachine fsm, string stateName) : base(fsm, stateName) { }
        // 0x0600076d; ARM64 0x62f014. JSON is not inspected, including malformed/null input.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId,
            string jsonCtorArgs) => new ApplicationStateUnloadBoot(fsm, stateId);
        // 0x0600076e; ARM64 0x62f0b8. Base enter precedes the single string overload;
        // operation result is ignored; no user storage, poll or replacement scheduling.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            SceneManager.UnloadSceneAsync("s_boot");
        }
    }
}
