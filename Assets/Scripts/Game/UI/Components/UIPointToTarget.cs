using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000849; bodies translated from the 1.10.1 x86-64 binary.
    // Rotates a UI arrow about Z so it points from this element towards a world position.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIPointToTarget : MonoBehaviour
    {
        private Vector3 m_targetPosition;
        private bool m_targetBehindCamera;

        // 0x06003000
        public void SetTarget(Vector3 targetPosition)
        {
            m_targetPosition = targetPosition;
        }

        // 0x06003001
        public void SetTargetBehindCamera(bool targetBehindCamera)
        {
            m_targetBehindCamera = targetBehindCamera;
        }

        // 0x06003002: the planar direction is flipped for a target behind the camera. The angle is
        // atan2(-x, y) in degrees, so straight up (+Y) is 0 and +X turns clockwise.
        private void Update()
        {
            Vector3 direction = m_targetPosition - transform.position;
            if (m_targetBehindCamera)
                direction = -direction;
            float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            transform.eulerAngles = new Vector3(0f, 0f, angle);
        }

        // 0x06003003
        public UIPointToTarget() { }
    }
}
