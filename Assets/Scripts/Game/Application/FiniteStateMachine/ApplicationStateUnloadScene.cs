// Original Game.Runtime.dll:HardlightProject.ApplicationStateUnloadScene.
// Complete methods: 0x06000772, 0x06000773, 0x06000774, 0x06000775.
// arm64 ranges: 0x06000775 0x62fd68..0x62fd70, 0x06000772 0x62f8ec..0x62f948, 0x06000773 0x62f948..0x62fa30, 0x06000774 0x62fa30..0x62fd68.
// x86_64 ranges: 0x06000775 0x655e50..0x655e60, 0x06000772 0x6559f0..0x655a40, 0x06000773 0x655a40..0x655b00, 0x06000774 0x655b00..0x655e50.
// GetValue supplies null/default and the original diagnostic flag; no null repair is introduced.
// A missing scene key returns without storage publication. A present key unloads, removes, then republishes.
// Removal rereads m_sceneName after the unload call, and publication rereads user.Storage.
// No handle-validity/completion gate or replacement loader is added.
using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public class ApplicationStateUnloadScene : FSMState
    {
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class JSONCtorArgs
        {
            [GraphUnityScene] public string SceneName;
            public JSONCtorArgs() { }
        }

        private readonly string m_sceneName;

        private ApplicationStateUnloadScene(FiniteStateMachine fsm, string stateName, string sceneName)
            : base(fsm, stateName)
        {
            m_sceneName = sceneName;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new ApplicationStateUnloadScene(fsm, stateId, args.SceneName);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            var loadedLevels = user.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(
                AppFSMKeys.LoadedLevels, null, true);
            if (loadedLevels.TryGetValue(m_sceneName, out AsyncOperationHandle handle))
            {
                AddressableManager.UnloadSceneAndClearResources(handle);
                loadedLevels.Remove(m_sceneName);
                user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
            }
        }
    }
}
