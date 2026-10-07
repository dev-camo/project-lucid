using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200028d. All11 own methods/one const field.
    // Only the last three original declarations are extension methods.
    public static class TransformUtils
    {
        public const string PathSeparator = "/"; // 04000836

        // 0600100c: position -> rotation -> scale, with actual Unity identity values.
        public static void ResetTransformToIdentity(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        // 0600100d: preserve interleaved source read/destination write ordering.
        public static void CopyLocalTransform(Transform transformTo, Transform transformFrom)
        {
            transformTo.localPosition = transformFrom.localPosition;
            transformTo.localRotation = transformFrom.localRotation;
            transformTo.localScale = transformFrom.localScale;
        }

        // 0600100e: capture all three locals before the original one-arg SetParent.
        public static void ParentAndRetainLocalTransform(Transform child, Transform parent)
        {
            Vector3 position = child.localPosition;
            Quaternion rotation = child.localRotation;
            Vector3 scale = child.localScale;
            child.SetParent(parent);
            child.localPosition = position;
            child.localRotation = rotation;
            child.localScale = scale;
        }

        // 0600100f: Unity Vector3 equality tests the whole approximate zero vector.
        // Walk local scales on the object and its parents; do not test any axis alone.
        public static bool HasZeroScale(GameObject objectToCheck)
        {
            Transform current = objectToCheck.transform;
            while (current != null)
            {
                if (current.localScale == Vector3.zero) return true;
                current = current.parent;
            }
            return false;
        }

        // 06001010: first read rect.min, then a second rect.max. The real child
        // query includes inactive RectTransforms; four rect loads occur per child.
        // Preserve new-value comparison selection and its unordered-value behavior.
        public static Rect CalculateBoundingRect(Transform transform)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                Vector2 min = rectTransform.rect.min;
                Vector2 max = rectTransform.rect.max;
                RectTransform[] children = transform.gameObject.GetComponentsInChildren<RectTransform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    RectTransform child = children[i];
                    float childMinX = child.rect.xMin;
                    min.x = childMinX < min.x ? childMinX : min.x;
                    float childMaxX = child.rect.xMax;
                    max.x = childMaxX > max.x ? childMaxX : max.x;
                    float childMinY = child.rect.yMin;
                    min.y = childMinY < min.y ? childMinY : min.y;
                    float childMaxY = child.rect.yMax;
                    max.y = childMaxY > max.y ? childMaxY : max.y;
                }
                return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }
            return default(Rect);
        }

        // 06001011: missing RectTransform OR missing supplied camera returns zero.
        // Four world corners; project first corner, then traverse the other three.
        public static Rect CalculateScreenRect(Transform transform, Camera camera)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null && camera != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                Vector3 first = camera.WorldToScreenPoint(corners[0]);
                float minX = first.x, maxX = first.x, minY = first.y, maxY = first.y;
                for (int i = 1; i < corners.Length; i++)
                {
                    Vector3 projected = camera.WorldToScreenPoint(corners[i]);
                    minX = projected.x < minX ? projected.x : minX;
                    maxX = projected.x > maxX ? projected.x : maxX;
                    minY = projected.y < minY ? projected.y : minY;
                    maxY = projected.y > maxY ? projected.y : maxY;
                }
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            return default(Rect);
        }

        // 06001012: original constraint Component; count captured after sorting.
        public static void SortImmediateChildrenOfType<T>(Transform transform, Comparison<T> comparison, List<T> tempChildList) where T : Component
        {
            GetImmediateChildrenOfType(transform, tempChildList);
            tempChildList.Sort(comparison);
            int count = tempChildList.Count;
            for (int i = 0; i < count; i++) tempChildList[i].transform.SetSiblingIndex(i);
        }

        // 06001013: unconstrained T. Clear before childCount; child count captured
        // once. The generic component's null check is managed reference equality.
        // The Editor can return a managed wrapper for a missing component that
        // compares equal to Unity null. The original reference check retains it.
        public static void GetImmediateChildrenOfType<T>(Transform transform, List<T> children)
        {
            children.Clear();
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                T component = transform.GetChild(i).GetComponent<T>();
                if (component != null) children.Add(component);
            }
        }

        // 06001014: genuine extension; null child before parent comparison, then
        // separately reload the next ancestor. Native compiler makes a tail loop.
        // Recursion is inferred from the optimized tail loop; deep-stack and
        // native fault equivalence remain unproven. Null-parent behavior is kept.
        public static bool IsParentOf(this Transform parent, Transform child)
        {
            if (child == null) return false;
            if (child.transform.parent == parent) return true;
            return parent.IsParentOf(child.transform.parent);
        }

        // 06001015: recurse before reading parent.name; preserve Concat2 then3.
        public static string GetParentsSceneHierarchy(this Transform transform)
        {
            string hierarchy = string.Empty;
            Transform parent = transform.parent;
            if (parent != null)
            {
                hierarchy = string.Concat(hierarchy, parent.GetParentsSceneHierarchy());
                hierarchy = string.Concat(hierarchy, parent.name, PathSeparator);
            }
            return hierarchy;
        }

        // 06001016: parent hierarchy evaluates before the transform's name getter.
        public static string GetSceneHierarchy(this Transform transform)
        {
            return string.Concat(transform.GetParentsSceneHierarchy(), transform.name);
        }
    }
}
