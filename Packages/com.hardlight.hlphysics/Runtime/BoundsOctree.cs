using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original HLPhysics.Runtime loose octree. Growth, warning text and append-only
    // queries retain the shipped behavior, including the bounded failed-add mutations.
    public class BoundsOctree<T>
    {
        public int Count { get; private set; }
        private BoundsOctreeNode<T> m_rootNode;
        private readonly float m_looseness;
        private readonly float m_initialSize;
        private readonly float m_minSize;

        public BoundsOctree(float initialWorldSize, Vector3 initialWorldPos, float minNodeSize, float loosenessVal)
        {
            if (minNodeSize > initialWorldSize)
            {
                Debug.LogWarning("Minimum node size must be at least as big as the initial world size. Was: " +
                    minNodeSize.ToString() + " Adjusted to: " + initialWorldSize.ToString());
                minNodeSize = initialWorldSize;
            }
            Count = 0;
            m_initialSize = initialWorldSize;
            m_minSize = minNodeSize;
            m_looseness = Mathf.Clamp(loosenessVal, 1f, 2f);
            m_rootNode = new BoundsOctreeNode<T>(m_initialSize, m_minSize, m_looseness, initialWorldPos);
        }

        public void Add(T obj, Bounds objBounds)
        {
            int count = 0;
            while (!m_rootNode.Add(obj, objBounds))
            {
                Grow(objBounds.center - m_rootNode.Center);
                if (++count > 20)
                {
                    Debug.LogError("Aborted Add operation as it seemed to be going on forever (" +
                        20.ToString() + ") attempts at growing the octree.");
                    return;
                }
            }
            Count++;
        }

        public void GetColliding(List<T> collidingWith, Bounds checkBounds)
        {
            m_rootNode.GetColliding(ref checkBounds, collidingWith);
        }

        public void GetWithinFrustum(Plane[] planes, List<T> result)
        {
            m_rootNode.GetWithinFrustum(planes, result);
        }

        private void Grow(Vector3 direction)
        {
            int xDirection = direction.x >= 0f ? 1 : -1;
            int yDirection = direction.y >= 0f ? 1 : -1;
            int zDirection = direction.z >= 0f ? 1 : -1;
            BoundsOctreeNode<T> oldRoot = m_rootNode;
            float oldLength = oldRoot.BaseLength;
            float half = oldLength * 0.5f;
            float newLength = oldLength + oldLength;
            Vector3 newCenter = oldRoot.Center + new Vector3(xDirection * half, yDirection * half, zDirection * half);
            m_rootNode = new BoundsOctreeNode<T>(newLength, m_minSize, m_looseness, newCenter);
            if (oldRoot.HasAnyObjects())
            {
                int rootPos = m_rootNode.BestFitChild(oldRoot.Center);
                BoundsOctreeNode<T>[] children = new BoundsOctreeNode<T>[8];
                for (int i = 0; i < 8; i++)
                {
                    if (i == rootPos)
                    {
                        children[i] = oldRoot;
                    }
                    else
                    {
                        int x = i % 2 == 0 ? -1 : 1;
                        int y = i > 3 ? -1 : 1;
                        int z = i < 2 || i > 3 && i < 6 ? -1 : 1;
                        Vector3 center = newCenter + new Vector3(x * half, y * half, z * half);
                        children[i] = new BoundsOctreeNode<T>(oldRoot.BaseLength, m_minSize, m_looseness, center);
                    }
                }
                m_rootNode.SetChildren(children);
            }
        }
    }
}
