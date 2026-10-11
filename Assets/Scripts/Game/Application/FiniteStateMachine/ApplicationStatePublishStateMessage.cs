// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// HardlightProject.ApplicationStatePublishStateMessage+JSONCtorArgs 0x02000114
// 0x06000737 .ctor 0x62c6b8, 0x652c00
// HardlightProject.ApplicationStatePublishStateMessage 0x02000113
// 0x06000734 .ctor 0x62c3b8, 0x652940
// 0x06000735 ConstructInstance 0x62c414, 0x652990
// 0x06000736 DoOnEnter 0x62c504, 0x652a50
// Original Game.Runtime whole four-method family including protected JSONCtorArgs.
// Base enter precedes storage lookup using StateEvents, null fallback and true default insertion.
// AddUnique preserves list/null/provider behavior; no replacement event queue is constructed.
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplicationStatePublishStateMessage : FSMState
    {
        [System.Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        protected class JSONCtorArgs
        {
            [GraphUnityObject(typeof(ApplicationStateEvent))] public string EventId;
        }
        private readonly ApplicationStateEvent m_eventId;
        public ApplicationStatePublishStateMessage(FiniteStateMachine fsm, string stateName, ApplicationStateEvent eventId)
            : base(fsm, stateName) { m_eventId = eventId; }
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            ApplicationStateEvent eventId = ApplicationStateEvent.FindByName(args.EventId);
            return new ApplicationStatePublishStateMessage(fsm, transitionId, eventId);
        }
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_eventId);
        }
    }
}
