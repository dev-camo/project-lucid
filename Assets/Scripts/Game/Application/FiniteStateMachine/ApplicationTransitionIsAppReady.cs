// Original Game.Runtime 0x0200017a / methods 0x06000860..0x06000862.
// Preserve user-storage lookup before registry lookup; a present invalid/unfinished
// scene still resolves the genuine App reference. The final AND is eager.
// App.IsReady is the original auto-property (backing field 0x0400033a), not a port flag.
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplicationTransitionIsAppReady : FSMTransition
    {
        // Game.Runtime.dll 0x06000860; ARM64 0x63e558.
        private ApplicationTransitionIsAppReady(FiniteStateMachine fsm, FSMIdentifier transitionId)
            : base(fsm, transitionId) { }

        // 0x06000861; ARM64 0x63e560. JSON is not used.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId,
            string jsonCtorArgs) => new ApplicationTransitionIsAppReady(fsm, transitionId);

        // 0x06000862; ARM64 0x63e5e0. Missing table/entry returns before App lookup.
        // Once present, App lookup runs even if the handle is invalid/not done.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            if (!user.Storage.TryGetValue(AppFSMKeys.LoadedLevels,
                    out Dictionary<string, AsyncOperationHandle> loadedLevels)
                || !loadedLevels.TryGetValue("s_common", out AsyncOperationHandle common))
                return false;

            bool sceneReady = common.IsValid() && common.IsDone;
            SystemRef<App> appRef = ProcessManager.GetSystemRef<App>(null, true);
            bool appReady = appRef.IsValid() && appRef.Get().IsReady;
            return sceneReady & appReady;
        }
    }
}
