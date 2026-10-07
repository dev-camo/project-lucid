using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original Core owner: all eight declarations, no fields or authored ctor.
    public static class GameObjectExtensions
    {
        // 06000277..279: shipped assert-named helpers retain direct Unity calls.
        public static T GetComponentWithAssert<T>(this GameObject thisGameObject) where T : class
        {
            return thisGameObject.GetComponent<T>();
        }

        public static T GetComponentInChildrenWithAssert<T>(this GameObject thisGameObject, bool includeInactive = false) where T : class
        {
            return thisGameObject.GetComponentInChildren<T>(includeInactive);
        }

        public static T GetComponentInParentWithAssert<T>(this GameObject thisGameObject) where T : class
        {
            return thisGameObject.GetComponentInParent<T>();
        }

        // 0600027a: the original implicit Unity object truth test decides whether
        // AddComponent runs. It preserves missing/destroyed Unity wrappers.
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (!component) component = gameObject.AddComponent<T>();
            return component;
        }

        // 0600027b: bool is required; original native delegates to genuine Unity.
        public static void Destroy(this GameObject obj, bool destroyImmediate)
        {
            if (destroyImmediate) UnityEngine.Object.DestroyImmediate(obj);
            else UnityEngine.Object.Destroy(obj);
        }

        // 0600027c: capture transform/count once, traverse children in reverse.
        public static void RemoveAllChildren(this GameObject gameObject, bool destroyImmediate = false)
        {
            Transform transform = gameObject.transform;
            int childCount = transform.childCount;
            for (int index = childCount - 1; index >= 0; --index)
                transform.GetChild(index).gameObject.Destroy(destroyImmediate);
        }

        // 0600027d: snapshot genuine attached components, remove Transform and
        // supplied exclusions, then gather inherited RequireComponent attributes.
        // Unity RequireComponent is genuinely sealed in the shipped metadata;
        // its original native class test skips null and nonmatching attributes.
        // AddUnique is the maintained original IList helper, including null Type
        // entries. The first reverse pass removes components whose exact runtime
        // type is not required, then a second reverse pass removes the remainder.
        public static void RemoveAllComponents(this GameObject gameObject, bool destroyImmediate = false, List<Component> excludeList = null)
        {
            Transform transform = gameObject.transform;
            var components = new List<Component>(gameObject.GetComponents<Component>());
            components.Remove(transform);
            if (excludeList != null)
                foreach (Component excluded in excludeList) components.Remove(excluded);

            var requiredTypes = new List<Type>();
            foreach (Component component in components)
            {
                object[] attributes = component.GetType().GetCustomAttributes(true);
                foreach (object attribute in attributes)
                {
                    if (attribute is RequireComponent required)
                    {
                        requiredTypes.AddUnique(required.m_Type0);
                        requiredTypes.AddUnique(required.m_Type1);
                        requiredTypes.AddUnique(required.m_Type2);
                    }
                }
            }

            for (int index = components.Count - 1; index >= 0; --index)
            {
                Component component = components[index];
                if (requiredTypes.Contains(component.GetType())) continue;
                component.Destroy(destroyImmediate);
                components.RemoveAt(index);
            }
            for (int index = components.Count - 1; index >= 0; --index)
            {
                components[index].Destroy(destroyImmediate);
                components.RemoveAt(index);
            }
        }

        // 0600027e: original string literal resolved from shipped metadata as
        // "/". Parent is queried for the Unity inequality test, then queried
        // again for the actual traversal; no inferred root/null fallback.
        public static string GetHierarchyPath(this GameObject gameObject)
        {
            string path = gameObject.name;
            Transform transform = gameObject.transform;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.gameObject.name + "/" + path;
            }
            return path;
        }
    }
}
