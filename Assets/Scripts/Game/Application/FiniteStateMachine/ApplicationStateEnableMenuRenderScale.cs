// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// HardlightProject.ApplicationStateEnableMenuRenderScale 0x02000156
// 0x06000801 .ctor 0x639df4, 0x65fee0
// 0x06000802 ConstructInstance 0x639eb4, 0x65ff80
// 0x06000803 DoOnEnter 0x639f40, 0x65fff0
// Original Game.Runtime whole three-method family.
// SystemRef initializer precedes the base constructor. Enter has no base-hook call.
// A single captured visual-quality receiver is reused across both calls.
// Camera acquisition is skipped when device quality can change; otherwise TryGet precedes two false toggles.
// Captured local spelling and CSharp/native fault equivalence remain inferred.
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationStateEnableMenuRenderScale : FSMState
    {
        private readonly SystemRef<VisualQualityManager_SDT> m_visualQualityManagerRef = ProcessManager.GetSystemRef<VisualQualityManager_SDT>();
        public ApplicationStateEnableMenuRenderScale(FiniteStateMachine fsm, string stateName) : base(fsm, stateName) { }
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
            => new ApplicationStateEnableMenuRenderScale(fsm, transitionId);
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            VisualQualityManager_SDT visualQualityManager = m_visualQualityManagerRef.Get();
            visualQualityManager.EnableMenuRenderScale();
            if (!visualQualityManager.DeviceCanChangeGraphicsQuality()
                && ProcessManager.GetSystemRef<CinemachineCameraManager>().TryGet(out CinemachineCameraManager cameraManager))
            {
                cameraManager.MainCamera.enabled = false;
                cameraManager.ToggleGameplayCameraActive(false);
            }
        }
    }
}
