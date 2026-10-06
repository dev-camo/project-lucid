using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObject = UnityEngine.Object;
namespace ProjectLucid.Tests
{
    public sealed class UICollectionsLifecycleTests
    {
        private static int checks;
        private static void Check(bool value, string label) { Assert.That(value, Is.True, label); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static Type ManagerType(string name) => typeof(UIContainerManager).Assembly.GetType("Hardlight." + name, true);
        private static UIContainerManager Manager(string name, Transform parent) => (UIContainerManager)Activator.CreateInstance(ManagerType(name), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { parent }, null);
        private static void Identifier(UIContainer container, UIContainerIdentifier id) => Field(typeof(UIContainer), "m_identifier").SetValue(container, id);
        private static int RefCount(UIContainerIdentifier id) => (int)Field(typeof(UIContainerIdentifier), "m_refCount").GetValue(id);
        private static void RefCount(UIContainerIdentifier id, int value) => Field(typeof(UIContainerIdentifier), "m_refCount").SetValue(id, value);
        // Actual Unity frame fixture only. No supplied assets, Addressable
        // handles/providers or global UI/App registry are involved.
        [UnityTest]
        public IEnumerator OriginalDebugFreeLinkedStackManagersKeepOwnedCloneAndCleanupQuirks()
        {
            checks = 0; int managed = 0;
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
                Assert.That(checks, Is.EqualTo(58), "exact owned frame assertion count");
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
