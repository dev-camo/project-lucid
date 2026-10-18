using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200033f. Mirrors a muzzle transform between the
    // authored right-side pose and a left-side pose derived from it.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterMuzzle
    {
        private readonly Transform m_transform;
        private readonly Vector3 m_leftSideRotation;
        private readonly Vector3 m_leftSideScale;
        private readonly Vector3 m_rightSideRotation;
        private readonly Vector3 m_rightSideScale;
        // Initialiser constants read from native data at 0x29a7d40/0x29a7d50.
        private readonly Vector3 m_rotateMultiplier = new Vector3(-1f, -1f, 1f);
        private readonly Vector3 m_scaleMultiplier = new Vector3(1f, 1f, -1f);

        // 0600145e; the authored pose is the right side, the left side is mirrored from it.
        public CharacterMuzzle(Transform transform)
        {
            m_transform = transform;
            m_rightSideRotation = m_transform.localEulerAngles;
            m_rightSideScale = m_transform.localScale;
            m_leftSideRotation = Vector3.Scale(m_rotateMultiplier, m_rightSideRotation);
            m_leftSideScale = Vector3.Scale(m_scaleMultiplier, m_rightSideScale);
        }

        // 0600145f
        public void SetMuzzleSide(bool isRightSide)
        {
            m_transform.localEulerAngles = isRightSide ? m_rightSideRotation : m_leftSideRotation;
            m_transform.localScale = isRightSide ? m_rightSideScale : m_leftSideScale;
        }
    }
}
