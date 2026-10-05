using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMStateSubFSM : FSMState
    {
        [Serializable]
        [GraphNodeColour(0f, 1f, 0f)]
        private class JSONCtorArgs
        {
            [GraphHorizontalBreak(false)]
            public bool ManualNameEntry;
            [FiniteStateMachineOpen]
            [FiniteStateMachinePopup("ManualNameEntry")]
            public string SubFSMName;
            public bool ResetStateOnEnter;
            public bool HasFinishedOnEndStateOnly;
            [FiniteStateMachineChildStatePopup("SubFSMName", true)]
            public string DefaultState;
        }
        public readonly FiniteStateMachine SubFSM;
        public readonly bool ResetStateOnEnter;
        public readonly bool HasFinishedOnEndStateOnly;
        public readonly IFSMState DefaultState;
        // HLUnityCore.Runtime.dll:Hardlight.FSMStateSubFSM:0x06000421;
        // arm64 0x1ac7244. Register base state before retaining supplied fields.
        public FSMStateSubFSM(FiniteStateMachine fsm, FSMIdentifier stateId, FiniteStateMachine subFSM, bool resetStateOnEnter = true, bool hasFinishedOnEndStateOnly = false, IFSMState defaultState = null) : base(fsm, stateId)
        {
            SubFSM = subFSM; ResetStateOnEnter = resetStateOnEnter;
            HasFinishedOnEndStateOnly = hasFinishedOnEndStateOnly; DefaultState = defaultState;
        }
        // Original token 0x06000422; arm64 0x1ac72a8. A missing dependency
        // returns null without registering this state so the factory can retry.
        // Missing named defaults are accepted as null; ManualNameEntry is unused.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            if (!FSMManager.GetManager().TryGetFSM(new FSMIdentifier(ConvertNameToId(args.SubFSMName)), out FiniteStateMachine subFSM)) return null;
            IFSMState defaultState = null;
            if (!string.IsNullOrEmpty(args.DefaultState)) subFSM.States.TryGetValue(ConvertNameToId(args.DefaultState), out defaultState);
            return new FSMStateSubFSM(fsm, stateId, subFSM, args.ResetStateOnEnter, args.HasFinishedOnEndStateOnly, defaultState);
        }
        // Original token 0x06000423; arm64 0x1ac75a4. Serialize retained
        // dependency/default names and flags; ManualNameEntry remains false.
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            SubFSMName = LookupNameUsingId(SubFSM.FSMId), ResetStateOnEnter = ResetStateOnEnter,
            HasFinishedOnEndStateOnly = HasFinishedOnEndStateOnly,
            DefaultState = DefaultState == null ? null : LookupNameUsingId(DefaultState.StateId)
        });
        // Original token 0x06000424; arm64 0x1ac7770. New list each call;
        // a directly constructed null dependency is included without validation.
        public override List<FiniteStateMachine> GetDependencies() => new List<FiniteStateMachine> { SubFSM };
        // Original token 0x06000425; arm64 0x1ac7874. Initialization wins
        // when reset is requested or no active state exists; otherwise re-enter
        // the existing child with the caller's action and unchanged storage.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            IFSMState state = SubFSM.GetActiveState(user);
            if (ResetStateOnEnter || state == null) SubFSM.InitialiseUser(user, action, DefaultState);
            else state.OnEnter(user, action);
        }
        // Original token 0x06000426; arm64 0x1ac7968. Nonreset leave retains
        // the child active state; reset leave clears it via the original engine.
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            if (ResetStateOnEnter) SubFSM.ClearUser(user, action);
            else SubFSM.GetActiveState(user)?.OnLeave(user, action);
        }
        // Original token 0x06000427; arm64 0x1ac7a60 tail-calls child engine.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context) => SubFSM.Update(user, context);
        // Original token 0x06000428; arm64 0x1ac7a68. Child completion must
        // be true before the optional end-state check; null active state is false.
        public override bool HasFinished(IGraphUser user)
        {
            IFSMState state = SubFSM.GetActiveState(user);
            return state != null && state.HasFinished(user) && (!HasFinishedOnEndStateOnly || state.IsEndState());
        }
    }
}
