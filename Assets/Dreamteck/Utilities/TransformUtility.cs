using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dreamteck
{
    public static class TransformUtility
    {
        public static Vector3 GetPosition(Matrix4x4 m)
        {
            return m.GetColumn(3);
        }

        public static Quaternion GetRotation(Matrix4x4 m)
        {
            return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        }

        public static Vector3 GetScale(Matrix4x4 m)
        {
            return new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
        }

        public static void SetPosition(ref Matrix4x4 m, ref Vector3 p)
        {
            m.SetColumn(3, new Vector4(p.x, p.y, p.z, 1f));
        }

        public static void GetChildCount(Transform parent, ref int count)
        {
            foreach (Transform child in parent)
            {
                count++;
                GetChildCount(child, ref count);
            }
        }

        public static void MergeBoundsRecursively(this Transform rootParent, Transform tr, ref Bounds bounds, string nameToIgnore = null)
        {
            foreach (Transform child in tr)
            {
                if (!string.IsNullOrEmpty(nameToIgnore) && child.name == nameToIgnore)
                {
                    continue;
                }

                rootParent.MergeBoundsRecursively(child, ref bounds);

                var meshFilter = child.GetComponent<MeshFilter>();

                if (meshFilter == null) continue;
                if (meshFilter.sharedMesh == null)
                {
                    Debug.LogError("MESH FILTER " + meshFilter.name + " IS MISSING A MESH");
                    continue;
                }
                var min = child.TransformPoint(meshFilter.sharedMesh.bounds.min);
                var max = child.TransformPoint(meshFilter.sharedMesh.bounds.max);

                bounds.Encapsulate(rootParent.InverseTransformPoint(min));
                bounds.Encapsulate(rootParent.InverseTransformPoint(max));
            }
        }

        public static bool IsParent(Transform child, Transform parent)
        {
            Transform current = child;
            while(current.parent != null)
            {
                current = current.parent;
                if (current == parent) return true;
            }
            return false;
        }
    }
}
