using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000810, all four fields and six methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformFakeParent : MonoBehaviour
    {
        public bool IgnoreRotation { get; private set; }
        private Transform m_targetParent;
        private Vector3 m_translationOffset;
        private Quaternion m_rotationOffset;

        // 0x06002ea5: store the target and the two offsets without modifying either
        // transform or normalizing the supplied quaternion.
        public void StartParenting(Transform targetParent, Vector3 translationOffset,
            Quaternion rotationOffset, bool ignoreRotation)
        {
            m_targetParent = targetParent;
            m_translationOffset = translationOffset;
            m_rotationOffset = rotationOffset;
            IgnoreRotation = ignoreRotation;
        }

        // 0x06002ea6: the supplied release stores IgnoreRotation but does not read
        // it here. Translation uses the composed rotation, including rotationOffset,
        // rather than TransformPoint (which would also apply parent scale).
        public bool Evaluate(out Vector3 position, out Quaternion rotation)
        {
            if (m_targetParent == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }
            rotation = m_targetParent.rotation * m_rotationOffset;
            position = m_targetParent.position + rotation * m_translationOffset;
            return true;
        }

        // 0x06002ea7: clear the target and offsets, retaining IgnoreRotation.
        public void Stop()
        {
            m_targetParent = null;
            m_translationOffset = Vector3.zero;
            m_rotationOffset = Quaternion.identity;
        }
    }
}
