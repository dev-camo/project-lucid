// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// HardlightProject.ApplicationStateLoadGameSave+<OpenSave>d__3 0x020000e3
// 0x060006a6 .ctor 0x622ed0, 0x6496c0
// 0x060006a7 System.IDisposable.Dispose 0x622efc, 0x6496e0
// 0x060006a8 MoveNext 0x622f00, 0x6496f0
// 0x060006a9 System.Collections.Generic.IEnumerator<System.Object>.get_Current 0x623100, 0x649920
// 0x060006aa System.Collections.IEnumerator.Reset 0x623108, 0x649930
// 0x060006ab System.Collections.IEnumerator.get_Current 0x623148, 0x649970
// HardlightProject.ApplicationStateLoadGameSave 0x020000e2
// 0x060006a2 .ctor 0x622b14, 0x649350
// 0x060006a3 ConstructInstance 0x622b54, 0x649380
// 0x060006a4 DoOnEnter 0x622bf8, 0x649400
// 0x060006a5 OpenSave 0x622e44, 0x649640
// Original Game.Runtime whole 10-method family including natural <OpenSave>d__3.
// Native enter calls base, writes false, obtains genuine SaveManager, then starts the coroutine.
// Iterator closes an open save, yields OpenSave(-1), then writes true; it never validates success.
// No leave-time cancellation, manufactured readiness or save service execution is added.
using System.Collections;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationStateLoadGameSave : FSMState
    {
        public ApplicationStateLoadGameSave(FiniteStateMachine fsm, string stateName) : base(fsm, stateName) { }
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
            => new ApplicationStateLoadGameSave(fsm, transitionId);
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            user.Storage.SetValue(AppFSMKeys.GameSaveLoaded, false);
            CoroutineUtils.RunCoroutine(OpenSave(ProcessManager.GetSystem<SaveManager>(), user));
        }
        private static IEnumerator OpenSave(SaveManager saveManager, IGraphUser user)
        {
            if (saveManager.IsAnySaveOpen) saveManager.CloseSave();
            yield return saveManager.OpenSave(-1);
            user.Storage.SetValue(AppFSMKeys.GameSaveLoaded, true);
        }
    }
}
