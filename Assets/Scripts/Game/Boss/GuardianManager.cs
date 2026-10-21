using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000254 and nested 02000255; 0x711AF0..0x711C50, 0x706F90.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GuardianManager : MonoBehaviour, ISystem
    {
        [Serializable]
        public struct EggpawnRestPoints
        {
            public Transform Location;
            [HideInInspector] public bool Occupied;
        }

        [Tooltip("Arranged in order of closeness to the boss. These will be occupied by returning eggpawns in Phase 4."), SerializeField]
        private EggpawnRestPoints[] m_eggpawnRestPoints;
        public Action FinalHitReady;

        // 06000caf.
        private void Awake() => ProcessManager.RegisterSystem(this, null, false, false);

        // 06000cb0: claims the first unoccupied rest point.
        public bool TryGetFreeReturnPoint(out Transform usableLocation)
        {
            usableLocation = null;
            for (int i = 0; i < m_eggpawnRestPoints.Length; i++)
            {
                if (m_eggpawnRestPoints[i].Occupied) continue;
                m_eggpawnRestPoints[i].Occupied = true;
                usableLocation = m_eggpawnRestPoints[i].Location;
                return true;
            }
            return false;
        }

        // 06000cb1.
        public void BossReadyForFinalHit() => FinalHitReady?.Invoke();

        // 06000cb2.
        private void OnDestroy() => ProcessManager.UnregisterSystem(this);
    }
}
