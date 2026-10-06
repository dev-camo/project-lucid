using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class UIStartupVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original UI startup verification: " + label); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        private static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            bool caught = false; try { action(); } catch (T) { caught = true; } Check(caught, label);
        }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static UIManager RawManager()
        {
            UIManager manager = Raw<UIManager>();
            Field(typeof(UIManager), "OnContainerClosed").SetValue(manager, (Action<UIContainerIdentifier>)delegate { });
            Field(typeof(UIManager), "m_containerLastSelectedWidgetIndex").SetValue(manager, new Dictionary<UIContainerIdentifier, int>());
            Field(typeof(UIManager), "m_containerManagers").SetValue(manager, new Dictionary<UIContainerBehaviour, UIContainerManager>(HardlightEnumComparers.UIContainerBehaviourComparer));
            Field(typeof(UIManager), "m_visibilityGroupDefinitions").SetValue(manager, new List<UIVisibilityGroupDefinition>());
            Field(typeof(UIManager), "m_uiVisibilityGroupMembers").SetValue(manager, new Dictionary<string, List<UIVisibilityGroupMember>>());
            Field(typeof(UIManager), "m_workingUIGroupVisibility").SetValue(manager, new Dictionary<string, bool>());
            return manager;
        }
        private static UIVisibilityGroupDefinition Definition(string guid, bool visible)
        {
            var definition = Raw<UIVisibilityGroupDefinition>();
            typeof(ScriptableObjectWithGuid).GetField("m_guid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, guid);
            Field(typeof(UIVisibilityGroupDefinition), "m_initialVisibility").SetValue(definition, visible);
            return definition;
        }
        private static void Declarations()
        {
            foreach (var row in new[] {
                (Type:typeof(UIManager), Names:new[]{"m_guiCamera","m_freeParent","m_linkedParent","m_singleParent","m_stackParent","m_transitionParent","m_debugParent","m_visibilityGroupDefinitions","OnContainerCreated","OnContainerClosed","m_containerLastSelectedWidgetIndex","m_containerManagers","StackableDataID","m_visibilityGroupsStack","m_uiVisibilityGroupMembers","m_workingUIGroupVisibility"}),
                (Type:typeof(UIScreenTransitionManager), Names:new[]{"m_maximumTimeoutSeconds","m_defaultTransitionIdentifier","StackableDataMidpointWaitID","m_uiManagerRef","m_transitionStackableData","m_currentTransitionIdentifier","m_currentTransition","m_maximumTimeoutCoroutine"}),
                (Type:typeof(UIVisibilityGroupMember), Names:new[]{"m_visibilityGroupDefinition","m_setGameObjectActive","m_onBecomeVisible","m_onBecomeInvisible","m_uiManagerSystemRef","m_gameObject","m_isVisible","m_initialVisibilitySet"}) })
            {
                Check(row.Type.BaseType == typeof(MonoBehaviour), row.Type.Name + " complete genuine MonoBehaviour base");
                Check(row.Type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(row.Names), row.Type.Name + " all original fields/order");
                var options = row.Type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Check(options.Select(a => a.Option).SequenceEqual(new[]{Option.ArrayBoundsChecks, Option.NullChecks}) && options.All(a => Equals(a.Value, false)), row.Type.Name + " exact native class options");
            }
            Check(typeof(UIManager).IsSealed && typeof(UIManager).GetInterfaces().SequenceEqual(new[]{typeof(ISystem)}), "original sealed UIManager and real empty ISystem contract");
            Check(!typeof(UIScreenTransitionManager).IsSealed && typeof(UIScreenTransitionManager).GetInterfaces().SequenceEqual(new[]{typeof(ISystem)}), "original extensible transition host and ISystem contract");
            Check(typeof(UIVisibilityGroupMember).IsDefined(typeof(DisallowMultipleComponent), false) && !typeof(UIManager).IsDefined(typeof(DisallowMultipleComponent), false), "original component attributes have no invented UIManager restriction");
            Check(Field(typeof(UIManager), "m_guiCamera").FieldType == typeof(Camera) && Field(typeof(UIManager), "m_freeParent").FieldType == typeof(Transform), "actual Unity camera/parent types");
            Check(Field(typeof(UIManager), "OnContainerCreated").IsPublic && Field(typeof(UIManager), "OnContainerClosed").IsPrivate && typeof(UIManager).GetEvent("OnContainerClosed") != null, "original public Action versus real event backing graph");
            Check(typeof(UIManager).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.IsGenericMethod).All(m => m.GetGenericArguments()[0].GetGenericParameterConstraints().SequenceEqual(new[]{typeof(UIContainer)})), "all three genuine container generic constraints");
            foreach (var row in new[] {
                (Outer:typeof(UIManager), Name:"<>c", Fields:new[]{"<>9","<>9__42_0"}),
                (Outer:typeof(UIScreenTransitionManager), Name:"<>c__DisplayClass12_0", Fields:new[]{"<>4__this","onMidpointCallback","onCompletionCallback"}),
                (Outer:typeof(UIScreenTransitionManager), Name:"<WaitForTransition>d__11", Fields:new[]{"<>1__state","<>2__current","<>4__this","containerIdentifier","onMidpointCallback","onCompletionCallback","onTimeoutCallback"}),
                (Outer:typeof(UIScreenTransitionManager), Name:"<OnTransitionMaximumTimeout>d__17", Fields:new[]{"<>1__state","<>2__current","<>4__this","onCompletionCallback"}) })
            {
                Type generated = row.Outer.GetNestedType(row.Name, BindingFlags.NonPublic);
                Check(generated != null, "natural original generated type " + row.Name);
                Check(generated.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(row.Fields), row.Name + " full natural field order without padding");
            }
            var registryFields = typeof(HardlightEnumComparers).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(registryFields.Length == 11 && registryFields.All(f => f.IsInitOnly), "whole genuine Hardlight enum registry11 readonly fields");
            Check((typeof(HardlightEnumComparers).Attributes & TypeAttributes.BeforeFieldInit) != 0, "original registry preserves BeforeFieldInit");
            foreach (FieldInfo field in registryFields)
            {
                object comparer = field.GetValue(null); Type type = field.FieldType; var equal = type.GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly); var hash = type.GetMethod("GetHashCode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly); Type enumType = hash.GetParameters()[0].ParameterType;
                Check(comparer != null && comparer.GetType() == type, field.Name + " real allocated comparer");
                Check(enumType.IsEnum && Enum.GetUnderlyingType(enumType) == typeof(int), field.Name + " genuine full Int32 enum");
                object negative = Enum.ToObject(enumType, int.MinValue), positive = Enum.ToObject(enumType, int.MaxValue);
                Check((bool)equal.Invoke(comparer, new[]{negative, negative}) && !(bool)equal.Invoke(comparer, new[]{negative, positive}), field.Name + " raw equality including undeclared values");
                Check((int)hash.Invoke(comparer, new[]{negative}) == int.MinValue && (int)hash.Invoke(comparer, new[]{positive}) == int.MaxValue, field.Name + " raw signed hash bits");
            }
        }
        private static void VisibilityAndQueries()
        {
            UIVisibilityGroupMember member = Raw<UIVisibilityGroupMember>();
            member.OnVisibilityChanged(false);
            Check(!(bool)Field(typeof(UIVisibilityGroupMember), "m_isVisible").GetValue(member) && (bool)Field(typeof(UIVisibilityGroupMember), "m_initialVisibilitySet").GetValue(member), "initial false visibility publishes initialization even when value did not change");
            member.OnVisibilityChanged(false); Check(!(bool)Field(typeof(UIVisibilityGroupMember), "m_isVisible").GetValue(member), "same false initialized value retains original early return");
            member.OnVisibilityChanged(true); Check((bool)Field(typeof(UIVisibilityGroupMember), "m_isVisible").GetValue(member), "changed true visibility publishes before optional event and activation");
            Field(typeof(UIVisibilityGroupMember), "m_initialVisibilitySet").SetValue(member, false); member.OnVisibilityChanged(true);
            Check((bool)Field(typeof(UIVisibilityGroupMember), "m_initialVisibilitySet").GetValue(member), "same true uninitialized value still initializes");
            var manager = RawManager(); int notifications = 0;
            manager.OnContainerClosed += _ => notifications++;
            Check(!manager.IsOpen(null) && !manager.IsOnlyContainerOpen(null), "empty manager map does not invent an open identifier");
            Check(manager.AreExclusivelyOpen(null), "empty manager map preserves original subset true for null list");
            UIContainer found; Check(!manager.TryGet(null, out found) && ReferenceEquals(found, null), "empty failed TryGet assigns null after iteration");
            HardlightProject.UIContainerSimple typed; Check(!manager.TryGetAs(null, out typed) && ReferenceEquals(typed, null), "genuine constrained TryGetAs preserves absent null result");
            manager.SetEnabled(null, false); manager.CloseAllContainers(); manager.CloseAllContainersExcept(null); manager.Close(null);
            Check(notifications == 1, "Close emits original event even without any owned container");
            manager.OnContainerClosed -= _ => notifications++;
            Check(notifications == 1, "removing a different original delegate does not emit a notification");
            Field(typeof(UIManager), "OnContainerClosed").SetValue(manager, null);
            Throws<NullReferenceException>(() => manager.Close(null), "original Close directly invokes event with no fabricated null fallback");
            Throws<NullReferenceException>(() => manager.GetOrCreate((UIContainerIdentifier)null), "opening a missing identifier retains original required-prefab boundary");
            Throws<NullReferenceException>(() => manager.SetSelectedWidget(null, 5), "selected widget routes require original real container identifier");
            Throws<NullReferenceException>(() => manager.TryGetSelectedWidget(null, out _), "selected widget read preserves the missing container boundary");
            var definitions = (List<UIVisibilityGroupDefinition>)Field(typeof(UIManager), "m_visibilityGroupDefinitions").GetValue(manager);
            definitions.Add(Definition("a", true)); definitions.Add(Definition("b", false));
            var working = (Dictionary<string, bool>)Field(typeof(UIManager), "m_workingUIGroupVisibility").GetValue(manager);
            working.Add("stale", true);
            var combine = (StackableData.ResultCarrier<Dictionary<string, bool>>.Operation)Delegate.CreateDelegate(typeof(StackableData.ResultCarrier<Dictionary<string, bool>>.Operation), manager, typeof(UIManager).GetMethod("VisibilityGroupsStackCombiner", BindingFlags.Instance | BindingFlags.NonPublic));
            var result = default(StackableData.ResultCarrier<Dictionary<string, bool>>);
            Check(combine(new StackableData.StackableDataContainer<Dictionary<string, bool>>(new Dictionary<string, bool>{{"a",false}}), ref result) == StackableData.OperationAction.Continue, "partial first override continues native combination");
            Check(ReferenceEquals(result.Value, working) && result.HasValue && !working.ContainsKey("stale") && working["a"] == false, "unset carrier clears shared working map and publishes exact original alias");
            Check(combine(new StackableData.StackableDataContainer<Dictionary<string, bool>>(new Dictionary<string, bool>{{"a",true},{"b",false}}), ref result) == StackableData.OperationAction.EarlyExit, "count equality selects original early exit");
            Check(!working["a"] && !working["b"], "first encountered duplicate GUID wins through original TryAdd");
            var alternate = new Dictionary<string, bool>{{"other",true}}; var present = new StackableData.ResultCarrier<Dictionary<string, bool>>(alternate);
            combine(new StackableData.StackableDataContainer<Dictionary<string, bool>>(new Dictionary<string, bool>{{"unknown",true}}), ref present);
            Check(ReferenceEquals(present.Value, alternate) && working.ContainsKey("unknown") && !alternate.ContainsKey("unknown"), "present carrier retains original value while combiner still mutates shared working map");
            Check(working.Count == 3, "unknown GUID participates in native count without filtering");
            var stack = new StackableData(); Field(typeof(UIManager), "m_visibilityGroupsStack").SetValue(manager, stack);
            StackableData.RegisterTypeOperations<Dictionary<string,bool>>(combine);
            stack.SetBaseValue(0, new Dictionary<string,bool>{{"a",true},{"b",false}}, StackableData.RetrievalOperation.LogicalAnd);
            Check(stack.Get<Dictionary<string,bool>>(0)["a"] && !stack.Get<Dictionary<string,bool>>(0)["b"], "real maintained StackableData invokes original native UI combiner");
            StackableDataHandle handle = manager.AddVisibilityOverrides(new Dictionary<string,bool>{{"a",false}});
            Check(!stack.Get<Dictionary<string,bool>>(0)["a"] && !stack.Get<Dictionary<string,bool>>(0)["b"], "latest genuine override wins before base dictionary fills missing GUID");
            manager.RemoveVisibilityOverrides(handle); Check(stack.Get<Dictionary<string,bool>>(0)["a"], "removing original handle restores base priority");
            var members = (Dictionary<string,List<UIVisibilityGroupMember>>)Field(typeof(UIManager), "m_uiVisibilityGroupMembers").GetValue(manager);
            members.Add("a", new List<UIVisibilityGroupMember>{member});
            Invoke(manager, "OnStackableDataUpdated", 99); Check((bool)Field(typeof(UIVisibilityGroupMember), "m_isVisible").GetValue(member), "unrelated stack ID does not traverse visibility members");
            stack.AddOverride(0, new Dictionary<string,bool>{{"a",false}}); Invoke(manager, "OnStackableDataUpdated", 0);
            Check(!(bool)Field(typeof(UIVisibilityGroupMember), "m_isVisible").GetValue(member), "real original live member traversal applies computed visibility");
            manager.UnregisterUIVisibilityGroupMember(definitions[0], member);
            Check(members.ContainsKey("a") && members["a"].Count == 0, "unregister retains genuine empty GUID member list");
        }
        private static void TransitionIterators()
        {
            var host = Raw<UIScreenTransitionManager>(); var stack = new StackableData();
            Field(typeof(UIScreenTransitionManager), "m_transitionStackableData").SetValue(host, stack);
            Field(typeof(UIScreenTransitionManager), "m_uiManagerRef").SetValue(host, new SystemRef<UIManager>("private-verification-owned-ui", RawManager()));
            stack.SetBaseValue(0, true, StackableData.RetrievalOperation.LogicalAnd);
            Check(!host.IsTransitionActive(), "original identifier GUID-null activity begins false");
            Throws<NullReferenceException>(() => host.HasReachedMidPoint(), "original midpoint getter directly calls missing current transition");
            StackableDataHandle wait = host.AddWaitForMidpointOverride();
            Check(!stack.Get<bool>(0), "genuine false midpoint override participates in Boolean LogicalAnd");
            int callback = 0; Invoke(host, "OnReachMidpoint", (Action)(() => callback++));
            Check(callback == 1 && !stack.Get<bool>(0), "original midpoint callback executes before wait-stack read");
            Check(typeof(StackableData).GetField("OnDataUpdated", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stack) != null, "blocked midpoint subscribes actual original stack notification");
            Invoke(host, "OnMidpointWaitValueUpdated", 7); Check(!stack.Get<bool>(0), "unrelated midpoint update retains wait");
            Throws<NullReferenceException>(() => host.ReleaseWaitForMidpointHandle(wait), "released wait reaches real ContinueTransition and preserves missing actual transition failure");
            Check(stack.Get<bool>(0) && typeof(StackableData).GetField("OnDataUpdated", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stack) != null, "original failing continuation leaves subscription after changed true stack");
            Invoke(host, "OnTransitionComplete", (Action)(() => callback++)); Check(callback == 1, "inactive completion guard skips callback and host lookup");
            var iterator = (IEnumerator)Invoke(host, "OnTransitionMaximumTimeout", (Action)(() => callback++));
            Field(typeof(UIScreenTransitionManager), "m_maximumTimeoutSeconds").SetValue(host, 27);
            Check(iterator.MoveNext() && iterator.Current is WaitForSecondsRealtime, "original timeout wrapper defers constructing actual realtime yield until MoveNext");
            var yielded = (WaitForSecondsRealtime)iterator.Current;
            Check(yielded.waitTime == 27f, "timeout reads live original integer field at first MoveNext");
            Field(typeof(UIScreenTransitionManager), "m_maximumTimeoutSeconds").SetValue(host, 1);
            Check(yielded.waitTime == 27f, "created realtime wait retains originally sampled float seconds");
            ((IDisposable)iterator).Dispose(); Check(!iterator.MoveNext() && ReferenceEquals(iterator.Current, yielded) && callback == 1, "genuine RET Dispose does not cancel timeout iterator; inactive completion preserves last Current");
            Check(!iterator.MoveNext(), "completed original timeout iterator remains false");
            Throws<NotSupportedException>(() => iterator.Reset(), "original generated timeout Reset throws");
            var waiting = (IEnumerator)Invoke(host, "WaitForTransition", null, null, null, null);
            Check(waiting.MoveNext() && waiting.Current is WaitUntil, "idle transition still yields real WaitUntil before start");
            var condition = (WaitUntil)waiting.Current;
            Check(!condition.keepWaiting, "natural original callback negates GUID activity without replacement polling");
            ((IDisposable)waiting).Dispose(); Throws<NullReferenceException>(() => waiting.MoveNext(), "RET Dispose preserves next start and original missing identifier boundary");
            Check(!waiting.MoveNext() && ReferenceEquals(waiting.Current, condition), "faulted original iterator retains yield and consumed state");
            Throws<NotSupportedException>(() => waiting.Reset(), "original generated wait Reset throws");
        }
        public static int RunManaged()
        {
            checks = 0;
            var map = (IDictionary)typeof(StackableData).GetField("LogicalAnds", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Type key = typeof(Dictionary<string,bool>); bool existed = map.Contains(key); object previous = existed ? map[key] : null;
            try { Declarations(); VisibilityAndQueries(); TransitionIterators(); return checks; }
            finally { if (existed) map[key] = previous; else map.Remove(key); }
        }
        public static void Run() { int total = RunManaged(); Debug.Log("PASS original UI startup managed/type/iterator checks=" + total + "; actual host/member/coroutine and authored content proof remains separate."); }
    }
}
