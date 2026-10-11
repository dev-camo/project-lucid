// Original Game.Runtime.dll:HardlightProject.ApplicationTransitionIsSceneLoaded.
// Complete methods: 0x06000874, 0x06000875, 0x06000876, 0x06000877, 0x06000878.
// arm64 ranges: 0x06000878 0x63f700..0x63f708, 0x06000874 0x63f2a4..0x63f394, 0x06000875 0x63f394..0x63f454, 0x06000876 0x63f454..0x63f5d0, 0x06000877 0x63f5d0..0x63f700.
// x86_64 ranges: 0x06000878 0x665220..0x665230, 0x06000874 0x664e00..0x664ed0, 0x06000875 0x664ed0..0x664f80, 0x06000876 0x664f80..0x665100, 0x06000877 0x665100..0x665220.
// Original TryGetStorage runs even for the SceneManager branch.
// Addressable readiness requires a present dictionary/key and valid, completed handle; faults are retained.
// Only historical IsNull property spelling is corrected to the original method invocation.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationTransitionIsSceneLoaded : FSMTransition
    {
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        protected class JSONCtorArgs
        {
            [GraphUnityScene] public string SceneName;
            public bool UseAppStorage;
            public bool IsAddressable;
        }

        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();
        private readonly string m_sceneName;
        private readonly bool m_useAppStorage;
        private readonly bool m_isAddressable;

        private ApplicationTransitionIsSceneLoaded(FiniteStateMachine fsm, FSMIdentifier transitionId,
            string sceneName, bool useAppStorage, bool isAddressable) : base(fsm, transitionId)
        {
            m_sceneName = sceneName;
            m_useAppStorage = useAppStorage;
            m_isAddressable = isAddressable;
        }

        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new ApplicationTransitionIsSceneLoaded(fsm, transitionId, args.SceneName, args.UseAppStorage, args.IsAddressable);
        }

        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            if (!TryGetStorage(user, out IGraphStorage storage)) return false;
            if (m_isAddressable)
            {
                return storage.TryGetValue(AppFSMKeys.LoadedLevels, out Dictionary<string, AsyncOperationHandle> loadedLevels)
                    && loadedLevels.TryGetValue(m_sceneName, out AsyncOperationHandle handle)
                    && handle.IsValid() && handle.IsDone;
            }
            Scene scene = SceneManager.GetSceneByName(m_sceneName);
            return scene.IsValid() && scene.isLoaded;
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
