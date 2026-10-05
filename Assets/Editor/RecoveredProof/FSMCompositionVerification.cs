#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using UnityEngine;
namespace ProjectLucid
{
    public static class FSMCompositionVerification
    {
        private sealed class ProbeState : FSMState
        {
            private readonly List<string> trace; private readonly string label;
            public bool Finished, End; public Action EnterCallback, UpdateCallback, LeaveCallback;
            public FSMStateChangeAction SeenAction; public FSMUpdateContext SeenContext;
            public ProbeState(FiniteStateMachine fsm, string id, List<string> trace, string label) : base(fsm, id) { this.trace = trace; this.label = label; }
            protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { trace.Add(label + ":enter"); SeenAction = action; EnterCallback?.Invoke(); }
            protected override void DoUpdate(IGraphUser user, FSMUpdateContext context) { trace.Add(label + ":update"); SeenContext = context; UpdateCallback?.Invoke(); }
            protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action) { trace.Add(label + ":leave"); SeenAction = action; LeaveCallback?.Invoke(); }
            public override bool HasFinished(IGraphUser user) { trace.Add(label + ":finished"); return Finished; }
            public override bool IsEndState() { trace.Add(label + ":end"); return End; }
        }
        private sealed class GroupAccess : FSMStateGroup { public GroupAccess(FiniteStateMachine fsm, string id) : base(fsm,id) { } public void Replace(List<IFSMState> value) => States = value; }
        private static readonly List<FiniteStateMachine> Registered = new List<FiniteStateMachine>();
        private static string Name() => "Lucid-composition-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine(bool registered = false)
        {
            var machine = new FiniteStateMachine(Name(), skipAddToManager: !registered);
            if (registered) Registered.Add(machine); return machine;
        }
        public static void Run()
        {
            try { VerifyGroupCallbacks(); VerifyGroupFactory(); VerifySubMachineCallbacks(); VerifySubMachineFactory(); FSMTransitionLogicVerification.Run(); }
            finally { foreach (var machine in Registered) FSMManager.GetManager().ReleaseFSM(machine); Registered.Clear(); }
            Debug.Log("Original FSM group/submachine source-based verification passed. Concrete game states and full startup remain unresolved.");
        }
        private static void VerifyGroupCallbacks()
        {
            var fsm = Machine(); var trace = new List<string>(); var a = new ProbeState(fsm,Name(),trace,"a"); var b = new ProbeState(fsm,Name(),trace,"b");
            var group = new FSMStateGroup(fsm,Name(),a,a); group.AddState(null); group.AddState(b);
            Check(group.States.SequenceEqual(new IFSMState[]{a,a,b}) && !group.HasFinished(null) && !group.IsEndState() && group.GetDependencies()==null,"group duplicates and inherited completion/dependency behavior");
            var action = FSMStateChangeAction.Create(FSMActionReason.Forced); var user = new FSMUser(); var context = new FSMUpdateContext(0.375f,FSMUpdateType.FixedUpdate);
            group.OnEnter(user,action); group.Update(user,context); group.OnLeave(user,action);
            Trace(trace,"a:enter","a:enter","b:enter","a:update","a:update","b:update","a:leave","a:leave","b:leave");
            Check(ReferenceEquals(a.SeenAction,action) && ReferenceEquals(b.SeenAction,action) && b.SeenContext.DeltaTime==0.375f && b.SeenContext.UpdateType==FSMUpdateType.FixedUpdate,"group passes action/context unchanged"); action.Destroy();
            group.States.Clear(); group.States.Add(a); group.States.Add(b); trace.Clear(); a.UpdateCallback=()=>group.States.Add(b);
            Throws<InvalidOperationException>(()=>group.Update(user,context),"group detects live list additions"); Trace(trace,"a:update"); a.UpdateCallback=null;
            group.States.Clear(); group.States.Add(a); group.States.Add(b); trace.Clear(); a.EnterCallback=()=>throw new InvalidOperationException("fixture");
            Throws<InvalidOperationException>(()=>group.OnEnter(user,null),"child errors propagate before later callbacks"); Trace(trace,"a:enter");a.EnterCallback=null;
            group.States.Clear(); group.States.Add(null); Throws<NullReferenceException>(()=>group.Update(user,context),"directly inserted null child fails");
            var replaced = new GroupAccess(fsm,Name()); replaced.AddState(a);replaced.AddState(b);trace.Clear();a.UpdateCallback=()=>replaced.Replace(new List<IFSMState>());
            replaced.Update(user,context);Trace(trace,"a:update","b:update");Check(replaced.States.Count==0,"foreach retains original list after property replacement");a.UpdateCallback=null;
        }
        private static void VerifyGroupFactory()
        {
            var fsm=Machine();var trace=new List<string>();var a=new ProbeState(fsm,"composition-child-a",trace,"a");
            string missing=Name();Check(FSMStateGroup.ConstructInstance(fsm,missing,"{\"States\":[\"composition-child-a\",\"absent\"]}")==null&&!fsm.StateExists(missing,out _),"unresolved child returns null before group registration");
            var group=(FSMStateGroup)FSMStateGroup.ConstructInstance(fsm,Name(),"{\"States\":[\"composition-child-a\",\"composition-child-a\"]}");
            Check(group.States.Count==2&&ReferenceEquals(group.States[0],a)&&ReferenceEquals(group.States[1],a),"factory preserves duplicate names");
            var serialized=JsonUtility.FromJson<GroupJSON>(group.SerialiseRuntimeToJSON());Check(serialized.States.SequenceEqual(new[]{"composition-child-a","composition-child-a"}),"serialized names/order");
            Check(((FSMStateGroup)FSMStateGroup.ConstructInstance(fsm,Name(),"{\"States\":[]}")).States.Count==0,"empty authored group is valid");
            ((IDictionary<int,IFSMState>)fsm.States)[GraphNameLookup.ConvertNameToId("present-null-child")] = null;
            Check(((FSMStateGroup)FSMStateGroup.ConstructInstance(fsm,Name(),"{\"States\":[\"present-null-child\",\"composition-child-a\"]}")).States.SequenceEqual(new IFSMState[]{a}),"present null child resolves then AddState discards it");
            Throws<NullReferenceException>(()=>FSMStateGroup.ConstructInstance(fsm,Name(),""),"empty constructor JSON is not defaulted");
        }
        [Serializable] private class GroupJSON { public List<string> States; }
        private static void VerifySubMachineCallbacks()
        {
            var parent=Machine();var child=Machine();var trace=new List<string>();var a=new ProbeState(child,Name(),trace,"a");var b=new ProbeState(child,Name(),trace,"b");child.DefaultState=a;
            var user=new FSMUser();var action=FSMStateChangeAction.Create(FSMActionReason.Forced);var context=new FSMUpdateContext(0.5f,FSMUpdateType.LateUpdate);
            var retained=new FSMStateSubFSM(parent,Name(),child,false,false,b);retained.OnEnter(user,action);
            Check(ReferenceEquals(child.GetActiveState(user),b)&&b.SeenAction.Reason==FSMActionReason.InitialiseUser&&ReferenceEquals(b.SeenAction.ParentAction,action),"first nonreset enter uses retained default and parent action");Trace(trace,"b:enter");
            trace.Clear();retained.OnLeave(user,action);retained.OnEnter(user,action);Trace(trace,"b:leave","b:enter");Check(ReferenceEquals(child.GetActiveState(user),b)&&ReferenceEquals(b.SeenAction,action),"nonreset leave/reenter preserves child selection and original action");
            trace.Clear();retained.Update(user,context);Trace(trace,"b:update");Check(b.SeenContext.DeltaTime==0.5f&&b.SeenContext.UpdateType==FSMUpdateType.LateUpdate,"child engine receives context");
            trace.Clear();Check(!retained.HasFinished(user),"unfinished child");Trace(trace,"b:finished");b.Finished=true;trace.Clear();Check(retained.HasFinished(user),"completion without end-only flag");Trace(trace,"b:finished");
            var endOnly=new FSMStateSubFSM(parent,Name(),child,false,true);trace.Clear();Check(!endOnly.HasFinished(user),"finished nonend child blocked");Trace(trace,"b:finished","b:end");b.End=true;trace.Clear();Check(endOnly.HasFinished(user),"finished end child allowed");Trace(trace,"b:finished","b:end");
            var reset=new FSMStateSubFSM(parent,Name(),child,true,false,a);trace.Clear();reset.OnEnter(user,action);Trace(trace,"b:leave","a:enter");Check(ReferenceEquals(child.GetActiveState(user),a),"reset enters requested default");trace.Clear();reset.OnLeave(user,action);Trace(trace,"a:leave");Check(child.GetActiveState(user)==null&&!reset.HasFinished(user),"reset leave clears active child and null completion is false");
            Check(retained.GetDependencies().Count==1&&ReferenceEquals(retained.GetDependencies()[0],child)&&!ReferenceEquals(retained.GetDependencies(),retained.GetDependencies()),"fresh retained dependency list");
            var invalid=new FSMStateSubFSM(parent,Name(),null);Check(invalid.GetDependencies().Count==1&&invalid.GetDependencies()[0]==null,"direct null dependency retained");Throws<NullReferenceException>(()=>invalid.HasFinished(user),"null dependency fails on use");action.Destroy();
        }
        private static void VerifySubMachineFactory()
        {
            var parent=Machine();var child=Machine(true);var trace=new List<string>();var a=new ProbeState(child,"composition-default",trace,"a");
            string id=Name();Check(FSMStateSubFSM.ConstructInstance(parent,id,"{\"SubFSMName\":\"missing-dependency\"}")==null&&!parent.StateExists(id,out _),"missing submachine retry has no state registration");
            string args="{\"SubFSMName\":\""+child+"\",\"ManualNameEntry\":true,\"ResetStateOnEnter\":false,\"HasFinishedOnEndStateOnly\":true,\"DefaultState\":\"composition-default\"}";
            var state=(FSMStateSubFSM)FSMStateSubFSM.ConstructInstance(parent,Name(),args);Check(ReferenceEquals(state.SubFSM,child)&&ReferenceEquals(state.DefaultState,a)&&!state.ResetStateOnEnter&&state.HasFinishedOnEndStateOnly,"factory retained dependency/default/flags");
            var serialized=JsonUtility.FromJson<SubJSON>(state.SerialiseRuntimeToJSON());Check(!serialized.ManualNameEntry&&serialized.SubFSMName==child.ToString()&&serialized.DefaultState=="composition-default"&&!serialized.ResetStateOnEnter&&serialized.HasFinishedOnEndStateOnly,"serializer resets authoring-only manual flag");
            args="{\"SubFSMName\":\""+child+"\",\"DefaultState\":\"missing\"}";Check(((FSMStateSubFSM)FSMStateSubFSM.ConstructInstance(parent,Name(),args)).DefaultState==null,"missing named default accepted as null");
        }
        [Serializable] private class SubJSON { public bool ManualNameEntry;public string SubFSMName;public bool ResetStateOnEnter;public bool HasFinishedOnEndStateOnly;public string DefaultState; }
        private static void Trace(List<string> trace,params string[] expected){Check(trace.SequenceEqual(expected),"callback trace "+string.Join(",",trace));}
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("FSM composition verification failed: "+message);}
        private static void Throws<T>(Action action,string message)where T:Exception{try{action();}catch(T){return;}throw new InvalidOperationException("FSM composition verification failed: "+message);}
    }
}
#endif
