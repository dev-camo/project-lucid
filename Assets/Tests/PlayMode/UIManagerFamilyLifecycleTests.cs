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
    public sealed class UIManagerFamilyLifecycleTests
    {
        private int checks;
        private void Check(bool value, string label) { Assert.That(value, Is.True, label); checks++; }
        private static FieldInfo Own(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static UIContainerManager Single(Transform parent) => (UIContainerManager)Activator.CreateInstance(typeof(UIContainerManager).Assembly.GetType("Hardlight.UISingular", true), new object[] { parent });
        private static int RefCount(UIContainerIdentifier id) => (int)Own(typeof(UIContainerIdentifier), "m_refCount").GetValue(id);
        private static void SetRefCount(UIContainerIdentifier id, int value) => Own(typeof(UIContainerIdentifier), "m_refCount").SetValue(id, value);
        private static void SetIdentifier(UIContainer container, UIContainerIdentifier id) => Own(typeof(UIContainer), "m_identifier").SetValue(container, id);
        [UnityTest]
        public IEnumerator OriginalSingularAndTransitionManagersRetainDeferredDestroyAndIdentifierRules()
        {
            checks = 0;
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
                Debug.Log("PASS genuine UI manager owned runtime checks=" + checks + "; engine-only;" + "; no authored prefab/catalog/UIManager/App approval.");
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
