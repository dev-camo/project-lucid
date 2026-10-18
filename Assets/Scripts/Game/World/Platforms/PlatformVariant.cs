using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000778: a platform column built from a top piece and
    // repeated copies of a mid piece, stacked to an integer height.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class PlatformVariant : MonoBehaviour
    {
        [SerializeField] private PlatformPiece m_topPiece;
        [SerializeField] private PlatformPiece m_midPiece;
        [HideInInspector] [SerializeField] private List<PlatformPiece> m_midPieceInstances = new List<PlatformPiece>();

        public GameObject VariantInstance => gameObject;

        // 060029f8: mid instance i sits i + 2 piece sizes above the base. Instances from
        // index height - 1 upwards are hidden, not removed.
        public void SetHeight(int height, float basePieceHeight, float pieceSize)
        {
            m_topPiece.SetHeight(basePieceHeight + pieceSize * 0.5f + height * pieceSize);
            m_midPiece.SetActive(height > 0);
            if (m_midPiece.GameObjects.Length == 0)
                return;
            for (int i = 0; i <= height - 2; i++)
            {
                if (m_midPieceInstances.Count == i)
                    m_midPieceInstances.Add(null);
                PlatformPiece instance = m_midPieceInstances[i] ?? new PlatformPiece(m_midPiece);
                instance.SetActive(true);
                instance.SetHeight(basePieceHeight + pieceSize * 2f + i * pieceSize);
                m_midPieceInstances[i] = instance;
            }
            for (int i = m_midPieceInstances.Count - 1; i >= height - 1 && i >= 0; i--)
                m_midPieceInstances[i].SetActive(false);
        }

        // 060029f9: PlatformPiece.Destroy is empty in the retail player, so only the
        // indexer read remains before each removal.
        public void RemoveUnusedMidPieceInstances(int platformHeight)
        {
            for (int i = m_midPieceInstances.Count - 1; i >= platformHeight - 1 && i >= 0; i--)
            {
                m_midPieceInstances[i].Destroy();
                m_midPieceInstances.RemoveAt(i);
            }
        }
    }
}
