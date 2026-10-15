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
    // Owned inactive parents, a real UIContainerSimple template and owned
    // identifiers. Managers are built with their genuine constructors; no
    // UIManager, App, Addressables handle or authored content is involved.
    public sealed class OriginalUIContainerManagerGapTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static UIContainerManager Manager(string name, Transform parent) =>
            (UIContainerManager)Activator.CreateInstance(typeof(UIContainerManager).Assembly.GetType("Hardlight." + name, true),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { parent }, null);

        // Stands in for the authored serialized identifier reference.
        private static void SetIdentifier(UIContainer container, UIContainerIdentifier id) =>
            typeof(UIContainer).GetField("m_identifier", Own).SetValue(container, id);

        private static UIContainerIdentifier Identifier(List<UObject> owned, string guid)
        {
            var id = ScriptableObject.CreateInstance<UIContainerIdentifier>();
            owned.Add(id);
            typeof(ScriptableObjectWithGuid).GetField("m_guid", Own).SetValue(id, guid);
            return id;
        }

        private static UIContainer Template(List<UObject> owned, UIContainerIdentifier id)
        {
            var owner = new GameObject("Lucid owned container template");
            owned.Add(owner);
            owner.SetActive(false);
            UIContainer template = owner.AddComponent<UIContainerSimple>();
            SetIdentifier(template, id);
            return template;
        }

        private static GameObject Parent(List<UObject> owned)
        {
            var parent = new GameObject("Lucid owned manager parent");
            owned.Add(parent);
            parent.SetActive(false);
            return parent;
        }

        // UISingular.CloseAllExcept 0x0600014d (x86-64 0x1a463d0) tail-calls the
        // virtual CloseAll slot (+0x188); CloseAll 0x06000148 (0x1a460c0) runs
        // Destroy, then field.Identifier.Close (0x1a4614f), and only then stores
        // null. UITransitionContainerManager.CloseAllExcept 0x0600015f
        // (0x1a471a0) instead tail-calls virtual Close (+0x198) with the field's
        // identifier, and Close 0x06000159 clears the field before closing. With
        // a live container whose identifier is null both routes fault on the
        // release, but UISingular keeps its field and the transition manager has
        // already cleared it. Routing UISingular through Close (or the
        // transition manager through CloseAll) swaps the two outcomes.
        [UnityTest]
        public IEnumerator SingularCloseAllExceptFaultKeepsFieldWhileTransitionCloseClearsIt()
        {
            var owned = new List<UObject>();
            var clones = new List<GameObject>();
            try
            {
                UIContainerIdentifier id = Identifier(owned, "lucid-owned-gap-close-route");
                UIContainer template = Template(owned, id);
                Transform parent = Parent(owned).transform;
                var expectRetained = new Dictionary<string, bool> { { "UISingular", true }, { "UITransitionContainerManager", false } };
                var created = new Dictionary<string, UIContainer>();
                foreach (KeyValuePair<string, bool> row in expectRetained)
                {
                    UIContainerManager manager = Manager(row.Key, parent);
                    UIContainer container = manager.OpenContainer(template);
                    clones.Add(container.gameObject);
                    created.Add(row.Key, container);
                    SetIdentifier(container, null);
                    Assert.That(manager.IsOpen(null), Is.True, row.Key + " live field matches the null identifier");

                    Assert.Throws<NullReferenceException>(() => manager.CloseAllExcept(Array.Empty<UIContainerIdentifier>()), row.Key);
                    UIContainer found;
                    Assert.That(manager.IsOpen(null), Is.EqualTo(row.Value), row.Key + " field after the faulting release");
                    Assert.That(manager.TryGet(null, out found), Is.EqualTo(row.Value), row.Key);
                    Assert.That(ReferenceEquals(found, row.Value ? container : null), Is.True, row.Key);
                    // Both routes scheduled Destroy before the faulting release.
                    Assert.That(container != null, Is.True, row.Key + " destruction stays deferred within the frame");
                }
                yield return null;
                foreach (KeyValuePair<string, UIContainer> row in created)
                    Assert.That(row.Value == null, Is.True, row.Key + " Destroy preceded the fault");
            }
            finally
            {
                for (int i = clones.Count - 1; i >= 0; --i)
                    if (clones[i] != null) UObject.DestroyImmediate(clones[i]);
                for (int i = owned.Count - 1; i >= 0; --i)
                    if (owned[i] != null) UObject.DestroyImmediate(owned[i]);
            }
        }

        // UIDebug.OpenContainer 0x060000d2 (x86-64 0x1a40690) and UIFree.OpenContainer
        // 0x060000db (0x1a40d00) return the virtual TryGet result (+0x1d8) as soon
        // as it is true, with no Unity null test of the cached value, so a clone
        // destroyed outside the manager is returned again as the same destroyed
        // wrapper and no replacement is instantiated. A live-object guard before
        // returning would instantiate a second clone under the parent.
        [UnityTest]
        public IEnumerator DebugAndFreeReturnExternallyDestroyedCachedContainer()
        {
            var owned = new List<UObject>();
            var clones = new List<GameObject>();
            try
            {
                UIContainerIdentifier id = Identifier(owned, "lucid-owned-gap-destroyed-cache");
                UIContainer template = Template(owned, id);
                foreach (string name in new[] { "UIDebug", "UIFree" })
                {
                    Transform parent = Parent(owned).transform;
                    UIContainerManager manager = Manager(name, parent);
                    UIContainer first = manager.OpenContainer(template);
                    clones.Add(first.gameObject);
                    Assert.That(parent.childCount, Is.EqualTo(1), name);

                    UObject.Destroy(first.gameObject);
                    yield return null;
                    Assert.That(first == null, Is.True, name + " clone destroyed outside the manager");
                    Assert.That(parent.childCount, Is.Zero, name);

                    UIContainer again = manager.OpenContainer(template);
                    Assert.That(ReferenceEquals(again, first), Is.True, name + " returns the cached wrapper itself");
                    Assert.That(again == null, Is.True, name + " cached wrapper is still Unity-null");
                    Assert.That(parent.childCount, Is.Zero, name + " no replacement clone was instantiated");
                    Assert.That(manager.IsAnyOpen(), Is.True, name + " stored entry count is unchanged");
                }
            }
            finally
            {
                for (int i = clones.Count - 1; i >= 0; --i)
                    if (clones[i] != null) UObject.DestroyImmediate(clones[i]);
                for (int i = owned.Count - 1; i >= 0; --i)
                    if (owned[i] != null) UObject.DestroyImmediate(owned[i]);
            }
        }
    }
}
