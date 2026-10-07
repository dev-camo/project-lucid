using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original HLPhysics.Runtime node. Its eight child slots, reverse migration and
    // List.Remove(record) equality behavior are part of the original contract.
    public class BoundsOctreeNode<T>
    {
        public Vector3 Center { get; private set; }
        public float BaseLength { get; private set; }
        private float m_looseness;
        private float m_minSize;
        private float m_adjLength;
        private Bounds m_bounds;
        private readonly List<OctreeObject> m_objects = new List<OctreeObject>();
        private BoundsOctreeNode<T>[] m_children;
        private Bounds[] m_childBounds;
        private const int NUM_OBJECTS_ALLOWED = 8;

        private bool HasChildren { get { return m_children != null; } }

        public BoundsOctreeNode(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal)
        {
            SetValues(baseLengthVal, minSizeVal, loosenessVal, centerVal);
        }

        public bool Add(T obj, Bounds objBounds)
        {
            if (!Encapsulates(m_bounds, objBounds)) return false;
            SubAdd(obj, objBounds);
            return true;
        }

        public void GetColliding(ref Bounds checkBounds, List<T> result)
        {
            if (!m_bounds.Intersects(checkBounds)) return;
            for (int i = 0; i < m_objects.Count; i++)
            {
                if (m_objects[i].Bounds.Intersects(checkBounds)) result.Add(m_objects[i].Obj);
            }
            if (m_children != null)
            {
                for (int i = 0; i < 8; i++) m_children[i].GetColliding(ref checkBounds, result);
            }
        }

        public void GetWithinFrustum(Plane[] planes, List<T> result)
        {
            if (!GeometryUtility.TestPlanesAABB(planes, m_bounds)) return;
            for (int i = 0; i < m_objects.Count; i++)
            {
                if (GeometryUtility.TestPlanesAABB(planes, m_objects[i].Bounds)) result.Add(m_objects[i].Obj);
            }
            if (m_children != null)
            {
                for (int i = 0; i < 8; i++) m_children[i].GetWithinFrustum(planes, result);
            }
        }

        public void SetChildren(BoundsOctreeNode<T>[] childOctrees)
        {
            if (childOctrees.Length != 8)
            {
                Debug.LogError("Child octree array must be length 8. Was length: " + childOctrees.Length.ToString());
                return;
            }
            m_children = childOctrees;
        }

        public int BestFitChild(Vector3 objBoundsCenter)
        {
            // Inverse comparisons intentionally retain the original unordered choices:
            // either NaN operand selects x bit1, y bit4 or z bit2 on both shipped CPUs.
            return (objBoundsCenter.x <= Center.x ? 0 : 1) +
                (objBoundsCenter.y >= Center.y ? 0 : 4) +
                (objBoundsCenter.z <= Center.z ? 0 : 2);
        }

        public bool HasAnyObjects()
        {
            if (m_objects.Count > 0) return true;
            if (m_children != null)
            {
                for (int i = 0; i < 8; i++) if (m_children[i].HasAnyObjects()) return true;
            }
            return false;
        }

        private void SetValues(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal)
        {
            BaseLength = baseLengthVal;
            m_minSize = minSizeVal;
            m_looseness = loosenessVal;
            Center = centerVal;
            m_adjLength = m_looseness * baseLengthVal;
            m_bounds = new Bounds(Center, Vector3.one * m_adjLength);
            m_childBounds = new Bounds[8];
            float quarter = BaseLength * 0.25f;
            float childLength = BaseLength * 0.5f * m_looseness;
            m_childBounds[0] = new Bounds(new Vector3(Center.x - quarter, Center.y + quarter, Center.z - quarter), Vector3.one * childLength);
            m_childBounds[1] = new Bounds(new Vector3(Center.x + quarter, Center.y + quarter, Center.z - quarter), Vector3.one * childLength);
            m_childBounds[2] = new Bounds(new Vector3(Center.x - quarter, Center.y + quarter, Center.z + quarter), Vector3.one * childLength);
            m_childBounds[3] = new Bounds(new Vector3(Center.x + quarter, Center.y + quarter, Center.z + quarter), Vector3.one * childLength);
            m_childBounds[4] = new Bounds(new Vector3(Center.x - quarter, Center.y - quarter, Center.z - quarter), Vector3.one * childLength);
            m_childBounds[5] = new Bounds(new Vector3(Center.x + quarter, Center.y - quarter, Center.z - quarter), Vector3.one * childLength);
            m_childBounds[6] = new Bounds(new Vector3(Center.x - quarter, Center.y - quarter, Center.z + quarter), Vector3.one * childLength);
            m_childBounds[7] = new Bounds(new Vector3(Center.x + quarter, Center.y - quarter, Center.z + quarter), Vector3.one * childLength);
        }

        private void SubAdd(T obj, Bounds objBounds)
        {
            if (!HasChildren)
            {
                if (m_objects.Count < NUM_OBJECTS_ALLOWED || BaseLength * 0.5f < m_minSize)
                {
                    m_objects.Add(new OctreeObject { Obj = obj, Bounds = objBounds });
                    return;
                }
                if (m_children == null) Split();
                if (m_children == null)
                {
                    Debug.LogError("Child creation failed for an unknown reason. Early exit.");
                    return;
                }
                for (int i = m_objects.Count - 1; i >= 0; i--)
                {
                    OctreeObject existingObj = m_objects[i];
                    int bestFit = BestFitChild(existingObj.Bounds.center);
                    if (Encapsulates(m_children[bestFit].m_bounds, existingObj.Bounds))
                    {
                        m_children[bestFit].SubAdd(existingObj.Obj, existingObj.Bounds);
                        m_objects.Remove(existingObj);
                    }
                }
            }
            int child = BestFitChild(objBounds.center);
            if (Encapsulates(m_children[child].m_bounds, objBounds))
            {
                m_children[child].SubAdd(obj, objBounds);
            }
            else
            {
                m_objects.Add(new OctreeObject { Obj = obj, Bounds = objBounds });
            }
        }

        private void Split()
        {
            float quarter = BaseLength * 0.25f;
            float newLength = BaseLength * 0.5f;
            m_children = new BoundsOctreeNode<T>[8];
            m_children[0] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x - quarter, Center.y + quarter, Center.z - quarter));
            m_children[1] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x + quarter, Center.y + quarter, Center.z - quarter));
            m_children[2] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x - quarter, Center.y + quarter, Center.z + quarter));
            m_children[3] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x + quarter, Center.y + quarter, Center.z + quarter));
            m_children[4] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x - quarter, Center.y - quarter, Center.z - quarter));
            m_children[5] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x + quarter, Center.y - quarter, Center.z - quarter));
            m_children[6] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x - quarter, Center.y - quarter, Center.z + quarter));
            m_children[7] = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, new Vector3(Center.x + quarter, Center.y - quarter, Center.z + quarter));
        }

        private static bool Encapsulates(Bounds outerBounds, Bounds innerBounds)
        {
            return outerBounds.Contains(innerBounds.min) && outerBounds.Contains(innerBounds.max);
        }

        private struct OctreeObject
        {
            public T Obj;
            public Bounds Bounds;
        }
    }
}
