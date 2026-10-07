using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class OriginalObjectExtensionsVerification
    {
        private sealed class Checks
        {
            internal int Count;
            internal void Require(bool condition, string message)
            {
                ++Count;
                if (!condition) throw new InvalidOperationException(message);
            }
        }

        private static GameObject Create(string name)
        {
            return new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static void Cleanup(params GameObject[] objects)
        {
            foreach (GameObject obj in objects)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        }

        // Reflection only: this group can run on a genuine managed host. The
        // remaining groups require actual Unity object/physics implementations.
        public static int RunOriginalCompleteDeclarations()
        {
            var checks = new Checks();
            foreach (Type owner in new[] { typeof(ComponentExtensions), typeof(GameObjectExtensions) })
            {
                checks.Require(owner.IsPublic && owner.IsAbstract && owner.IsSealed, "Original public static owner flags.");
                checks.Require(owner.IsDefined(typeof(ExtensionAttribute), false), "Original owner extension attribute.");
                checks.Require(owner.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Length == 0, "Original fieldless owner.");
                checks.Require(owner.TypeInitializer == null, "No invented static constructor.");
                MethodInfo[] methods = owner.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static);
                checks.Require(methods.Length == (owner == typeof(ComponentExtensions) ? 4 : 8), "Complete original owner API count.");
                foreach (MethodInfo method in methods)
                {
                    checks.Require(method.IsDefined(typeof(ExtensionAttribute), false), "Original ordinary extension declaration.");
                    checks.Require(method.GetMethodBody() != null, "Actual emitted whole body.");
                }
                foreach (MethodInfo method in methods.Where(m => m.Name.StartsWith("GetComponent", StringComparison.Ordinal)))
                {
                    checks.Require(method.GetGenericArguments().Single().GenericParameterAttributes == GenericParameterAttributes.ReferenceTypeConstraint, "Original accessor class constraint.");
                    checks.Require(method.GetGenericArguments()[0].GetGenericParameterConstraints().Length == 0, "No invented Component constraint on assert accessors.");
                }
                MethodInfo children = methods.Single(m => m.Name == "GetComponentInChildrenWithAssert");
                checks.Require(children.GetParameters()[1].IsOptional && Equals(children.GetParameters()[1].DefaultValue, false), "Original false includeInactive default.");
                MethodInfo destroy = methods.Single(m => m.Name == "Destroy");
                checks.Require(!destroy.GetParameters()[1].IsOptional && !destroy.GetParameters()[1].HasDefaultValue, "Destroy bool stays required.");
            }
            MethodInfo getOrAdd = typeof(GameObjectExtensions).GetMethod("GetOrAddComponent");
            checks.Require(getOrAdd.GetGenericArguments()[0].GenericParameterAttributes == GenericParameterAttributes.None, "Original GetOrAdd generic flags.");
            checks.Require(getOrAdd.GetGenericArguments()[0].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(Component) }), "Original Component generic bound.");
            ParameterInfo[] remove = typeof(GameObjectExtensions).GetMethod("RemoveAllComponents").GetParameters();
            checks.Require(remove[1].IsOptional && Equals(remove[1].DefaultValue, false), "Original remove-components false default.");
            checks.Require(remove[2].IsOptional && remove[2].HasDefaultValue && remove[2].DefaultValue == null, "Original null exclusion default.");
            checks.Require(typeof(GameObjectExtensions).GetMethod("RemoveAllChildren").GetParameters()[1].IsOptional, "Original optional child-removal flag.");
            return checks.Count;
        }

        public static int RunOriginalComponentQueries()
        {
            var checks = new Checks();
            GameObject root = Create("OriginalComponentRoot"), child = Create("OriginalComponentChild");
            try
            {
                child.transform.SetParent(root.transform);
                BoxCollider parentCollider = root.AddComponent<BoxCollider>();
                CapsuleCollider childCollider = child.AddComponent<CapsuleCollider>();
                checks.Require(ReferenceEquals(root.transform.GetComponentWithAssert<Transform>(), root.transform), "Component accessor returns genuine cached Transform.");
                checks.Require(ReferenceEquals(root.transform.GetComponentWithAssert<BoxCollider>(), parentCollider), "Component accessor returns attached collider.");
                checks.Require(root.transform.GetComponentWithAssert<Rigidbody>() == null, "Shipped assert accessor retains missing Unity object.");
                checks.Require(ReferenceEquals(root.transform.GetComponentInChildrenWithAssert<CapsuleCollider>(), childCollider), "Active child query returns original component.");
                child.SetActive(false);
                checks.Require(root.transform.GetComponentInChildrenWithAssert<CapsuleCollider>() == null, "Original default excludes inactive child.");
                checks.Require(ReferenceEquals(root.transform.GetComponentInChildrenWithAssert<CapsuleCollider>(true), childCollider), "Explicit includeInactive preserves original query.");
                checks.Require(ReferenceEquals(child.transform.GetComponentInParentWithAssert<BoxCollider>(), parentCollider), "Parent query follows actual hierarchy.");
            }
            finally { Cleanup(root, child); }
            return checks.Count;
        }

        public static int RunOriginalGameObjectQueriesAndCreation()
        {
            var checks = new Checks();
            GameObject root = Create("OriginalGameObjectRoot"), child = Create("OriginalGameObjectChild");
            try
            {
                child.transform.SetParent(root.transform);
                BoxCollider parentCollider = root.AddComponent<BoxCollider>();
                CapsuleCollider childCollider = child.AddComponent<CapsuleCollider>();
                checks.Require(ReferenceEquals(root.GetComponentWithAssert<Transform>(), root.transform), "GameObject accessor returns actual Transform.");
                checks.Require(ReferenceEquals(root.GetComponentWithAssert<BoxCollider>(), parentCollider), "GameObject accessor returns existing collider.");
                checks.Require(root.GetComponentWithAssert<Rigidbody>() == null, "Assert-named accessor preserves missing result.");
                checks.Require(ReferenceEquals(root.GetComponentInChildrenWithAssert<CapsuleCollider>(), childCollider), "GameObject active child query.");
                child.SetActive(false);
                checks.Require(root.GetComponentInChildrenWithAssert<CapsuleCollider>() == null, "GameObject child default excludes inactive child.");
                checks.Require(ReferenceEquals(root.GetComponentInChildrenWithAssert<CapsuleCollider>(true), childCollider), "GameObject explicit inactive inclusion.");
                checks.Require(ReferenceEquals(child.GetComponentInParentWithAssert<BoxCollider>(), parentCollider), "GameObject parent query.");
                Rigidbody created = root.GetOrAddComponent<Rigidbody>();
                checks.Require(created != null && ReferenceEquals(created, root.GetComponent<Rigidbody>()), "Missing component is genuinely created.");
                checks.Require(ReferenceEquals(root.GetOrAddComponent<Rigidbody>(), created), "Existing component identity is retained.");
                checks.Require(root.GetComponents<Rigidbody>().Length == 1, "Existing branch does not add a duplicate.");
            }
            finally { Cleanup(root, child); }
            return checks.Count;
        }

        public static int RunOriginalImmediateDestruction()
        {
            var checks = new Checks();
            GameObject root = Create("OriginalDestroyRoot"), child = Create("OriginalDestroyChild");
            try
            {
                child.transform.SetParent(root.transform);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.Destroy(true);
                checks.Require(collider == null && !ReferenceEquals(collider, null), "Original component destruction retains destroyed CLR wrapper.");
                checks.Require(root != null && root.GetComponent<BoxCollider>() == null, "Component destruction preserves GameObject.");
                child.Destroy(true);
                checks.Require(child == null && !ReferenceEquals(child, null), "Original GameObject destruction retains destroyed CLR wrapper.");
                checks.Require(root != null && root.transform.childCount == 0, "GameObject destruction removes the actual child.");
            }
            finally { Cleanup(root, child); }
            return checks.Count;
        }

        public static int RunOriginalReverseChildRemoval()
        {
            var checks = new Checks();
            GameObject root = Create("OriginalRemoveRoot"), first = Create("OriginalRemoveFirst"), second = Create("OriginalRemoveSecond"), grandchild = Create("OriginalRemoveGrandchild");
            try
            {
                first.transform.SetParent(root.transform);
                second.transform.SetParent(root.transform);
                grandchild.transform.SetParent(first.transform);
                checks.Require(root.transform.childCount == 2, "Genuine authored child fixture.");
                root.RemoveAllChildren(true);
                checks.Require(root != null && root.transform.childCount == 0, "All immediate children removed.");
                checks.Require(first == null && second == null && grandchild == null, "Child destruction removes real descendants.");
                root.RemoveAllChildren(true);
                checks.Require(root != null && root.transform.childCount == 0, "Empty hierarchy remains valid.");
            }
            finally { Cleanup(root, first, second, grandchild); }
            return checks.Count;
        }

        public static int RunOriginalRequiredComponentRemoval()
        {
            var checks = new Checks();
            GameObject root = Create("OriginalRequiredComponentRoot");
            try
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                HingeJoint joint = root.AddComponent<HingeJoint>();
                BoxCollider keep = root.AddComponent<BoxCollider>();
                checks.Require(body != null && joint != null && keep != null, "Genuine Unity physics requirements fixture.");
                root.RemoveAllComponents(true, new List<Component> { keep });
                checks.Require(root.transform != null, "Original removal preserves Transform.");
                checks.Require(ReferenceEquals(root.GetComponent<BoxCollider>(), keep), "Original exclusion retains the exact component.");
                checks.Require(joint == null && body == null, "Dependents removed before required Rigidbody.");
                checks.Require(root.GetComponents<Component>().Length == 2, "Only genuine Transform and excluded collider remain.");
                root.RemoveAllComponents(true);
                checks.Require(keep == null && root.GetComponents<Component>().Length == 1, "Second removal clears remaining component and retains Transform.");
            }
            finally { Cleanup(root); }
            return checks.Count;
        }

        public static int RunOriginalHierarchyPath()
        {
            var checks = new Checks();
            GameObject root = Create("Alpha"), child = Create("Beta/name"), grandchild = Create("Gamma");
            try
            {
                child.transform.SetParent(root.transform);
                grandchild.transform.SetParent(child.transform);
                checks.Require(root.GetHierarchyPath() == "Alpha", "Root path is its actual name.");
                checks.Require(child.GetHierarchyPath() == "Alpha/Beta/name", "Original separator leaves slash-containing names unchanged.");
                checks.Require(grandchild.GetHierarchyPath() == "Alpha/Beta/name/Gamma", "Full original ancestor order.");
                child.transform.SetParent(null);
                checks.Require(grandchild.GetHierarchyPath() == "Beta/name/Gamma", "Path follows current genuine hierarchy.");
            }
            finally { Cleanup(root, child, grandchild); }
            return checks.Count;
        }
    }
}
