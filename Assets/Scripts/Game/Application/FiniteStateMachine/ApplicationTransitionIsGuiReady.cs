// Original Game.Runtime 0x0200017f and JSON owner 0x02000180, methods 0x0600086b..0x0600086f.
// Both SystemRefs are acquired before base FSM registration. Authored UseAppStorage
// selects the genuine App storage; absent/default-null scene dictionaries retain
// the original faults. Read registered UIRuntimeConfiguration only after valid/done s_gui.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationTransitionIsGuiReady : FSMTransition
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class JSONCtorArgs
        {
            public bool UseAppStorage;
        }

        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();
        private readonly SystemRef<UIRuntimeConfiguration> m_uiRuntimeConfigRef = ProcessManager.GetSystemRef<UIRuntimeConfiguration>();
        private readonly bool m_useAppStorage;

        private ApplicationTransitionIsGuiReady(FiniteStateMachine fsm,
            FSMIdentifier transitionId, bool useAppStorage) : base(fsm, transitionId)
        {
            m_useAppStorage = useAppStorage;
        }

        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new ApplicationTransitionIsGuiReady(fsm, transitionId, args.UseAppStorage);
        }

        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            if (!TryGetStorage(user, out IGraphStorage storage)) return false;
            Dictionary<string, AsyncOperationHandle> loadedLevels = storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels);
            return loadedLevels.TryGetValue("s_gui", out AsyncOperationHandle handle)
                && handle.IsValid() && handle.IsDone && m_uiRuntimeConfigRef.IsValid();
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
