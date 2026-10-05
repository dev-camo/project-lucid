using System.Collections.Generic;

namespace Hardlight
{
    // Original HLUnityCore.Runtime.dll base type. Native base hooks are empty
    // and its default update is false; authored subclasses remain separate work.
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMTransition : IFSMTransition
    {
        public readonly FiniteStateMachine FSM;
        // 0x060004c3/0x060004c4; arm64 0x1ad1748/0x1ad1754.
        public int FSMId => FSM.FSMId;
        public int TransitionId { get; }

        // 0x060004c5; arm64 0x1aca97c: automatically register with the owner.
        public FSMTransition(FiniteStateMachine fsm, FSMIdentifier transitionId)
        {
            FSM = fsm;
            TransitionId = transitionId.Id;
            fsm.AddTransition(this);
        }
        // 0x060004c6; arm64 0x1ad175c; decoded original literal is empty.
        public virtual string SerialiseRuntimeToJSON() => string.Empty;
        // 0x060004c7/0x060004c8/0x060004c9/0x060004ca;
        // arm64 0x1ad17a0/0x1ad17b0/0x1ad17c0/0x1ad17d0: virtual dispatch.
        public void OnEnter(IGraphUser user, FSMStateChangeAction action) => DoOnEnter(user, action);
        public void OnLeave(IGraphUser user, FSMStateChangeAction action) => DoOnLeave(user, action);
        public void OnFire(IGraphUser user, FSMUpdateContext context, FSMStateChangeAction action) => DoOnFire(user, context, action);
        public bool Update(IGraphUser user, FSMUpdateContext context) => DoUpdate(user, context);
        // 0x060004cb; arm64 0x1ad17e0: native null dependency list.
        public virtual List<FiniteStateMachine> GetDependencies() => null;
        // 0x060004cc/0x060004cd/0x060004ce;
        // arm64 0x1ad17e8/0x1acb310/0x1acb46c: RET.
        protected virtual void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { }
        protected virtual void DoOnLeave(IGraphUser user, FSMStateChangeAction action) { }
        protected virtual void DoOnFire(IGraphUser user, FSMUpdateContext context, FSMStateChangeAction action) { }
        // 0x060004cf; arm64 0x1ad17ec: native false.
        protected virtual bool DoUpdate(IGraphUser user, FSMUpdateContext context) => false;
        // 0x060004d0/0x060004d1; arm64 0x1acf3a8/0x1ad17f4.
        public GraphStorageKey GetStorageKey(string name) => new GraphStorageKey(name, TransitionId, FSMId);
        public GraphStorageKey GetStorageKey(int nameId) => new GraphStorageKey(nameId, TransitionId, FSMId);
        // 0x060004d2/0x060004d3; arm64 0x1ac417c/0x1ac27e0.
        public static int ConvertNameToId(string name) => GraphNameLookup.ConvertNameToId(name);
        public static string LookupNameUsingId(int id) => GraphNameLookup.LookupNameUsingId(id);
        // 0x060004d4; arm64 0x1ad182c.
        public override string ToString() => GraphNameLookup.LookupNameUsingId(TransitionId);
    }
}
