// Original Game.Runtime 0x020000fa..0x020000fc, methods 0x060006e8..0x060006ef.
// FindByName uses the original Unity Object.name key. Preserve transition queue,
// close-before-open callback, live storage reads and post-success typed removal order.
// Json argument defaults and natural callback ownership remain original.
using System;
using System.Collections.Generic;
using UnityEngine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public class ApplicationStateOpenUIContainer : FSMState
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class JSONCtorArgs
        {
            [GraphUnityObject(typeof(UIContainerIdentifier))] public string Container;
            public bool PlayScreenTransition;
            [GraphUnityObject(typeof(UIContainerIdentifier))] public string TransitionOverrideContainer;
            public bool QueueScreenTransitionIfAlreadyActive = true;
            [GraphUnityObject(typeof(UIContainerIdentifier))] public List<string> CloseContainers;
        }

        private readonly UIContainerIdentifier m_containerIdentifier;
        private readonly bool m_playScreenTransition;
        private readonly UIContainerIdentifier m_transitionOverrideContainer;
        private readonly bool m_queueScreenTransitionIfAlreadyActive;
        private readonly IReadOnlyList<UIContainerIdentifier> m_closeContainers;

        private ApplicationStateOpenUIContainer(FiniteStateMachine fsm, string stateName,
            UIContainerIdentifier containerIdentifier, bool playScreenTransition,
            UIContainerIdentifier transitionOverrideContainer, bool queueScreenTransitionIfAlreadyActive,
            IReadOnlyList<UIContainerIdentifier> closeContainers) : base(fsm, stateName)
        {
            m_containerIdentifier = containerIdentifier;
            m_playScreenTransition = playScreenTransition;
            m_transitionOverrideContainer = transitionOverrideContainer;
            m_queueScreenTransitionIfAlreadyActive = queueScreenTransitionIfAlreadyActive;
            m_closeContainers = closeContainers;
        }

        public static IFSMState ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            UIContainerIdentifier containerIdentifier = UIContainerIdentifier.FindByName(args.Container);
            UIContainerIdentifier transitionOverrideContainer = null;
            if (!string.IsNullOrEmpty(args.TransitionOverrideContainer))
                transitionOverrideContainer = UIContainerIdentifier.FindByName(args.TransitionOverrideContainer);
            List<UIContainerIdentifier> closeContainers = new List<UIContainerIdentifier>();
            foreach (string name in args.CloseContainers)
            {
                if (!string.IsNullOrEmpty(name)) closeContainers.Add(UIContainerIdentifier.FindByName(name));
            }
            return new ApplicationStateOpenUIContainer(fsm, stateId, containerIdentifier,
                args.PlayScreenTransition, transitionOverrideContainer,
                args.QueueScreenTransitionIfAlreadyActive, closeContainers);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            if (m_playScreenTransition)
            {
                UIScreenTransitionManager transitionManager = ProcessManager.GetSystem<UIScreenTransitionManager>();
                if (m_queueScreenTransitionIfAlreadyActive || !transitionManager.IsTransitionActive())
                {
                    transitionManager.QueueTransition(m_transitionOverrideContainer, () =>
                    {
                        CloseContainers();
                        OpenContainer(user);
                    }, null, null);
                    return;
                }
                CloseContainers();
            }
            OpenContainer(user);
        }

        private void CloseContainers()
        {
            if (m_closeContainers == null || m_closeContainers.Count == 0) return;
            UIManager manager = ProcessManager.GetSystem<UIManager>();
            foreach (UIContainerIdentifier container in m_closeContainers) manager.Close(container);
        }

        private void OpenContainer(IGraphUser user)
        {
            UIManager manager = ProcessManager.GetSystem<UIManager>();
            IUIContainerParameters parameters = user.Storage.GetValue<IUIContainerParameters>(AppFSMKeys.UIContainerParameters);
            UIContainerIdentifier container = m_containerIdentifier;
            if (user.Storage.TryGetValue(AppFSMKeys.UIContainerIdentifierOverride, out UIContainerIdentifier identifierOverride))
                container = identifierOverride;
            if (parameters != null) manager.GetOrCreate(container, parameters);
            else manager.GetOrCreate(container);
            user.Storage.RemoveValue<IUIContainerParameters>(AppFSMKeys.UIContainerParameters);
            user.Storage.RemoveValue<UIContainerIdentifier>(AppFSMKeys.UIContainerIdentifierOverride);
        }
    }
}
