using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{

public static class FSMTransitionLogicVerification
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        ++checks;
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { Check(true, message); return; }
        throw new InvalidOperationException(message);
    }
    private static FiniteStateMachine Owner() => new FiniteStateMachine(
        "transition-proof." + Guid.NewGuid().ToString("N"), skipAddToManager: true);
    private class Child : IFSMTransition
    {
        public int FSMId => 0;
        public int Id = 101;
        public Func<int> IdGetter;
        public int TransitionId => IdGetter?.Invoke() ?? Id;
        public override string ToString() => "Not A Transition Name";
        public Action Enter, Leave;
        public Func<IGraphUser, FSMUpdateContext, bool> UpdateAction;
        public void OnEnter(IGraphUser u, FSMStateChangeAction a) => Enter?.Invoke();
        public void OnLeave(IGraphUser u, FSMStateChangeAction a) => Leave?.Invoke();
        public void OnFire(IGraphUser u, FSMUpdateContext c, FSMStateChangeAction a) { }
        public bool Update(IGraphUser u, FSMUpdateContext c) => UpdateAction?.Invoke(u, c) ?? false;
        public string SerialiseRuntimeToJSON() => "";
        public List<FiniteStateMachine> GetDependencies() => null;
    }
    private class Children : FSMTransitionWithChildren
    {
        public Children(FiniteStateMachine f, IFSMTransition a = null, IFSMTransition b = null)
            : base(f, 201, a, b) { }
        public List<IFSMTransition> List { get => ChildTransitions; set => ChildTransitions = value; }
    }
    private class Logic : FSMTransitionLogicOp
    {
        public Logic(LogicOperator op) : base(Owner(), 202, op) { }
        public List<IFSMTransition> List { get => ChildTransitions; set => ChildTransitions = value; }
    }

    [Serializable]
    private class NamesArgs { public List<string> ChildTransitionNames; }
    [Serializable]
    private class LogicArgs
    {
        public string LogicOp;
        public List<string> ChildTransitionNames;
    }

    private static string NamesJson(params string[] names) => JsonUtility.ToJson(
        new NamesArgs { ChildTransitionNames = new List<string>(names) });
    private static string LogicJson(string operation, params string[] names) => JsonUtility.ToJson(
        new LogicArgs { LogicOp = operation, ChildTransitionNames = new List<string>(names) });
    private static int NodeId(string name) => GraphNameLookup.ConvertNameToId(name);
    private static List<IFSMTransition> ReadChildren(FSMTransitionWithChildren node) =>
        (List<IFSMTransition>)typeof(FSMTransitionWithChildren)
            .GetProperty("ChildTransitions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(node);

    private static void VerifyJsonConstructionAndSerialization()
    {
        // These fixtures exercise pinned Unity JsonUtility itself; each FSM opts
        // out of manager registration and never uses authored or original content.
        var owner = Owner();
        string prefix = "child-transition-json." + Guid.NewGuid().ToString("N");
        string nameA = prefix + ".a", nameB = prefix + ".b", nameNull = prefix + ".null";
        var a = new Child { Id = NodeId(nameA) };
        var b = new Child { Id = NodeId(nameB) };
        owner.AddTransition(a); owner.AddTransition(b);
        var rows = (Dictionary<int, IFSMTransition>)owner.Transitions;
        rows[NodeId(nameNull)] = null;
        int before = rows.Count;
        string missing = prefix + ".missing";
        IFSMTransition failed = FSMTransitionWithChildren.ConstructInstance(owner, NodeId(prefix + ".missing-parent"), NamesJson(nameA, missing));
        Check(failed == null && rows.Count == before, "children factory missing key before registration");
        failed = FSMTransitionLogicOp.ConstructInstance(owner, NodeId(prefix + ".missing-logic"), LogicJson("bad", nameA, missing));
        Check(failed == null && rows.Count == before, "missing child precedes malformed operator");
        Throws<ArgumentException>(() => FSMTransitionLogicOp.ConstructInstance(owner,
            NodeId(prefix + ".bad-logic"), LogicJson("bad")), "resolved malformed operator");
        Check(rows.Count == before, "malformed operator before registration");
        Throws<OverflowException>(() => FSMTransitionLogicOp.ConstructInstance(owner,
            NodeId(prefix + ".overflow-logic"), LogicJson("2147483648")), "factory Int32 enum overflow");
        Check(rows.Count == before, "overflow before registration");
        Throws<ArgumentException>(() => FSMTransitionWithChildren.ConstructInstance(owner,
            NodeId(prefix + ".bad-json"), "{not-json"), "children invalid JSON propagates");
        Check(rows.Count == before, "invalid JSON before registration");
        var children = (FSMTransitionWithChildren)FSMTransitionWithChildren.ConstructInstance(owner,
            NodeId(prefix + ".children"), NamesJson(nameA, nameNull, nameB, nameA));
        List<IFSMTransition> list = ReadChildren(children);
        Check(list.Count == 3 && list[0] == a && list[1] == b && list[2] == a,
            "children factory preserves duplicates and drops present null");
        Check(rows[children.TransitionId] == children, "children factory registers completed node");
        Check(children.SerialiseRuntimeToJSON() == NamesJson(nameA, nameB, nameA), "children JSON order/TransitionId names");
        var logic = (FSMTransitionLogicOp)FSMTransitionLogicOp.ConstructInstance(owner,
            NodeId(prefix + ".logic"), LogicJson(" lOgIcXoR ", nameA, nameNull, nameB, nameA));
        list = ReadChildren(logic);
        Check(logic.LogicOp == FSMTransitionLogicOp.LogicOperator.LogicXOR && list.Count == 3 && list[2] == a,
            "logic factory case/whitespace and duplicate/null children");
        Check(logic.SerialiseRuntimeToJSON() == LogicJson("LogicXOR", nameA, nameB, nameA), "logic JSON operator-before-names order");
        logic = (FSMTransitionLogicOp)FSMTransitionLogicOp.ConstructInstance(owner,
            NodeId(prefix + ".numeric"), LogicJson("4", nameA));
        Check((int)logic.LogicOp == 4 && logic.SerialiseRuntimeToJSON() == LogicJson("4", nameA), "undefined numeric operator JSON");
        logic = (FSMTransitionLogicOp)FSMTransitionLogicOp.ConstructInstance(owner,
            NodeId(prefix + ".combined"), LogicJson("LogicOR, LogicXOR"));
        Check(logic.LogicOp == FSMTransitionLogicOp.LogicOperator.LogicNOT && logic.Update(null, default), "comma names OR into NOT");
        children = (FSMTransitionWithChildren)FSMTransitionWithChildren.ConstructInstance(owner,
            NodeId(prefix + ".empty"), NamesJson());
        Check(ReadChildren(children).Count == 0 && children.SerialiseRuntimeToJSON() == NamesJson(), "empty child factory/serializer");

        // Observe omitted-list behavior through JsonUtility, then verify the
        // native factory's direct traversal contract with that actual result.
        NamesArgs omitted = JsonUtility.FromJson<NamesArgs>("{}");
        before = rows.Count;
        if (omitted.ChildTransitionNames == null)
        {
            Throws<NullReferenceException>(() => FSMTransitionWithChildren.ConstructInstance(owner,
                NodeId(prefix + ".omitted"), "{}"), "actual omitted list has no factory repair");
            Check(rows.Count == before, "omitted-null list before registration");
        }
        else
        {
            children = (FSMTransitionWithChildren)FSMTransitionWithChildren.ConstructInstance(owner,
                NodeId(prefix + ".omitted"), "{}");
            Check(ReadChildren(children).Count == omitted.ChildTransitionNames.Count, "actual omitted list forwarded");
        }

        var enterOnly = new Children(Owner());
        int childUpdates = 0;
        enterOnly.List.Add(new Child { UpdateAction = (u,c) => { ++childUpdates; return true; } });
        Check(!enterOnly.Update(null, default) && childUpdates == 0, "children base update does not forward");
        enterOnly.List = new List<IFSMTransition> { null };
        Throws<NullReferenceException>(() => enterOnly.SerialiseRuntimeToJSON(), "raw null serializer child");

        var enumList = new Children(Owner());
        bool mutate = true;
        var idMutation = new Child { IdGetter = () => {
            if (mutate) { mutate = false; enumList.List.Add(b); }
            return a.Id;
        } };
        enumList.List.Add(idMutation);
        Throws<InvalidOperationException>(() => enumList.SerialiseRuntimeToJSON(), "children serializer enumerator invalidation");
        enumList.List = new List<IFSMTransition> { idMutation, a }; mutate = true;
        idMutation.IdGetter = () => {
            if (mutate) { mutate = false; enumList.List = new List<IFSMTransition> { b, b }; }
            return a.Id;
        };
        Check(enumList.SerialiseRuntimeToJSON() == NamesJson(nameA, nameA), "children serializer enumerates old replaced list");

        var indexed = new Logic(FSMTransitionLogicOp.LogicOperator.LogicAND);
        int appendedIdCalls = 0;
        mutate = true;
        idMutation = new Child { IdGetter = () => {
            if (mutate) { mutate = false; indexed.List.Add(new Child { IdGetter = () => { ++appendedIdCalls; return b.Id; } }); }
            return a.Id;
        } };
        indexed.List.Add(idMutation);
        Check(indexed.SerialiseRuntimeToJSON() == LogicJson("LogicAND", nameA) && appendedIdCalls == 0,
            "logic serializer captures initial count");
        indexed.List = new List<IFSMTransition> { idMutation, a }; mutate = true;
        idMutation.IdGetter = () => {
            if (mutate) { mutate = false; indexed.List = new List<IFSMTransition> { a, b }; }
            return a.Id;
        };
        Check(indexed.SerialiseRuntimeToJSON() == LogicJson("LogicAND", nameA, nameB), "logic serializer rereads replaced list");
        indexed.List = new List<IFSMTransition> { idMutation, a }; mutate = true;
        idMutation.IdGetter = () => {
            if (mutate) { mutate = false; indexed.List.RemoveAt(0); }
            return a.Id;
        };
        Throws<ArgumentOutOfRangeException>(() => indexed.SerialiseRuntimeToJSON(), "logic serializer shrinking index list");
    }
    public static int Run()
    {
        checks = 0;
        VerifyAttributesAndEnumParser();
        var owner = Owner();
        var child = new Child();
        var children = new Children(owner, child, child);
        Check(owner.Transitions[201] == children && children.List.Count == 2, "registration and duplicates");
        children.AddChildTransition(null);
        Check(children.List.Count == 2, "null append ignored");
        children.List = null;
        children.AddChildTransition(null);
        Throws<NullReferenceException>(() => children.AddChildTransition(child), "nonnull append null list");
        children.List = new List<IFSMTransition>();
        var trace = new List<string>();
        var first = new Child { Enter = () => trace.Add("first+"), Leave = () => trace.Add("first-") };
        var second = new Child { Enter = () => trace.Add("second+"), Leave = () => trace.Add("second-") };
        children.List.Add(first); children.List.Add(second);
        children.OnEnter(null, null); children.OnLeave(null, null);
        Check(string.Join(",", trace) == "first+,second+,first-,second-", "forwarding order");
        first.Enter = () => children.List.Add(child);
        Throws<InvalidOperationException>(() => children.OnEnter(null, null), "foreach add invalidates");
        children.List = new List<IFSMTransition> { first, second }; trace.Clear();
        first.Enter = () => { trace.Add("first+"); children.List = new List<IFSMTransition>(); };
        children.OnEnter(null, null);
        Check(string.Join(",", trace) == "first+,second+", "foreach retains original list on replacement");
        children.List = new List<IFSMTransition> { null };
        Throws<NullReferenceException>(() => children.OnLeave(null, null), "raw null child");

        for (int count = 0; count <= 3; ++count)
        for (int successes = 0; successes <= count; ++successes)
        for (int op = -1; op <= 4; ++op)
        {
            int updates = 0;
            var logic = new Logic((FSMTransitionLogicOp.LogicOperator)op);
            for (int i = 0; i < count; ++i)
            {
                bool result = i < successes;
                logic.List.Add(new Child { UpdateAction = (u, c) => { ++updates; return result; } });
            }
            bool expected = op == 0 ? successes == count : op == 1 ? successes > 0 :
                op == 2 ? successes == 1 : op == 3 ? successes == 0 : false;
            Check(logic.Update(null, default) == expected && updates == count, "logic truth table/full traversal");
        }
        var append = new Logic(FSMTransitionLogicOp.LogicOperator.LogicAND);
        int extraCalls = 0;
        append.List.Add(new Child { UpdateAction = (u,c) => {
            append.List.Add(new Child { UpdateAction = (u2,c2) => { ++extraCalls; return true; } }); return true;
        } });
        Check(!append.Update(null, default) && extraCalls == 0, "initial loop count and final AND count");
        var remove = new Logic(FSMTransitionLogicOp.LogicOperator.LogicOR);
        remove.List.Add(new Child { UpdateAction = (u,c) => { remove.List.RemoveAt(0); return true; } });
        remove.List.Add(new Child());
        Throws<ArgumentOutOfRangeException>(() => remove.Update(null, default), "index loop shrink");
        var replace = new Logic(FSMTransitionLogicOp.LogicOperator.LogicAND);
        int oldCalls = 0, newCalls = 0;
        replace.List.Add(new Child { UpdateAction = (u,c) => {
            replace.List = new List<IFSMTransition> { new Child(), new Child { UpdateAction = (u2,c2) => { ++newCalls; return true; } } };
            return true;
        } });
        replace.List.Add(new Child { UpdateAction = (u,c) => { ++oldCalls; return true; } });
        Check(replace.Update(null, default) && oldCalls == 0 && newCalls == 1, "index loop rereads replacement");
        var throws = new Logic((FSMTransitionLogicOp.LogicOperator)99);
        throws.List.Add(new Child { UpdateAction = (u,c) => throw new ApplicationException() });
        Throws<ApplicationException>(() => throws.Update(null, default), "invalid operator still evaluates children");
        VerifyJsonConstructionAndSerialization();
        UnityEngine.Debug.Log("Recovered child transition, logic, and graph attribute checks passed: " + checks);
        return checks;
    }

    private static void VerifyAttributesAndEnumParser()
    {
        // Original class-level metadata retains locally compiled option
        // attributes, including exact order and the boxed false values.
        Type[] optionTypes = { typeof(FSMStateGroup), typeof(FSMStateSubFSM),
            typeof(FSMTransitionLogicOp), typeof(FSMTransitionWithChildren) };
        for (int i = 0; i < optionTypes.Length; ++i)
        {
            object[] options = optionTypes[i].GetCustomAttributes(typeof(Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute), false);
            Check(options.Length == 2, "original class compiler option count: " + optionTypes[i].Name);
            var first = (Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute)options[0];
            var second = (Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute)options[1];
            var expectedFirst = i == 3 ? Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks : Unity.IL2CPP.CompilerServices.Option.NullChecks;
            var expectedSecond = i == 3 ? Unity.IL2CPP.CompilerServices.Option.NullChecks : Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks;
            Check(first.Option == expectedFirst && second.Option == expectedSecond
                && Equals(first.Value, false) && Equals(second.Value, false), "original class compiler options: " + optionTypes[i].Name);
        }
        Check(new GraphNodeFocusAttribute(null, null).TargetNodeType == ".", "Focus null-string concat");
        Check(new GraphNodeFocusAttribute(null, "C", " \t").TargetNodeType == ".C", "Focus blank assembly omitted");
        var focus = new GraphNodeFocusAttribute("N", "C", " A ", true);
        Check(focus.TargetNodeType == "N.C,  A " && focus.IncludeChildren, "Focus retained spacing and flag");
        Check(new GraphNodeFocusAttribute(typeof(FSMTransitionLogicVerification)).TargetNodeType == typeof(FSMTransitionLogicVerification).FullName, "Focus FullName");
        Throws<NullReferenceException>(() => new GraphNodeFocusAttribute((Type)null), "Focus null type");
        Type generic = typeof(List<>).GetGenericArguments()[0];
        Check(new GraphNodeFocusAttribute(generic).TargetNodeType == null, "Focus null FullName retained");
        var popup = new GraphNodePopupAttribute("N", "C", null, true, true);
        Check(popup.TargetNodeType == "N.C" && popup.IncludeChildren && popup.IncludeInvisible, "Popup name and flags");
        Check(new GraphNodePopupAttribute(typeof(FSMTransitionLogicVerification), true, true).TargetNodeType == typeof(FSMTransitionLogicVerification).FullName, "Popup FullName");
        Throws<NullReferenceException>(() => new GraphNodePopupAttribute((Type)null), "Popup null type");
        Check(new GraphEnumPopupAttribute(typeof(string)).EnumType == typeof(string), "non-enum Type retained");
        Check(new GraphEnumPopupAttribute(null).EnumType == null, "null EnumType retained");
        Check(new GraphNodeDefaultNameAttribute(null).DefaultName == null, "null default name retained");
        Check(new GraphDisplayNameAttribute("").DisplayName == "", "empty display name retained");
        var color = new GraphNodeColourAttribute(-1f, float.NaN, 2f);
        Check(color.Colour.r == -1 && float.IsNaN(color.Colour.g) && color.Colour.b == 2 && color.Colour.a == 1, "raw color/alpha");
        var copy = color.Colour; copy.r = 10;
        Check(color.Colour.r == -1, "color getter copy");
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(GraphNodePopupAttribute), typeof(AttributeUsageAttribute));
        Check(usage.ValidOn == AttributeTargets.Field && usage.AllowMultiple && usage.Inherited, "inherited field attribute usage");
        usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(GraphDisplayNameAttribute), typeof(AttributeUsageAttribute));
        Check(usage.ValidOn == (AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field) && !usage.AllowMultiple && usage.Inherited, "display usage");
        string[] valid = { " logicor ", "4", "-1", "2147483647", "LogicOR, LogicXOR", "LogicAND,LogicOR" };
        int[] expected = { 1, 4, -1, 2147483647, 3, 1 };
        for (int i = 0; i < valid.Length; ++i)
            Check((int)(FSMTransitionLogicOp.LogicOperator)Enum.Parse(typeof(FSMTransitionLogicOp.LogicOperator), valid[i], true) == expected[i], "Enum.Parse accepted value");
        Throws<ArgumentNullException>(() => Enum.Parse(typeof(FSMTransitionLogicOp.LogicOperator), null, true), "enum null value");
        foreach (string text in new[] { "", " ", "bogus", "1foo", ",", "LogicOR," })
            Throws<ArgumentException>(() => Enum.Parse(typeof(FSMTransitionLogicOp.LogicOperator), text, true), "enum bad value");
        foreach (string text in new[] { "2147483648", "-2147483649" })
            Throws<OverflowException>(() => Enum.Parse(typeof(FSMTransitionLogicOp.LogicOperator), text, true), "enum overflow");
        Check(new GraphHorizontalBreakAttribute().AfterProperty && !new GraphHorizontalBreakAttribute(false).AfterProperty, "horizontal flag/default");
        Check(new FiniteStateMachinePopupAttribute().ManualEntryFieldName == "ManualEntry" && new FiniteStateMachinePopupAttribute(null).ManualEntryFieldName == null, "FSM popup string/default");
        var childPopup = new FiniteStateMachineChildStatePopupAttribute(null, true);
        Check(childPopup.FieldName == null && childPopup.AddDefaultEntry && !new FiniteStateMachineChildStatePopupAttribute("A").AddDefaultEntry, "child popup args/default");
        Check(new FiniteStateMachineOpenAttribute() is GraphAttributeBase, "open base");
        Type groupDto = typeof(FSMStateGroup).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
        Check(groupDto.GetCustomAttribute<GraphNodeDefaultNameAttribute>().DefaultName == "StateGroup" && groupDto.GetCustomAttribute<GraphNodeColourAttribute>().Colour.g == 0.75f, "group DTO name/color");
        Check(groupDto.GetField("States").GetCustomAttribute<GraphNodePopupAttribute>().TargetNodeType == "Hardlight.FSMStateGraphNode", "group DTO popup");
        Type subDto = typeof(FSMStateSubFSM).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
        Check(subDto.GetCustomAttribute<GraphNodeColourAttribute>().Colour.g == 1f && !subDto.GetField("ManualNameEntry").GetCustomAttribute<GraphHorizontalBreakAttribute>().AfterProperty, "subFSM DTO color/break");
        Check(subDto.GetField("SubFSMName").GetCustomAttribute<FiniteStateMachinePopupAttribute>().ManualEntryFieldName == "ManualNameEntry" && subDto.GetField("SubFSMName").GetCustomAttribute<FiniteStateMachineOpenAttribute>() != null, "subFSM DTO name attributes");
        childPopup = subDto.GetField("DefaultState").GetCustomAttribute<FiniteStateMachineChildStatePopupAttribute>();
        Check(childPopup.FieldName == "SubFSMName" && childPopup.AddDefaultEntry, "subFSM DTO default popup");
    }
}
}
