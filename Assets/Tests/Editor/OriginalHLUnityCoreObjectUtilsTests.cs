using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    // Behaviour of the original HLUnityCore.Runtime ObjectUtils (020002ca) against
    // its 1.10.1 x86-64 bodies.
    // Every GameObject and asset is created and destroyed by this fixture, and the
    // probe component's static log is cleared around each case.
    // Recorded EditMode gaps: DestroyChildren 0x0600122d (deferred Object.Destroy
    // needs Play Mode), FindComponentInRaycastHits
    // 0x06001221 (RaycastHit2D colliders need a physics query) and
    // FindObjectsOfTypeAll 0x06001218 (editor-wide Resources scan).
    [TestFixture]
    public sealed class OriginalHLUnityCoreObjectUtilsTests
    {
        private readonly List<GameObject> m_ownedObjects = new List<GameObject>();
        private readonly List<Object> m_ownedAssets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            OriginalHLUnityCoreCopyComponentProbe.Log.Clear();
        }

        private GameObject Create(string name, Transform parent = null)
        {
            GameObject gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }
            else
            {
                m_ownedObjects.Add(gameObject);
            }
            return gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in m_ownedObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
            m_ownedObjects.Clear();
            foreach (Object asset in m_ownedAssets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            m_ownedAssets.Clear();
            OriginalHLUnityCoreCopyComponentProbe.Log.Clear();
        }

        // GetComponentInParent 0x0600121c (0x9851f0) starts at the parent, and
        // GetComponentInChildren 0x0600121a (0x985180) takes the first of
        // GetComponentsInChildren<T>(true), so self and inactive children count.
        [Test]
        public void ParentLookupsSkipSelfWhileChildLookupsIncludeSelfAndInactive()
        {
            GameObject root = Create("Root");
            GameObject middle = Create("Middle", root.transform);
            GameObject leaf = Create("Leaf", middle.transform);
            BoxCollider rootCollider = root.AddComponent<BoxCollider>();
            BoxCollider leafCollider = leaf.AddComponent<BoxCollider>();

            Assert.That(ObjectUtils.GetComponentInParent<BoxCollider>(leaf), Is.SameAs(rootCollider));
            Assert.That(ObjectUtils.GetComponentInParent<BoxCollider>(root), Is.Null);
            Assert.That(ObjectUtils.GetComponentInSelfOrParent<BoxCollider>(leaf), Is.SameAs(leafCollider));
            Assert.That(ObjectUtils.GetComponentInSelfOrParent<BoxCollider>(middle), Is.SameAs(rootCollider));

            middle.SetActive(false);
            Assert.That(ObjectUtils.GetComponentInChildren<BoxCollider>(middle), Is.SameAs(leafCollider));
            Assert.That(ObjectUtils.GetComponentInChildren<BoxCollider>(root), Is.SameAs(rootCollider));
            Assert.That(ObjectUtils.GetComponentInSelfOrChildren<BoxCollider>(middle), Is.SameAs(leafCollider));
            Assert.That(ObjectUtils.GetComponentInChildren<SphereCollider>(root), Is.Null);
        }

        // FindComponentInTree 0x06001220 (0x9849b0): self and children first, then
        // each ancestor; the Component overload 0x0600121f forwards gameObject.
        [Test]
        public void FindComponentInTreeSearchesDownwardsBeforeWalkingUp()
        {
            GameObject root = Create("Root");
            GameObject middle = Create("Middle", root.transform);
            GameObject leaf = Create("Leaf", middle.transform);
            BoxCollider rootCollider = root.AddComponent<BoxCollider>();

            Assert.That(ObjectUtils.FindComponentInTree<BoxCollider>(leaf), Is.SameAs(rootCollider));
            BoxCollider leafCollider = leaf.AddComponent<BoxCollider>();
            Assert.That(ObjectUtils.FindComponentInTree<BoxCollider>(middle), Is.SameAs(leafCollider));
            Assert.That(ObjectUtils.FindComponentInTree<BoxCollider>(middle.transform), Is.SameAs(leafCollider));
            Assert.That(ObjectUtils.FindComponentInTree<SphereCollider>(leaf), Is.Null);
        }

        // GetComponentsInChildrenNonRecursive 0x0600121b (0x985480): direct
        // children only, inactive ones only on request, missing components skipped.
        [Test]
        public void NonRecursiveChildComponentsCoverOnlyDirectChildren()
        {
            GameObject root = Create("Root");
            root.AddComponent<EventTrigger>();
            EventTrigger active = Create("Active", root.transform).AddComponent<EventTrigger>();
            GameObject inactiveObject = Create("Inactive", root.transform);
            EventTrigger inactive = inactiveObject.AddComponent<EventTrigger>();
            inactiveObject.SetActive(false);
            Create("Empty", root.transform);
            Create("Grandchild", active.transform).AddComponent<EventTrigger>();

            Assert.That(ObjectUtils.GetComponentsInChildrenNonRecursive<EventTrigger>(root, false), Is.EqualTo(new[] { active }));
            Assert.That(ObjectUtils.GetComponentsInChildrenNonRecursive<EventTrigger>(root, true), Is.EqualTo(new[] { active, inactive }));
        }

        // FindInChildren 0x06001226 and FindTagInChildren 0x06001227 scan
        // GetComponentsInChildren<Transform>(true), the source first.
        [Test]
        public void ChildNameAndTagSearchesIncludeTheSourceAndInactiveChildren()
        {
            GameObject root = Create("Root");
            GameObject branch = Create("Branch", root.transform);
            GameObject leaf = Create("Leaf", branch.transform);
            branch.SetActive(false);

            Assert.That(ObjectUtils.FindInChildren(root, "Leaf"), Is.SameAs(leaf));
            Assert.That(ObjectUtils.FindInChildren(root, "Root"), Is.SameAs(root));
            Assert.That(ObjectUtils.FindInChildren(root, "leaf"), Is.Null, "names compare ordinally");
            Assert.That(ObjectUtils.FindTagInChildren(root, "Untagged"), Is.SameAs(root));

            // Built-in tags only, so CompareTag never meets an undefined tag.
            leaf.tag = "EditorOnly";
            Assert.That(ObjectUtils.FindTagInChildren(root, "EditorOnly"), Is.SameAs(leaf), "an inactive, non-root child is scanned");
            Assert.That(ObjectUtils.FindTagInChildren(branch, "EditorOnly"), Is.SameAs(leaf));
            Assert.That(ObjectUtils.FindTagInChildren(root, "Player"), Is.Null);
        }

        // EnsureComponent 0x06001229 (0x984340): true only when it adds.
        [Test]
        public void EnsureComponentReportsWhetherItAdded()
        {
            GameObject root = Create("Root");
            Assert.That(ObjectUtils.EnsureComponent<BoxCollider>(root), Is.True);
            Assert.That(ObjectUtils.EnsureComponent<BoxCollider>(root), Is.False);
            Assert.That(root.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
        }

        // SetLayerRecursively 0x0600122a (0x1b35870): a null or unknown name returns
        // before any change; 0x0600122b (0x1b358b0) applies the id to the subtree.
        [Test]
        public void SetLayerRecursivelyIgnoresUnknownNamesAndCoversTheSubtree()
        {
            GameObject root = Create("Root");
            GameObject leaf = Create("Leaf", Create("Middle", root.transform).transform);

            Assert.DoesNotThrow(() => ObjectUtils.SetLayerRecursively(root, (string)null));
            ObjectUtils.SetLayerRecursively(root, "ProjectLucidMissingLayer");
            Assert.That(leaf.layer, Is.EqualTo(0));

            int water = LayerMask.NameToLayer("Water");
            ObjectUtils.SetLayerRecursively(root, "Water");
            Assert.That(root.layer, Is.EqualTo(water));
            Assert.That(leaf.layer, Is.EqualTo(water));
            Assert.DoesNotThrow(() => ObjectUtils.SetLayerRecursively((GameObject)null, water));
        }

        // SetChildrenActive 0x0600122c touches only direct children.
        [Test]
        public void SetChildrenActiveTogglesDirectChildrenOnly()
        {
            GameObject root = Create("Root");
            GameObject child = Create("Child", root.transform);
            GameObject grandchild = Create("Grandchild", child.transform);

            ObjectUtils.SetChildrenActive(root, false);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(child.activeSelf, Is.False);
            Assert.That(grandchild.activeSelf, Is.True);
        }

        // SortChildrenByName 0x0600122e with <>c.<SortChildrenByName>b__24_0
        // 0x0600123b (0x1b367a0): String.CompareOrdinal, so 'B' sorts before 'a'.
        [Test]
        public void SortChildrenByNameUsesOrdinalOrder()
        {
            GameObject root = Create("Root");
            GameObject lowerB = Create("b", root.transform);
            GameObject upperB = Create("B", root.transform);
            GameObject lowerA = Create("a", root.transform);

            ObjectUtils.SortChildrenByName(root.transform);
            Assert.That(upperB.transform.GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(lowerA.transform.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(lowerB.transform.GetSiblingIndex(), Is.EqualTo(2));
        }

        // GetAllChildren_LeafFirst 0x06001233 (0x1b364f0): post-order.
        [Test]
        public void GetAllChildrenLeafFirstListsEachSubtreeBeforeItsRoot()
        {
            GameObject root = Create("Root");
            GameObject a = Create("A", root.transform);
            GameObject a1 = Create("A1", a.transform);
            GameObject a2 = Create("A2", a.transform);
            GameObject b = Create("B", root.transform);

            List<Transform> transforms = new List<Transform>();
            ObjectUtils.GetAllChildren_LeafFirst(root.transform, transforms);
            Assert.That(transforms, Is.EqualTo(new[] { a1.transform, a2.transform, a.transform, b.transform }));
        }

        // ActionForEachChildAtDepth 0x06001232 (0x1b36320): only children that have
        // children are descended, inactive ones skipped unless asked otherwise.
        [Test]
        public void ActionForEachChildAtDepthVisitsTheTargetLevel()
        {
            GameObject root = Create("Root");
            GameObject a = Create("A", root.transform);
            GameObject a1 = Create("A1", a.transform);
            GameObject b = Create("B", root.transform);
            GameObject b1 = Create("B1", b.transform);
            GameObject c = Create("C", root.transform);
            b.SetActive(false);

            List<GameObject> visited = new List<GameObject>();
            ObjectUtils.ActionForEachChildAtDepth(root.transform, visited.Add, 0);
            Assert.That(visited, Is.EqualTo(new[] { a, b, c }));

            visited.Clear();
            ObjectUtils.ActionForEachChildAtDepth(root.transform, visited.Add, 1);
            Assert.That(visited, Is.EqualTo(new[] { a1 }));

            visited.Clear();
            ObjectUtils.ActionForEachChildAtDepth(root.transform, visited.Add, 1, false);
            Assert.That(visited, Is.EqualTo(new[] { a1, b1 }));
        }

        // GameObjectsHierarchySort 0x0600122f with GetGlobalHierarchyIndex 0x06001230
        // (0x1b36100): within one scene a child sorts after its parent and before
        // the parent's next sibling.
        [Test]
        public void HierarchySortOrdersByGlobalHierarchyIndex()
        {
            GameObject root = Create("Root");
            GameObject a = Create("A", root.transform);
            GameObject a1 = Create("A1", a.transform);
            GameObject b = Create("B", root.transform);
            Create("C", root.transform);

            Assert.That(ObjectUtils.GameObjectsHierarchySort(a1, b), Is.EqualTo(-1));
            Assert.That(ObjectUtils.GameObjectsHierarchySort(b, a1), Is.EqualTo(1));
            Assert.That(ObjectUtils.GameObjectsHierarchySort(a1, a), Is.EqualTo(1));
            Assert.That(ObjectUtils.GameObjectsHierarchySort(a, a), Is.EqualTo(0));
        }

        // SpawnPrefab 0x06001222 (0x1b355d0) and 0x06001223 (0x985680) call
        // Instantiate<T>(original, parent, worldPositionStays: false) [0x06000FA9],
        // so the original's position is kept as the local position.
        [Test]
        public void SpawnPrefabInstantiatesUnderTheParent()
        {
            GameObject root = Create("Root");
            root.transform.position = new Vector3(5f, 0f, 0f);
            GameObject original = Create("Original");
            original.transform.position = new Vector3(1f, 0f, 0f);
            GameObject spawned = ObjectUtils.SpawnPrefab(original, root.transform);
            Assert.That(spawned, Is.Not.SameAs(original));
            Assert.That(spawned.transform.parent, Is.SameAs(root.transform));
            Assert.That(spawned.name, Is.EqualTo("Original(Clone)"));
            Assert.That(spawned.transform.localPosition, Is.EqualTo(new Vector3(1f, 0f, 0f)));

            OriginalHLUnityCoreCopyComponentProbe probe = original.AddComponent<OriginalHLUnityCoreCopyComponentProbe>();
            OriginalHLUnityCoreCopyComponentProbe spawnedProbe = ObjectUtils.SpawnPrefab(probe, root.transform);
            Assert.That(spawnedProbe, Is.Not.SameAs(probe));
            Assert.That(spawnedProbe.transform.parent, Is.SameAs(root.transform));
            Assert.That(spawnedProbe.transform.localPosition, Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        // SpawnPrefab 0x06001224 (0x1b35640) and 0x06001225 (0x985710) call
        // Instantiate<T>(original, position, rotation, parent) [0x06000FA7]: the
        // location is a world position.
        [Test]
        public void SpawnPrefabAtLocationUsesWorldSpace()
        {
            GameObject root = Create("Root");
            root.transform.position = new Vector3(5f, 0f, 0f);
            GameObject original = Create("Original");
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject spawned = ObjectUtils.SpawnPrefab(original, new Vector3(1f, 2f, 3f), rotation, root.transform);
            Assert.That(spawned.transform.parent, Is.SameAs(root.transform));
            Assert.That(Vector3.Distance(spawned.transform.position, new Vector3(1f, 2f, 3f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(spawned.transform.rotation, rotation), Is.LessThan(1e-3f));

            OriginalHLUnityCoreCopyComponentProbe probe = original.AddComponent<OriginalHLUnityCoreCopyComponentProbe>();
            OriginalHLUnityCoreCopyComponentProbe spawnedProbe = ObjectUtils.SpawnPrefab(probe, new Vector3(1f, 2f, 3f), rotation, root.transform);
            Assert.That(spawnedProbe, Is.Not.SameAs(probe));
            Assert.That(Vector3.Distance(spawnedProbe.transform.position, new Vector3(1f, 2f, 3f)), Is.LessThan(1e-4f));
        }

        // AddMeshFilterAndRenderer 0x06001234 (0x1b36630): shared mesh and material,
        // shadows and both probe usages off, then the sorting order.
        [Test]
        public void AddMeshFilterAndRendererTurnsShadowsAndProbesOff()
        {
            GameObject root = Create("Root");
            Mesh mesh = new Mesh();
            Material material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            m_ownedAssets.Add(mesh);
            m_ownedAssets.Add(material);

            ObjectUtils.AddMeshFilterAndRenderer(root, mesh, material, 12);
            Assert.That(root.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            MeshRenderer meshRenderer = root.GetComponent<MeshRenderer>();
            Assert.That(meshRenderer.sharedMaterial, Is.SameAs(material));
            Assert.That(meshRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(meshRenderer.receiveShadows, Is.False);
            Assert.That(meshRenderer.lightProbeUsage, Is.EqualTo(LightProbeUsage.Off));
            Assert.That(meshRenderer.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.Off));
            Assert.That(meshRenderer.sortingOrder, Is.EqualTo(12));
        }

        // CopyComponent 0x06001235 (0x9840c0): declared instance properties first,
        // read-only ones skipped before any GetValue, a throwing setter swallowed
        // (LSDA catch pad 0x9841e9); then every declared field, private included.
        [Test]
        public void CopyComponentCopiesWritablePropertiesBeforeFields()
        {
            GameObject source = Create("Source");
            GameObject destination = Create("Destination");
            OriginalHLUnityCoreCopyComponentProbe original = source.AddComponent<OriginalHLUnityCoreCopyComponentProbe>();
            original.m_publicField = 42;
            original.SetPrivateField("secret");
            OriginalHLUnityCoreCopyComponentProbe.Log.Clear();

            OriginalHLUnityCoreCopyComponentProbe copy = ObjectUtils.CopyComponent(original, destination);
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.gameObject, Is.SameAs(destination));
            Assert.That(OriginalHLUnityCoreCopyComponentProbe.Log, Is.EqualTo(new[] { "Recorded=7 field=0" }), "the property runs before the fields are copied");
            Assert.That(copy.m_publicField, Is.EqualTo(42));
            Assert.That(copy.PrivateField, Is.EqualTo("secret"));
        }

        // FindObjectOfType 0x06001216 (0x984ae0), FindObjectsOfType 0x06001217
        // (0x984d20) and GetAllObjectsOfTypeFromScene 0x06001219 (0x985090): only
        // the includeDisabled/includeInactive paths reach inactive objects in the
        // loaded scenes. The probe type exists only on this fixture's objects.
        [Test]
        public void SceneSearchesReachInactiveObjectsOnlyWhenAsked()
        {
            GameObject root = Create("Root");
            GameObject child = Create("Child", root.transform);
            OriginalHLUnityCoreCopyComponentProbe probe = child.AddComponent<OriginalHLUnityCoreCopyComponentProbe>();
            child.SetActive(false);

            Assert.That(ObjectUtils.FindObjectOfType<OriginalHLUnityCoreCopyComponentProbe>(), Is.Null);
            Assert.That(ObjectUtils.FindObjectOfType<OriginalHLUnityCoreCopyComponentProbe>(true), Is.SameAs(probe));
            Assert.That(ObjectUtils.FindObjectsOfType<OriginalHLUnityCoreCopyComponentProbe>(true), Is.EqualTo(new[] { probe }));

            Scene scene = root.scene;
            Assert.That(ObjectUtils.GetAllObjectsOfTypeFromScene<OriginalHLUnityCoreCopyComponentProbe>(scene), Is.Empty);
            Assert.That(ObjectUtils.GetAllObjectsOfTypeFromScene<OriginalHLUnityCoreCopyComponentProbe>(scene, true), Is.EqualTo(new[] { probe }));

            child.SetActive(true);
            Assert.That(ObjectUtils.FindObjectOfType<OriginalHLUnityCoreCopyComponentProbe>(), Is.SameAs(probe));
        }
    }
}
