using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid
{
    public static class UIManagerFamilyVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Genuine UI manager verification: " + label); checks++; }
        private static Type SingleType => typeof(UIContainerManager).Assembly.GetType("Hardlight.UISingular", true);
        private static UIContainerManager Single(Transform parent) => (UIContainerManager)Activator.CreateInstance(SingleType, new object[] { parent });
        private static FieldInfo Own(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static int RefCount(UIContainerIdentifier id) => (int)Own(typeof(UIContainerIdentifier), "m_refCount").GetValue(id);
        private static void SetRefCount(UIContainerIdentifier id, int value) => Own(typeof(UIContainerIdentifier), "m_refCount").SetValue(id, value);
        private static void SetIdentifier(UIContainer container, UIContainerIdentifier id) => Own(typeof(UIContainer), "m_identifier").SetValue(container, id);
        private static void Declarations()
        {
            Check((int)typeof(UIContainerManager).Attributes == 1048705 && typeof(UIContainerManager).BaseType == typeof(object), "original complete abstract fieldless manager base");
            Check((int)SingleType.Attributes == 1048832 && SingleType.BaseType == typeof(UIContainerManager), "original internal sealed singular visibility and genuine base");
            Check((int)typeof(UITransitionContainerManager).Attributes == 1048577 && typeof(UITransitionContainerManager).BaseType == typeof(UIContainerManager), "original public unsealed transition manager");
            foreach (var row in new[] { new { Type = typeof(UIContainerManager), Expected = new[] { Option.ArrayBoundsChecks, Option.NullChecks } }, new { Type = SingleType, Expected = new[] { Option.ArrayBoundsChecks, Option.NullChecks } }, new { Type = typeof(UITransitionContainerManager), Expected = new[] { Option.NullChecks, Option.ArrayBoundsChecks } } })
            {
                var attrs = row.Type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray(); Check(attrs.Select(a => a.Option).SequenceEqual(row.Expected) && attrs.All(a => Equals(a.Value, false)), row.Type.Name + " exact ordered original options");
                Check(row.Type.GetInterfaces().Length == 0, row.Type.Name + " original zero direct interfaces");
            }
            var baseMethods = typeof(UIContainerManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken).ToArray();
            Check(baseMethods.Select(m => m.Name).SequenceEqual(new[] { "OpenContainer", "CloseAll", "Close", "IsOpen", "IsAnyOpen", "AreExclusivelyOpen", "TryGet", "CloseAllExcept", "SetEnabled" }), "complete original base method order");
            Check(baseMethods.Take(8).All(m => m.IsAbstract && m.IsVirtual) && !baseMethods.Last().IsAbstract && !baseMethods.Last().IsVirtual, "exact eight real contracts and concrete nonvirtual SetEnabled");
            Check(typeof(UIContainerManager).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "original base has no own fields");
            foreach (var row in new[] { new { Type = SingleType, Container = "m_singleContainer" }, new { Type = typeof(UITransitionContainerManager), Container = "m_transitionContainer" } })
            {
                var fields = row.Type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
                Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_parent", row.Container }) && fields[0].FieldType == typeof(Transform) && fields[1].FieldType == typeof(UIContainer), row.Type.Name + " full original two-field graph/order");
                Check(fields.All(f => f.IsPrivate && !f.IsDefined(typeof(SerializeField), false)) && fields[0].IsInitOnly && !fields[1].IsInitOnly, row.Type.Name + " original readonly parent and mutable nonserialized storage");
                var ctor = row.Type.GetConstructors().Single(); Check(ctor.GetParameters().Single().Name == "parent" && ctor.GetParameters().Single().ParameterType == typeof(Transform), row.Type.Name + " original constructor signature");
                var outArg = row.Type.GetMethod("TryGet").GetParameters()[1]; Check(outArg.Name == "container" && outArg.IsOut && outArg.ParameterType == typeof(UIContainer).MakeByRefType(), row.Type.Name + " complete original out contract");
            }
        }
        public static int RunManaged()
        {
            checks = 0; Declarations();
            foreach (UIContainerManager manager in new[] { Single(null), new UITransitionContainerManager(null) })
            {
                Check(Own(manager.GetType(), "m_parent").GetValue(manager) == null, manager.GetType().Name + " genuine constructor stores passed null parent after real base construction");
                Check(!manager.IsAnyOpen(), manager.GetType().Name + " empty original Unity predicate path");
                Check(!manager.IsOpen(null), manager.GetType().Name + " empty IsOpen avoids identifier comparison");
                Check(manager.AreExclusivelyOpen(null), manager.GetType().Name + " original empty manager accepts null requested list without reading it");
                Check(manager.AreExclusivelyOpen(new UIContainerIdentifier[] { null, null }), manager.GetType().Name + " original empty manager accepts nonempty requested list");
                UIContainer found; Check(!manager.TryGet(null, out found) && ReferenceEquals(found, null), manager.GetType().Name + " failed original TryGet writes null");
                manager.SetEnabled(null, false); manager.Close(null); manager.CloseAll(); manager.CloseAllExcept(null); Check(!manager.IsAnyOpen(), manager.GetType().Name + " original empty guard routes require no provider/container/canvas");
            }
            return checks;
        }
        public static void Run() { int total = RunManaged(); Debug.Log("PASS genuine UI manager declarations/empty predicates checks=" + total + "; clone/frame proof remains separate."); }
        // Actual runtime only: deferred Object.Destroy must keep its original
        // timing. Root may wrap this owned fixture in a bounded UnityTest.
        public static IEnumerator RunOwnedRuntime()
        {
            int managed = RunManaged();
            GameObject parentOwner = null, prefabOwner = null; UIContainerIdentifier first = null, alias = null;
            var allocatedClones = new List<GameObject>();
            try
            {
                parentOwner = new GameObject("Lucid owned UI manager parent"); parentOwner.SetActive(false); prefabOwner = new GameObject("Lucid owned actual container template"); prefabOwner.SetActive(false);
                var prefab = prefabOwner.AddComponent<UIContainerSimple>(); var prefabCanvas = prefabOwner.AddComponent<Canvas>(); Own(typeof(UIContainer), "m_canvas").SetValue(prefab, prefabCanvas); first = ScriptableObject.CreateInstance<UIContainerIdentifier>(); alias = ScriptableObject.CreateInstance<UIContainerIdentifier>(); SetIdentifier(prefab, first);
                Own(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(first, "lucid-owned-manager-guid"); Own(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(alias, "lucid-owned-manager-guid");
                foreach (UIContainerManager manager in new[] { Single(parentOwner.transform), new UITransitionContainerManager(parentOwner.transform) })
                {
                    SetRefCount(first, 1); var created = manager.OpenContainer(prefab); allocatedClones.Add(created.gameObject);
                    Check(created != null && !ReferenceEquals(created, prefab) && created.transform.parent == parentOwner.transform, manager.GetType().Name + " original typed instantiate uses genuine parent");
                    SetIdentifier(created, null); Check(!manager.IsAnyOpen() && created != null, manager.GetType().Name + " original live container with missing identifier is not any-open");
                    UIContainer missingId; Check(manager.IsOpen(null) && manager.TryGet(null, out missingId) && ReferenceEquals(missingId, created), manager.GetType().Name + " live null identifier matches requested null despite distinct IsAnyOpen rule"); SetIdentifier(created, first);
                    Check(manager.IsOpen(first) && manager.IsOpen(alias), manager.GetType().Name + " original GUID equality accepts distinct equal identifiers");
                    UIContainer found; Check(manager.TryGet(alias, out found) && ReferenceEquals(found, created), manager.GetType().Name + " TryGet returns live manager field after GUID test");
                    var canvas = created.GetComponent<Canvas>(); manager.SetEnabled(alias, false); Check(canvas != null && !canvas.enabled && !created.gameObject.activeSelf, manager.GetType().Name + " successful original SetEnabled dispatch changes cloned Canvas only");
                    manager.SetEnabled(alias, true); Check(canvas.enabled && !created.gameObject.activeSelf, manager.GetType().Name + " original Canvas re-enable retains inactive owner");
                    Check(manager.AreExclusivelyOpen(new[] { first, alias }), manager.GetType().Name + " genuine membership-only exclusivity ignores duplicate/requested count");
                    SetRefCount(alias, 3); manager.Close(alias); Check(!manager.IsAnyOpen() && RefCount(alias) == 2 && RefCount(first) == 1, manager.GetType().Name + " Close clears manager before closing passed equal-GUID object, not stored object");
                    Check(created != null, manager.GetType().Name + " original destruction remains deferred in same frame"); yield return null; Check(created == null, manager.GetType().Name + " original Destroy takes effect after frame");
                    SetRefCount(first, 1); var previous = manager.OpenContainer(prefab); allocatedClones.Add(previous.gameObject); SetRefCount(first, 3); var replacement = manager.OpenContainer(prefab); allocatedClones.Add(replacement.gameObject);
                    Check(!ReferenceEquals(previous, replacement) && RefCount(first) == 2, manager.GetType().Name + " Open replaces even same identifier and invokes prior Close");
                    yield return null; Check(previous == null && replacement != null, manager.GetType().Name + " previous clone destroyed while replacement remains");
                    manager.CloseAllExcept(new[] { alias }); Check(manager.IsAnyOpen(), manager.GetType().Name + " existing equivalent GUID exclusion retains container");
                    manager.CloseAllExcept(Array.Empty<UIContainerIdentifier>()); Check(!manager.IsAnyOpen() && RefCount(first) == 1, manager.GetType().Name + " no exclusion invokes original close route");
                    yield return null; Check(replacement == null, manager.GetType().Name + " exclusion close uses deferred destruction");
                }
                Debug.Log("PASS genuine UI manager owned runtime checks=" + checks + "; managed=" + managed + "; no authored prefab/catalog/UIManager/App approval.");
            }
            finally
            {
                try { foreach (var clone in allocatedClones) if (clone != null) UObject.DestroyImmediate(clone); }
                finally { try { if (prefabOwner != null) UObject.DestroyImmediate(prefabOwner); }
                finally { try { if (parentOwner != null) UObject.DestroyImmediate(parentOwner); }
                finally { try { if (alias != null) UObject.DestroyImmediate(alias); }
                finally { if (first != null) UObject.DestroyImmediate(first); } } } }
            }
        }
    }
}
