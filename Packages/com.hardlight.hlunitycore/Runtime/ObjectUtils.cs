using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime 020002ca (06001216..06001238), nested <>c
    // 020002cb (06001239..0600123b); bodies from the 1.10.1 x86-64 binary. Generic
    // bodies shipped only as shared instantiations; their method RGCTX tables fix
    // the Unity generic callees named in the comments.
    public class ObjectUtils
    {
        // 0x06001216 (0x984ae0): with includeDisabled, the first match in loaded
        // scene roots via GetComponentInChildren<T>(true); otherwise
        // Object.FindObjectOfType(typeof(T)) as T.
        public static T FindObjectOfType<T>(bool includeDisabled = false) where T : Component
        {
            if (includeDisabled)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }
                    foreach (GameObject rootGameObject in scene.GetRootGameObjects())
                    {
                        T component = rootGameObject.GetComponentInChildren<T>(true);
                        if (component != null)
                        {
                            return component;
                        }
                    }
                }
                return null;
            }
            return Object.FindObjectOfType(typeof(T)) as T;
        }

        // 0x06001217 (0x984d20).
        public static T[] FindObjectsOfType<T>(bool includeDisabled = false) where T : Component
        {
            if (includeDisabled)
            {
                List<T> components = new List<T>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }
                    foreach (GameObject rootGameObject in scene.GetRootGameObjects())
                    {
                        components.AddRange(rootGameObject.GetComponentsInChildren<T>(true));
                    }
                }
                return components.ToArray();
            }
            return Object.FindObjectsOfType(typeof(T)) as T[];
        }

        // 0x06001218 (0x985000).
        public static T[] FindObjectsOfTypeAll<T>() where T : Behaviour
        {
            return Resources.FindObjectsOfTypeAll(typeof(T)) as T[];
        }

        // 0x06001219 (0x985090): the roots are read before the list is created.
        public static List<T> GetAllObjectsOfTypeFromScene<T>(Scene scene, bool includeInactive = false)
        {
            GameObject[] rootGameObjects = scene.GetRootGameObjects();
            List<T> objects = new List<T>();
            foreach (GameObject rootGameObject in rootGameObjects)
            {
                objects.AddRange(rootGameObject.GetComponentsInChildren<T>(includeInactive));
            }
            return objects;
        }

        // 0x0600121a (0x985180): the first of GetComponentsInChildren<T>(true),
        // so the source itself is included.
        public static T GetComponentInChildren<T>(GameObject source) where T : Component
        {
            T[] components = source.GetComponentsInChildren<T>(true);
            if (components != null && components.Length > 0)
            {
                return components[0];
            }
            return null;
        }

        // 0x0600121b (0x985480): direct children only; truthy components are kept.
        public static T[] GetComponentsInChildrenNonRecursive<T>(GameObject source, bool includeInactive) where T : MonoBehaviour
        {
            List<T> components = new List<T>();
            for (int i = 0; i < source.transform.childCount; i++)
            {
                GameObject child = source.transform.GetChild(i).gameObject;
                if (!includeInactive && !child.activeSelf)
                {
                    continue;
                }
                T component = child.GetComponent<T>();
                if (component)
                {
                    components.Add(component);
                }
            }
            return components.ToArray();
        }

        // 0x0600121c (0x9851f0): ancestors only, nearest first.
        public static T GetComponentInParent<T>(GameObject source) where T : Component
        {
            Transform parent = source.transform.parent;
            if (parent == null)
            {
                return null;
            }

            T component;
            do
            {
                component = parent.GetComponent<T>();
                parent = parent.parent;
            }
            while (parent != null && component == null);
            return component;
        }

        // 0x0600121d (0x985300): GetComponentInChildren<T> is inlined.
        public static T GetComponentInSelfOrChildren<T>(GameObject source) where T : Component
        {
            T component = source.GetComponent<T>();
            if (component == null)
            {
                component = GetComponentInChildren<T>(source);
            }
            return component;
        }

        // 0x0600121e (0x9853e0).
        public static T GetComponentInSelfOrParent<T>(GameObject source) where T : Component
        {
            T component = source.GetComponent<T>();
            if (component == null)
            {
                return GetComponentInParent<T>(source);
            }
            return component;
        }

        // 0x0600121f (0x984960).
        public static T FindComponentInTree<T>(Component source) where T : Component
        {
            return FindComponentInTree<T>(source.gameObject);
        }

        // 0x06001220 (0x9849b0): self and children, then each ancestor. A miss
        // returns the last lookup result rather than a fresh null.
        public static T FindComponentInTree<T>(GameObject source) where T : Component
        {
            T component = GetComponentInSelfOrChildren<T>(source);
            if (component != null)
            {
                return component;
            }

            Transform parent = source.transform.parent;
            while (parent != null)
            {
                component = parent.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
                parent = parent.transform.parent;
            }
            return component;
        }

        // 0x06001221 (0x9844c0): only colliders on a RectTransform are considered.
        public static T FindComponentInRaycastHits<T>(IEnumerable<RaycastHit2D> raycastHits) where T : Component
        {
            foreach (RaycastHit2D raycastHit in raycastHits)
            {
                if (raycastHit.collider == null)
                {
                    continue;
                }
                RectTransform rectTransform = raycastHit.collider.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }
                T component = rectTransform.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
            return null;
        }

        // 0x06001222 (0x1b355d0): Object.Instantiate<GameObject>(original, parent,
        // worldPositionStays: false) [0x06000FA9].
        public static GameObject SpawnPrefab(GameObject prefab, Transform parent)
        {
            return Object.Instantiate(prefab, parent, false);
        }

        // 0x06001223 (0x985680): method RGCTX [1] is Instantiate<T>(original, parent,
        // worldPositionStays: false) [0x06000FA9].
        public static T SpawnPrefab<T>(T prefab, Transform parent) where T : Component
        {
            return Object.Instantiate(prefab, parent, false);
        }

        // 0x06001224 (0x1b35640).
        public static GameObject SpawnPrefab(GameObject prefab, Vector3 location, Quaternion rotation, Transform parent)
        {
            return Object.Instantiate(prefab, location, rotation, parent);
        }

        // 0x06001225 (0x985710).
        public static T SpawnPrefab<T>(T prefab, Vector3 location, Quaternion rotation, Transform parent) where T : MonoBehaviour
        {
            return Object.Instantiate(prefab, location, rotation, parent);
        }

        // 0x06001226 (0x1b35700): ordinal name match over all transforms,
        // inactive ones and the source included.
        public static GameObject FindInChildren(GameObject source, string name)
        {
            foreach (Transform child in source.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        // 0x06001227 (0x1b357b0).
        public static GameObject FindTagInChildren(GameObject source, string tag)
        {
            foreach (Transform child in source.GetComponentsInChildren<Transform>(true))
            {
                if (child.CompareTag(tag))
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        // 0x06001228 (0x1b35860): tail call to GameObjectExtensions 0x0600027e.
        [Obsolete("GetGameObjectPath is obsolete, please use: GameObjectExtensions.GetHierarchyPath")]
        public static string GetGameObjectPath(GameObject gameObject)
        {
            return gameObject.GetHierarchyPath();
        }

        // 0x06001229 (0x984340): true only when the component had to be added.
        public static bool EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            bool hasComponent = gameObject.GetComponent<T>() != null;
            if (!hasComponent)
            {
                gameObject.AddComponent<T>();
            }
            return !hasComponent;
        }

        // 0x0600122a (0x1b35870): a null or unknown layer name does nothing.
        public static void SetLayerRecursively(GameObject gameObject, string layerName)
        {
            if (layerName == null)
            {
                return;
            }
            int layerId = LayerMask.NameToLayer(layerName);
            if (layerId < 0 || layerId > 31)
            {
                return;
            }
            SetLayerRecursively(gameObject, layerId);
        }

        // 0x0600122b (0x1b358b0): the Unity null tests are written null-first.
        public static void SetLayerRecursively(GameObject gameObject, int layerId)
        {
            if (null == gameObject)
            {
                return;
            }

            gameObject.layer = layerId;
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                if (null == child)
                {
                    continue;
                }
                SetLayerRecursively(child.gameObject, layerId);
            }
        }

        // 0x0600122c (0x1b359d0): the child count is read once.
        public static void SetChildrenActive(GameObject gameObject, bool active)
        {
            int childCount = gameObject.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                gameObject.transform.GetChild(i).gameObject.SetActive(active);
            }
        }

        // 0x0600122d (0x1b35a70): deferred Object.Destroy, last child first.
        public static void DestroyChildren(GameObject gameObject)
        {
            for (int i = gameObject.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(gameObject.transform.GetChild(i).gameObject);
            }
        }

        // 0x0600122e (0x1b35b40), comparison <>c.<SortChildrenByName>b__24_0
        // 0x0600123b (0x1b367a0): String.CompareOrdinal of the names.
        public static void SortChildrenByName(Transform container)
        {
            List<Transform> children = new List<Transform>();
            foreach (Transform child in container)
            {
                children.Add(child);
            }
            children.Sort((item1, item2) => string.CompareOrdinal(item1.name, item2.name));
            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetSiblingIndex(i);
            }
        }

        // 0x0600122f (0x1b36030): build index across scenes, else hierarchy order.
        public static int GameObjectsHierarchySort(GameObject first, GameObject second)
        {
            if (first.scene != second.scene)
            {
                return first.scene.buildIndex.CompareTo(second.scene.buildIndex);
            }
            return GetGlobalHierarchyIndex(first.transform).CompareTo(GetGlobalHierarchyIndex(second.transform));
        }

        // 0x06001230 (0x1b36100): parent index plus this sibling's fraction scaled
        // by a float power of ten; GetDepthInHierarchy is inlined.
        private static double GetGlobalHierarchyIndex(Transform transform)
        {
            if (transform.parent == null)
            {
                return transform.GetSiblingIndex();
            }

            Transform parent = transform.parent;
            int siblingIndex = transform.GetSiblingIndex();
            int siblingCount = parent.childCount;
            int depth = GetDepthInHierarchy(transform) - 1;
            float depthScale = Mathf.Pow(10, depth);
            return GetGlobalHierarchyIndex(parent) + (siblingIndex + 1) / (double)(siblingCount + 1) / depthScale;
        }

        // 0x06001231 (0x1b36290): the number of ancestors. The native counting loop
        // tests with op_Equality [0x06000fcc] (not op_Inequality) and jumps back
        // through the method-entry s_Il2CppMethodInitialized check at 0x1b36301 on
        // every level, the shape of this self call after clang's tail-recursion
        // elimination; a source loop would not re-enter the entry check.
        private static int GetDepthInHierarchy(Transform transform)
        {
            if (transform.parent == null)
            {
                return 0;
            }
            return GetDepthInHierarchy(transform.parent) + 1;
        }

        // 0x06001232 (0x1b36320): only children that have children are descended.
        public static void ActionForEachChildAtDepth(Transform parent, Action<GameObject> callback, int targetDepth, bool ignoreIfParentInactive = true, int currentDepth = 0)
        {
            if (currentDepth == targetDepth)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    callback(parent.GetChild(i).gameObject);
                }
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.childCount > 0 && (!ignoreIfParentInactive || child.gameObject.activeSelf))
                {
                    ActionForEachChildAtDepth(child, callback, targetDepth, ignoreIfParentInactive, currentDepth + 1);
                }
            }
        }

        // 0x06001233 (0x1b364f0): post-order, each subtree before its root.
        public static void GetAllChildren_LeafFirst(Transform parent, List<Transform> transformList)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.childCount > 0)
                {
                    GetAllChildren_LeafFirst(child, transformList);
                }
                transformList.Add(child);
            }
        }

        // 0x06001234 (0x1b36630): shadows and probes off, in this setter order.
        public static void AddMeshFilterAndRenderer(GameObject parent, Mesh mesh, Material mat, int sortOrder)
        {
            parent.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = parent.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = mat;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.sortingOrder = sortOrder;
        }

        // 0x06001235 (0x9840c0): declared instance members only (flags 0x36).
        // Writable properties first, each copy in a bare catch; then all fields.
        // LSDA 0x2e1239c: only 0x9841bc..0x9841e7 (GetValue, SetValue) lands on the
        // catch pad 0x9841e9, so the CanWrite call at 0x9841a8 is outside the try.
        public static T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            FieldInfo[] fields = type.GetFields(bindingFlags);
            PropertyInfo[] properties = type.GetProperties(bindingFlags);
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanWrite)
                {
                    continue;
                }
                try
                {
                    property.SetValue(copy, property.GetValue(original, null), null);
                }
                catch
                {
                }
            }
            foreach (FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        // 0x06001236 (0x9843e0).
        public static T FindByName<T>(string name) where T : Object
        {
            return FindByNameSafe<T>(name);
        }

        // 0x06001237 (0x984420): null or empty names skip the engine query. First
        // ordinal name match in Resources.FindObjectsOfTypeAll<T> order; a null
        // element throws.
        public static T FindByNameSafe<T>(string name) where T : Object
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            foreach (T current in objects)
            {
                if (current.name == name)
                {
                    return current;
                }
            }
            return null;
        }
    }
}
