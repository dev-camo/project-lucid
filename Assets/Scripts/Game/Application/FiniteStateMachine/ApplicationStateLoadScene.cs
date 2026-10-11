// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x060006c4 0x623630, 0x649e10
// 0x060006c5 0x624b10, 0x64b340
// 0x060006c6 0x624bdc, 0x64b3f0
// 0x060006c7 0x624fb4, 0x64b7a0
// 0x060006c8 0x6250e4, 0x64b8c0
// Preserved original Game.Runtime.dll ApplicationStateLoadScene (020000ec) and
// protected JSONCtorArgs (020000ed); five original methods060006c4..060006c8.
// Dual native evidence retains exact ordinary pointers and closed generic usages.
// References are acquired before FSMState construction; no App readiness gate is
// added. LoadedLevels carries the real Addressables handle. A new request returns
// without progress publication; subsequent updates publish the actual percentage.
// Native scene-name read ordering is preserved after the request and before handle
// conversion. This is source preservation, with runtime/platform behavior pending.
using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplicationStateLoadScene : FSMState
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class JSONCtorArgs
        {
            [GraphUnityScene]
            public string SceneName;
            public bool UseAppStorage;
            public JSONCtorArgs() { }
        }

        protected readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private readonly SystemRef<MessageExchange<string>> m_messageExchange = ProcessManager.GetSystemRef<MessageExchange<string>>(null, true);
        private readonly string m_sceneName;
        private readonly bool m_useAppStorage;
        private const string m_loadingEventName = "LoadingProgress";

        protected ApplicationStateLoadScene(FiniteStateMachine fsm, string stateName, string sceneName, bool useAppStorage)
            : base(fsm, stateName)
        {
            m_sceneName = sceneName;
            m_useAppStorage = useAppStorage;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new ApplicationStateLoadScene(fsm, (string)stateId, args.SceneName, args.UseAppStorage);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            if (!TryGetStorage(user, out IGraphStorage storage))
                return;
            if (!storage.TryGetValue(AppFSMKeys.LoadedLevels, out Dictionary<string, AsyncOperationHandle> loadedLevels))
                loadedLevels = new Dictionary<string, AsyncOperationHandle>();
            if (!loadedLevels.TryGetValue(m_sceneName, out AsyncOperationHandle handle))
            {
                var sceneHandle = Addressables.LoadSceneAsync(m_sceneName, LoadSceneMode.Additive, true, 100);
                loadedLevels[m_sceneName] = sceneHandle;
                storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
                return;
            }
            MessageExchange<string> exchange = m_messageExchange.IsNull()
                ? ProcessManager.GetSystemAutoCreate<MessageExchange<string>>(null)
                : m_messageExchange.Get();
            exchange.PublishMessage(m_loadingEventName, handle.PercentComplete);
        }

        private bool TryGetStorage(IGraphUser user, out IGraphStorage storage)
        {
            if (m_useAppStorage && m_appRef.IsNull())
            {
                storage = null;
                return false;
            }
            storage = m_useAppStorage ? m_appRef.Get().Storage : user.Storage;
            return true;
        }
    }
}
