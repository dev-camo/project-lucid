// Original Game.Runtime.dll:HardlightProject.ApplicationTransitionIsSceneUnloaded.
// Complete methods: 0x06000879, 0x0600087a, 0x0600087b, 0x0600087c, 0x0600087d.
// arm64 ranges: 0x0600087d 0x63fb48..0x63fb50, 0x06000879 0x63f708..0x63f7f8, 0x0600087a 0x63f7f8..0x63f8b8, 0x0600087b 0x63f8b8..0x63fa18, 0x0600087c 0x63fa18..0x63fb48.
// x86_64 ranges: 0x0600087d 0x665610..0x665620, 0x06000879 0x665230..0x665300, 0x0600087a 0x665300..0x6653b0, 0x0600087b 0x6653b0..0x6654f0, 0x0600087c 0x6654f0..0x665610.
// Original Addressable unloading checks missing dictionary/key, not handle validity or completion.
// The SceneManager branch checks invalid scene OR not loaded, after genuine storage selection.
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
    public class ApplicationTransitionIsSceneUnloaded : FSMTransition
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
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

        private ApplicationTransitionIsSceneUnloaded(FiniteStateMachine fsm, FSMIdentifier transitionId,
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
            return new ApplicationTransitionIsSceneUnloaded(fsm, transitionId, args.SceneName, args.UseAppStorage, args.IsAddressable);
        }

        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            if (!TryGetStorage(user, out IGraphStorage storage)) return false;
            if (m_isAddressable)
            {
                return !storage.TryGetValue(AppFSMKeys.LoadedLevels, out Dictionary<string, AsyncOperationHandle> loadedLevels)
                    || !loadedLevels.ContainsKey(m_sceneName);
            }
            Scene scene = SceneManager.GetSceneByName(m_sceneName);
            return !scene.IsValid() || !scene.isLoaded;
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
