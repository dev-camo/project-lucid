#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class FSMCompletionVerification
    {
        private sealed class ProbeState : FSMState
        {
            public bool Finished;
            public int FinishCalls, EndCalls;
            public IGraphUser SeenUser;
            public ProbeState(FiniteStateMachine machine, string name) : base(machine, name) { }
            public override bool HasFinished(IGraphUser user) { ++FinishCalls; SeenUser = user; return Finished; }
            public override bool IsEndState() { ++EndCalls; return true; }
            public override string ToString() => "unrelated state name";
        }
        private static int checks;
        private static string Name() => "Lucid-completion-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        public static void Run()
        {
            checks = 0;
            VerifyCallbacks(); VerifyFactories(); VerifyMetadata();
            Debug.Log("Original FSM completion primitives source-based verification passed: " + checks + " checks. Authored startup remains unresolved.");
        }
        private static void VerifyCallbacks()
        {
            var machine = Machine(); var user = new FSMUser();
            var end = new FSMStateEnd(machine, Name()); var finished = new FSMStateFinished(machine, Name());
            Check(end.HasFinished(null) && end.HasFinished(user) && end.IsEndState(), "end invariant completion/end flag");
            Check(finished.HasFinished(null) && finished.HasFinished(user) && !finished.IsEndState(), "finished invariant completion/inherited nonend flag");
            Check(ReferenceEquals(machine.States[end.StateId], end) && ReferenceEquals(machine.States[finished.StateId], finished), "base state registration");
            var always = new FSMTransitionAlways(machine, Name());
            Check(always.Update(null, default) && always.Update(user, new FSMUpdateContext(-1f, FSMUpdateType.LateUpdate)), "always invariant true regardless of user/context");
            Check(ReferenceEquals(machine.Transitions[always.TransitionId], always), "base transition registration");
            var state = new ProbeState(machine, Name()); var hasFinished = new FSMTransitionHasFinished(machine, Name(), state);
            Check(!hasFinished.Update(user, default) && state.FinishCalls == 1 && state.EndCalls == 0 && ReferenceEquals(state.SeenUser, user), "retained state false without end query");
            state.Finished = true;
            Check(hasFinished.Update(null, default) && state.FinishCalls == 2 && state.EndCalls == 0 && state.SeenUser == null, "retained state true/user forwarded unchanged");
            var absent = new FSMTransitionHasFinished(machine, Name());
            Check(!absent.Update(null, default) && !absent.Update(user, default), "null retained state has no active-state fallback");
            Throws<NullReferenceException>(() => absent.SerialiseRuntimeToJSON(), "null retained state has no serializer repair");
            Check(typeof(FSMTransitionHasFinished).GetProperty("State", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hasFinished) == state, "private state getter retains exact reference");
            var other = Machine(); var foreign = new ProbeState(other, Name()) { Finished = true };
            Check(new FSMTransitionHasFinished(machine, Name(), foreign).Update(user, default), "direct foreign state has no ownership validation");
        }
        [Serializable] private class Args { public string State; }
        private static string Json(string name) => JsonUtility.ToJson(new Args { State = name });
        private static void VerifyFactories()
        {
            var machine = Machine(); var end = FSMStateEnd.ConstructInstance(machine, Name(), "{not-json");
            var finished = FSMStateFinished.ConstructInstance(machine, Name(), null);
            var always = FSMTransitionAlways.ConstructInstance(machine, Name(), "{not-json");
            Check(end is FSMStateEnd && finished is FSMStateFinished && always is FSMTransitionAlways, "primitive factories do not inspect JSON");
            Check(machine.States.Count == 2 && machine.Transitions.Count == 1, "primitive factories register exact supplied owners");
            string stateName = Name(); var state = new ProbeState(machine, stateName) { Finished = true };
            int before = machine.Transitions.Count; string missing = Name();
            Check(FSMTransitionHasFinished.ConstructInstance(machine, missing, Json(Name())) == null && machine.Transitions.Count == before, "missing nonblank state returns before registration");
            var node = (FSMTransitionHasFinished)FSMTransitionHasFinished.ConstructInstance(machine, Name(), Json(stateName));
            Check(node.Update(new FSMUser(), default) && node.SerialiseRuntimeToJSON() == Json(stateName), "resolved state completion and StateId name serialization");
            foreach (string name in new[] { null, "", " \t " })
            {
                node = (FSMTransitionHasFinished)FSMTransitionHasFinished.ConstructInstance(machine, Name(), Json(name));
                Check(node != null && !node.Update(null, default), "blank authored state retains null");
            }
            node = (FSMTransitionHasFinished)FSMTransitionHasFinished.ConstructInstance(machine, Name(), "{}");
            Check(node != null && !node.Update(null, default), "omitted state follows actual Unity blank value");
            string nullName = Name(); ((IDictionary<int, IFSMState>)machine.States)[GraphNameLookup.ConvertNameToId(nullName)] = null;
            node = (FSMTransitionHasFinished)FSMTransitionHasFinished.ConstructInstance(machine, Name(), Json(nullName));
            Check(node != null && !node.Update(null, default), "present null row resolves successfully");
            before = machine.Transitions.Count;
            Throws<NullReferenceException>(() => FSMTransitionHasFinished.ConstructInstance(machine, Name(), ""), "empty constructor JSON has no DTO fallback");
            Check(machine.Transitions.Count == before, "empty JSON failure before registration");
            Throws<ArgumentException>(() => FSMTransitionHasFinished.ConstructInstance(machine, Name(), "{not-json"), "malformed JSON propagates");
            Check(machine.Transitions.Count == before, "invalid JSON failure before registration");
            var dto = typeof(FSMTransitionHasFinished).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            Check(dto.GetCustomAttribute<GraphNodeDefaultNameAttribute>().DefaultName == "HasFinished" && dto.GetField("State").GetCustomAttribute<GraphNodeFocusAttribute>().TargetNodeType == "Hardlight.FSMStateGraphNode", "exact HasFinished authoring field metadata");
            Check(typeof(FSMStateEnd).GetCustomAttribute<GraphNodeDefaultNameAttribute>().DefaultName == "Finished" && typeof(FSMStateFinished).GetCustomAttribute<GraphNodeDefaultNameAttribute>().DefaultName == "Finished", "original end/finished shared default authoring name");
        }
        private static void VerifyMetadata()
        {
            foreach (Type type in new[] { typeof(FSMStateEnd), typeof(FSMStateFinished), typeof(FSMTransitionAlways), typeof(FSMTransitionHasFinished) })
            {
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Option[] expected = type == typeof(FSMTransitionHasFinished)
                    ? new[] { Option.ArrayBoundsChecks, Option.NullChecks } : new[] { Option.NullChecks, Option.ArrayBoundsChecks };
                Check(options.Select(x => x.Option).SequenceEqual(expected) && options.All(x => Equals(x.Value, false)), "original completion compiler options/order");
                Check(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format == "Core/{0}", "original completion menu format");
            }
            Type dto = typeof(FSMTransitionHasFinished).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            var dtoOptions = dto.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(dto.IsSerializable && dtoOptions.Select(x => x.Option).SequenceEqual(new[] { Option.ArrayBoundsChecks, Option.NullChecks })
                && dtoOptions.All(x => Equals(x.Value, false)), "original constructor DTO serialization/compiler options");
            var focus = dto.GetField("State").GetCustomAttribute<GraphNodeFocusAttribute>();
            var popup = dto.GetField("State").GetCustomAttribute<GraphNodePopupAttribute>();
            Check(focus.TargetNodeType == "Hardlight.FSMStateGraphNode" && !focus.IncludeChildren
                && popup.TargetNodeType == focus.TargetNodeType && !popup.IncludeChildren && !popup.IncludeInvisible, "original constructor state selector flags");
            Check(typeof(FSMTransitionAlways).GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName == "Always", "original invariant transition authoring name");
        }
        private static void Check(bool condition, string reason) { ++checks; if (!condition) throw new InvalidOperationException("FSM completion verification failed: " + reason); }
        private static void Throws<T>(Action action, string reason) where T : Exception { try { action(); } catch (T) { Check(true, reason); return; } throw new InvalidOperationException("FSM completion verification failed: " + reason); }
    }
}
#endif
