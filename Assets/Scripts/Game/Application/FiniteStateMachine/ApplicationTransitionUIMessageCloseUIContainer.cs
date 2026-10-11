// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// HardlightProject.ApplicationTransitionUIMessageCloseUIContainer+JSONCtorArgs 0x02000199
// 0x060008a6 .ctor 0x6417f8, 0x667150
// HardlightProject.ApplicationTransitionUIMessageCloseUIContainer 0x02000198
// 0x060008a3 .ctor 0x6415a0, 0x666f30
// 0x060008a4 ConstructInstance 0x6415f0, 0x666f80
// 0x060008a5 DoUpdate 0x6416e0, 0x667050
// Original Game.Runtime whole four-method family with its own protected JSONCtorArgs.
// Container resolution precedes event resolution although constructor argument order is event then container.
// Base update consumes the event before obtaining UIManager and closing; closure faults do not restore the removed event.
// All original provider and Engine behavior remains unexecuted.
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplicationTransitionUIMessageCloseUIContainer : ApplicationTransitionUIMessage
    {
        [System.Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        protected new class JSONCtorArgs
        {
            [GraphUnityObject(typeof(UIModernEvent))] public string EventId;
            [GraphUnityObject(typeof(UIContainerIdentifier))] public string Container;
        }
        private readonly UIContainerIdentifier m_container;
        private ApplicationTransitionUIMessageCloseUIContainer(FiniteStateMachine fsm, FSMIdentifier transitionId,
            UIModernEvent eventId, UIContainerIdentifier container) : base(fsm, transitionId, eventId) { m_container = container; }
        public new static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            UIContainerIdentifier container = UIContainerIdentifier.FindByName(args.Container);
            UIModernEvent eventId = UIModernEvent.FindByName(args.EventId);
            return new ApplicationTransitionUIMessageCloseUIContainer(fsm, transitionId, eventId, container);
        }
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            bool matched = base.DoUpdate(user, context);
            if (matched) ProcessManager.GetSystem<UIManager>().Close(m_container);
            return matched;
        }
    }
}
