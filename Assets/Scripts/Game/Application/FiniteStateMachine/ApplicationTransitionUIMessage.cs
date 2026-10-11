// Preserved Sonic Dream Team 1.10.1, Game.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// HardlightProject.ApplicationTransitionUIMessage+JSONCtorArgs 0x02000197
// 0x060008a2 .ctor 0x641598, 0x666f20
// HardlightProject.ApplicationTransitionUIMessage 0x02000196
// 0x0600089f .ctor 0x6413f8, 0x666db0
// 0x060008a0 ConstructInstance 0x64142c, 0x666de0
// 0x060008a1 DoUpdate 0x6414f8, 0x666e90
// Original Game.Runtime whole four-method family including protected JSONCtorArgs.
// Base update return is discarded; the actual App event list Remove result is returned.
// Remove consumes one matching event and retains its original equality/null behavior.
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationTransitionUIMessage : FSMTransition
    {
        [System.Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        protected class JSONCtorArgs
        {
            [GraphUnityObject(typeof(UIModernEvent))] public string EventId;
        }
        private readonly UIModernEvent m_eventId;
        public ApplicationTransitionUIMessage(FiniteStateMachine fsm, FSMIdentifier transitionId, UIModernEvent eventId)
            : base(fsm, transitionId) { m_eventId = eventId; }
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            UIModernEvent eventId = UIModernEvent.FindByName(args.EventId);
            return new ApplicationTransitionUIMessage(fsm, transitionId, eventId);
        }
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            return user.GetAs<App>().GetUIModernEvents().Remove(m_eventId);
        }
    }
}
