using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a10: batches positions for a LineRenderer.
    // The buffer is allocated in the constructor, before serialization, so it
    // always holds 32 positions; a serialized maximum above 32 indexes past its end.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class LineRendererContainer
    {
        [SerializeField]
        private int m_positionsCountMax = 32;
        [SerializeField]
        private LineRenderer m_lineRenderer;
        private int m_positionsCount;
        private Vector3[] m_positions;

        public LineRendererContainer()
        {
            m_positions = new Vector3[m_positionsCountMax];
        }

        public void SetActive(bool active)
        {
            m_lineRenderer.enabled = active;
        }

        // Appends until the count equals the serialized maximum, then drops further
        // positions. The test is equality, as in the original.
        public void AddPosition(Vector3 position)
        {
            if (m_positionsCount == m_positionsCountMax) return;
            m_positions[m_positionsCount] = position;
            m_positionsCount++;
        }

        // Passes the whole buffer; positionCount limits what the line uses.
        public void Finalise()
        {
            m_lineRenderer.positionCount = m_positionsCount;
            m_lineRenderer.SetPositions(m_positions);
            m_positionsCount = 0;
        }
    }
}
