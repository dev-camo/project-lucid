using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid
{
    public static class UICollectionsVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Genuine UI collection verification: " + label); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static Type ManagerType(string name) => typeof(UIContainerManager).Assembly.GetType("Hardlight." + name, true);
        private static UIContainerManager Manager(string name, Transform parent) => (UIContainerManager)Activator.CreateInstance(ManagerType(name), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { parent }, null);
        private static void Throws<T>(Action action, string label) where T : Exception { bool caught = false; try { action(); } catch (T) { caught = true; } Check(caught, label); }
        private static void Identifier(UIContainer container, UIContainerIdentifier id) => Field(typeof(UIContainer), "m_identifier").SetValue(container, id);
        private static UIContainer RawContainer() => (UIContainer)FormatterServices.GetUninitializedObject(typeof(UIContainerSimple));
        private static void DeclarationChecks()
        {
            foreach (string name in new[] { "UIDebug", "UIFree", "UILinked", "UIStacker" })
            {
                Type type = ManagerType(name); Check((int)type.Attributes == 1048832 && type.BaseType == typeof(UIContainerManager), name + " original internal sealed type and genuine base");
                string storage = name == "UIDebug" ? "m_debugContainers" : name == "UIFree" ? "m_freeContainers" : name == "UILinked" ? "m_linkedContainers" : "m_stackContainers";
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
                Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_parent", storage }) && fields.All(f => f.IsPrivate && f.IsInitOnly), name + " full original field names/order/readonly flags");
                Check(fields[0].FieldType == typeof(Transform) && fields.All(f => f.GetCustomAttributes(false).Length == 0), name + " genuine parent and unannotated nonserialized fields");
                var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single(); Check(ctor.IsAssembly && ctor.GetParameters().Single().Name == "parent" && ctor.GetParameters().Single().ParameterType == typeof(Transform), name + " original internal parent constructor");
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray(); Option[] expected = name == "UIDebug" || name == "UIStacker" ? new[] { Option.ArrayBoundsChecks, Option.NullChecks } : new[] { Option.NullChecks, Option.ArrayBoundsChecks };
                Check(options.Select(x => x.Option).SequenceEqual(expected) && options.All(x => Equals(x.Value, false)), name + " exact ordered original class options");
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken).ToArray();
                Check(methods.Select(m => m.Name).SequenceEqual(new[] { "OpenContainer", "Close", "IsOpen", "IsAnyOpen", "AreExclusivelyOpen", "TryGet", "CloseAllExcept", "CloseAll" }) && methods.All(m => m.IsVirtual && !m.IsFinal && !m.IsAbstract), name + " exact original eight override surfaces/order");
                var outArg = type.GetMethod("TryGet").GetParameters()[1]; Check(outArg.Name == "container" && outArg.IsOut && outArg.ParameterType == typeof(UIContainer).MakeByRefType(), name + " genuine out result contract");
            }
            foreach (string name in new[] { "<Reverse>d__0`1", "<Nodes>d__1`1", "<NodesReverse>d__2`1" })
            {
                Type iterator = typeof(LinkedListExtensions).GetNestedType(name, BindingFlags.NonPublic); Check(iterator != null && iterator.IsSealed && iterator.IsNestedPrivate, name + " natural original compiler identity");
                Check(iterator.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(new[] { "<>1__state", "<>2__current", "<>l__initialThreadId", "linkedList", "<>3__linkedList", "<element>5__2" }), name + " all six natural original fields/order");
            }
        }
        private static void LinkedChecks()
        {
            var list = new LinkedList<int>(); var one = list.AddLast(1); var two = list.AddLast(2); var three = list.AddLast(3);
            Check(list.Reverse().SequenceEqual(new[] { 3, 2, 1 }), "original value reverse order for genuine value-type generic argument");
            Check(list.Nodes().SequenceEqual(new[] { one, two, three }), "original forward node reference identity/order");
            Check(list.NodesReverse().SequenceEqual(new[] { three, two, one }), "original reverse node reference identity/order");
            var empty = new LinkedList<string>(); Check(!empty.Reverse().Any() && !empty.Nodes().Any() && !empty.NodesReverse().Any(), "all three empty original iterators terminate");
            var deferred = empty.Nodes(); var added = empty.AddLast("a"); Check(ReferenceEquals(deferred.First(), added), "First is read on first MoveNext after iterator creation");
            var source = new LinkedList<int>(); var first = source.AddLast(7); var nodes = source.Nodes().GetEnumerator();
            Check(nodes.MoveNext() && ReferenceEquals(nodes.Current, first), "node iterator first yield captures genuine current node");
            var second = source.AddLast(8); Check(nodes.MoveNext() && ReferenceEquals(nodes.Current, second), "append after yield is observed without collection version guard");
            source.Remove(second); Check(!nodes.MoveNext() && ReferenceEquals(nodes.Current, second), "detached yielded node stops traversal and retains last Current");
            var removeFuture = new LinkedList<int>(); var a = removeFuture.AddLast(1); var b = removeFuture.AddLast(2); var c = removeFuture.AddLast(3); var e = removeFuture.Nodes().GetEnumerator();
            Check(e.MoveNext() && ReferenceEquals(e.Current, a), "forward mutation fixture first yield"); removeFuture.Remove(b); Check(e.MoveNext() && ReferenceEquals(e.Current, c) && !e.MoveNext(), "removed future node is skipped using current Next link");
            var backward = new LinkedList<string>(); backward.AddLast("x"); var tail = backward.AddLast("y"); var reverse = backward.Reverse().GetEnumerator();
            Check(reverse.MoveNext() && reverse.Current == "y", "value iterator reads yielded node Value"); tail.Value = "changed"; Check(reverse.Current == "y", "already yielded value is captured separately from mutable node"); reverse.Dispose(); Check(reverse.MoveNext() && reverse.Current == "x", "original generated Dispose is RET and does not abort paused traversal");
            Throws<NotSupportedException>(() => ((IEnumerator)reverse).Reset(), "genuine iterator Reset throws original NotSupportedException"); Check(reverse.Current == "x", "Reset failure retains original current value"); Check(!reverse.MoveNext(), "normal reverse iterator exhaustion");
            var nodeReverse = backward.NodesReverse().GetEnumerator(); Check(nodeReverse.MoveNext() && ReferenceEquals(nodeReverse.Current, tail), "reverse node fixture yields live tail"); backward.Remove(tail); Check(!nodeReverse.MoveNext(), "detached reverse current clears Previous and ends traversal");
            var enumerable = new LinkedList<int>(new[] { 4 }).Nodes(); var firstEnumerator = enumerable.GetEnumerator(); var another = enumerable.GetEnumerator();
            Check(ReferenceEquals(enumerable, firstEnumerator) && !ReferenceEquals(firstEnumerator, another), "original initial-thread iterator reuse then fresh second enumerator"); Check(firstEnumerator.MoveNext() && another.MoveNext() && ReferenceEquals(firstEnumerator.Current, another.Current), "independent enumerators share actual node identity");
            LinkedList<int> missing = null; var nullForward = missing.Nodes(); var nullBackward = missing.NodesReverse(); var nullValues = missing.Reverse(); Check(nullForward != null && nullBackward != null && nullValues != null, "null source remains deferred in all wrappers");
            Throws<NullReferenceException>(() => nullForward.GetEnumerator().MoveNext(), "null Nodes fails at first original source read"); Throws<NullReferenceException>(() => nullBackward.GetEnumerator().MoveNext(), "null NodesReverse fails at first original source read"); Throws<NullReferenceException>(() => nullValues.GetEnumerator().MoveNext(), "null Reverse fails at first original source read");
        }
        public static int RunManaged()
        {
            checks = 0; DeclarationChecks(); LinkedChecks();
            foreach (string name in new[] { "UIDebug", "UIFree", "UILinked", "UIStacker" })
            {
                UIContainerManager manager = Manager(name, null); Check(Field(manager.GetType(), "m_parent").GetValue(manager) == null, name + " original constructor stores passed null parent");
                Check(!manager.IsAnyOpen() && !manager.IsOpen(null), name + " empty stored-count and identifier enumeration predicates"); Check(manager.AreExclusivelyOpen(null) && manager.AreExclusivelyOpen(new UIContainerIdentifier[] { null, null }), name + " empty subset succeeds without inspecting requested list");
                if (name == "UIDebug" || name == "UIFree") { Throws<ArgumentNullException>(() => { UIContainer found; manager.TryGet(null, out found); }, name + " direct dictionary null-key behavior"); }
                else { UIContainer found; Check(!manager.TryGet(null, out found) && ReferenceEquals(found, null), name + " failed empty node/stack lookup writes null"); }
                manager.CloseAll(); manager.CloseAllExcept(null); Check(!manager.IsAnyOpen(), name + " empty original bulk routes require no container/provider/Unity engine");
                UIContainer raw = RawContainer();
                if (name == "UILinked") ((LinkedList<UIContainer>)Field(manager.GetType(), "m_linkedContainers").GetValue(manager)).AddLast(raw);
                else if (name == "UIStacker") ((Stack<UIContainer>)Field(manager.GetType(), "m_stackContainers").GetValue(manager)).Push(raw);
                else
                {
                    var id = (UIContainerIdentifier)FormatterServices.GetUninitializedObject(typeof(UIContainerIdentifier)); Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(id, "lucid-managed-opaque-key");
                    ((Dictionary<UIContainerIdentifier, UIContainer>)Field(manager.GetType(), name == "UIDebug" ? "m_debugContainers" : "m_freeContainers").GetValue(manager)).Add(id, raw);
                }
                Check(manager.IsAnyOpen() && manager.IsOpen(null), name + " stored entry counts despite null current identifier; same-reference null comparison avoids engine calls");
                Check(manager.AreExclusivelyOpen(new UIContainerIdentifier[] { null }) && !manager.AreExclusivelyOpen(Array.Empty<UIContainerIdentifier>()), name + " current value identifier subset membership rather than collection-key snapshot");
                if (name == "UILinked" || name == "UIStacker") { UIContainer found; Check(manager.TryGet(null, out found) && ReferenceEquals(found, raw), name + " null current identifier finds captured genuine raw managed fixture"); }
                if (name == "UIDebug") { manager.CloseAll(); manager.CloseAllExcept(null); Check(manager.IsAnyOpen(), "original Debug bulk native RET bodies retain populated storage"); }
            }
            return checks;
        }
        public static void Run() { int total = RunManaged(); Debug.Log("PASS genuine remaining UI collection managed/declaration checks=" + total + "; owned lifecycle proof remains separate."); }
        private static int RefCount(UIContainerIdentifier id) => (int)Field(typeof(UIContainerIdentifier), "m_refCount").GetValue(id);
        private static void RefCount(UIContainerIdentifier id, int value) => Field(typeof(UIContainerIdentifier), "m_refCount").SetValue(id, value);
        // Actual Unity frame fixture only. No supplied assets, Addressable
        // handles/providers or global UI/App registry are involved.
        public static IEnumerator RunOwnedRuntime()
        {
            int managed = RunManaged();
            GameObject parent = null, templateOwner = null; UIContainerIdentifier a = null, alias = null, b = null, c = null;
            var clones = new List<GameObject>();
            try
            {
                parent = new GameObject("Lucid owned collection parent"); parent.SetActive(false);
                templateOwner = new GameObject("Lucid owned actual UI template"); templateOwner.SetActive(false); templateOwner.transform.SetParent(parent.transform, false);
                var template = templateOwner.AddComponent<UIContainerSimple>(); var canvas = templateOwner.AddComponent<Canvas>(); Field(typeof(UIContainer), "m_canvas").SetValue(template, canvas); templateOwner.SetActive(true);
                a = ScriptableObject.CreateInstance<UIContainerIdentifier>(); alias = ScriptableObject.CreateInstance<UIContainerIdentifier>(); b = ScriptableObject.CreateInstance<UIContainerIdentifier>(); c = ScriptableObject.CreateInstance<UIContainerIdentifier>();
                Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(a, "lucid-owned-collection-a"); Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(alias, "lucid-owned-collection-a"); Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(b, "lucid-owned-collection-b"); Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(c, "lucid-owned-collection-c");
                foreach (string name in new[] { "UIDebug", "UIFree", "UILinked", "UIStacker" })
                {
                    RefCount(a, 2); RefCount(alias, 3); RefCount(b, 2); RefCount(c, 2); Identifier(template, a);
                    var manager = Manager(name, parent.transform); var first = manager.OpenContainer(template); clones.Add(first.gameObject);
                    Check(first != null && !ReferenceEquals(first, template) && first.transform.parent == parent.transform, name + " original typed instantiate keeps actual owned parent");
                    Check(manager.IsAnyOpen() && manager.IsOpen(alias), name + " original stored count/GUID alias predicates"); UIContainer found; Check(manager.TryGet(alias, out found) && ReferenceEquals(found, first), name + " original successful lookup captures actual clone");
                    Check(manager.AreExclusivelyOpen(new[] { alias, a }), name + " membership-only exclusivity accepts aliases and extra requested entries");
                    Check(ReferenceEquals(manager.OpenContainer(template), first), name + " existing identifier reuses original container");
                    var firstCanvas = first.GetComponent<Canvas>(); bool active = first.gameObject.activeSelf; manager.SetEnabled(alias, false); Check(!firstCanvas.enabled && first.gameObject.activeSelf == active, name + " genuine base dispatch disables Canvas without changing owner activation"); manager.SetEnabled(alias, true); Check(firstCanvas.enabled && first.gameObject.activeSelf == active, name + " base dispatch re-enables Canvas without changing owner activation");
                    Identifier(first, null); Check(manager.IsAnyOpen() && manager.IsOpen(null), name + " populated collection counts live entry with null current ID"); Identifier(first, a);
                    if (name == "UIDebug" || name == "UIFree")
                    {
                        Identifier(first, b); Check(!manager.IsOpen(a) && manager.IsOpen(b) && manager.TryGet(a, out found) && ReferenceEquals(found, first) && !manager.TryGet(b, out found), name + " query compares current value ID while lookup retains dictionary key");
                        if (name == "UIDebug")
                        {
                            manager.CloseAll(); manager.CloseAllExcept(Array.Empty<UIContainerIdentifier>()); Check(manager.IsAnyOpen() && first != null && RefCount(a) == 2 && RefCount(b) == 2, "Debug genuine native RET bulk methods leave entries and leases unchanged");
                            Identifier(first, a); manager.Close(alias); Check(!manager.IsAnyOpen() && RefCount(alias) == 2 && RefCount(a) == 2, "Debug individual close removes key and releases passed alias rather than stored ID");
                        }
                        else
                        {
                            manager.CloseAllExcept(new[] { a }); Check(manager.IsAnyOpen() && first != null && RefCount(a) == 2 && RefCount(b) == 2, "Free exclusion uses dictionary keys despite changed value identifier");
                            manager.CloseAll(); Check(!manager.IsAnyOpen() && RefCount(b) == 1 && RefCount(a) == 2, "Free bulk close releases stored current ID rather than dictionary key");
                        }
                        Check(first != null, name + " original Destroy remains deferred in calling frame"); yield return null; Check(first == null, name + " original owner destruction completes on later frame");
                    }
                    else if (name == "UIStacker")
                    {
                        Identifier(template, b); var second = manager.OpenContainer(template); clones.Add(second.gameObject); Check(manager.IsOpen(a) && manager.IsOpen(b), "Stack stores both actual distinct identifiers");
                        manager.CloseAllExcept(new[] { b }); Check(manager.IsOpen(a) && manager.IsOpen(b) && RefCount(a) == 2 && RefCount(b) == 2, "Stack excluded top stops traversal even though lower entry is not excluded");
                        manager.Close(alias); Check(!manager.IsAnyOpen() && RefCount(alias) == 2 && RefCount(a) == 2 && RefCount(b) == 2, "Stack target close destroys preceding entries without releasing their stored IDs");
                        Check(first != null && second != null, "Stack both original destroys remain deferred"); yield return null; Check(first == null && second == null, "Stack original popped and captured target destruction complete next frame");
                        Identifier(template, a); var allA = manager.OpenContainer(template); clones.Add(allA.gameObject); Identifier(template, b); var allB = manager.OpenContainer(template); clones.Add(allB.gameObject); manager.CloseAll(); Check(!manager.IsAnyOpen() && RefCount(a) == 1 && RefCount(b) == 1, "Stack CloseAll instead releases each stored ID in pop order"); yield return null; Check(allA == null && allB == null, "Stack bulk original deferred destruction completes");
                    }
                    else
                    {
                        Identifier(template, b); var second = manager.OpenContainer(template); clones.Add(second.gameObject); Identifier(template, c); var third = manager.OpenContainer(template); clones.Add(third.gameObject);
                        Check(!first.gameObject.activeSelf && !second.gameObject.activeSelf && third.gameObject.activeSelf, "Linked creation hides previous Last only and retains template activation on new clone");
                        Identifier(template, a); Check(ReferenceEquals(manager.OpenContainer(template), first) && first.gameObject.activeSelf && !second.gameObject.activeSelf && !third.gameObject.activeSelf, "Linked reuse hides every current value before activating captured result");
                        manager.Close(alias); Check(!manager.IsOpen(a) && !manager.IsOpen(b) && manager.IsOpen(c) && RefCount(alias) == 2 && RefCount(a) == 2 && RefCount(b) == 2, "Linked target close removes only immediate successor via detached-node advance and releases passed alias only");
                        Check(first != null && second != null && third != null && !third.gameObject.activeSelf, "Linked close schedules two destroys without reactivating retained later entry"); yield return null; Check(first == null && second == null && third != null, "Linked detached advance retains genuine third clone after frame");
                        manager.CloseAll(); Check(!manager.IsAnyOpen() && RefCount(c) == 1, "Linked bulk close releases stored remaining ID and clears list"); yield return null; Check(third == null, "Linked bulk owner destruction remains deferred");
                        RefCount(a, 2); RefCount(b, 2); Identifier(template, a); var excludedA = manager.OpenContainer(template); clones.Add(excludedA.gameObject); Identifier(template, b); var excludedB = manager.OpenContainer(template); clones.Add(excludedB.gameObject);
                        manager.CloseAllExcept(new[] { b }); Check(!manager.IsAnyOpen() && RefCount(a) == 1 && RefCount(b) == 2, "Linked exclusion snapshot delegates original Close, whose successor removal also removes excluded next entry without ID release"); yield return null; Check(excludedA == null && excludedB == null, "Linked excluded successor follows original deferred Destroy");
                    }
                }
                Debug.Log("PASS genuine remaining UI collection owned frame checks=" + checks + "; managed=" + managed + "; authored content/UIManager/App unapproved.");
            }
            finally
            {
                try { foreach (GameObject clone in clones) if (clone != null) UObject.DestroyImmediate(clone); }
                finally { try { if (templateOwner != null) UObject.DestroyImmediate(templateOwner); }
                finally { try { if (parent != null) UObject.DestroyImmediate(parent); }
                finally { try { if (c != null) UObject.DestroyImmediate(c); }
                finally { try { if (b != null) UObject.DestroyImmediate(b); }
                finally { try { if (alias != null) UObject.DestroyImmediate(alias); }
                finally { if (a != null) UObject.DestroyImmediate(a); } } } } } }
            }
        }
    }
}
