using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a10: batches positions for a LineRenderer.
    // The buffer is sized when the container is constructed, from the
    // m_positionsCountMax initializer of 32.
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

        // Positions past the maximum are dropped; only an exact match stops
        // the append.
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
