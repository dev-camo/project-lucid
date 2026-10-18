using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a6f: a capsule whose gravity applies only within
    // an arc of angles measured around the forward axis from the transform's up.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GravityCapsuleArc : GravityCapsule
    {
        private const int GizmoDivisions = 10;
        [SerializeField]
        [Range(0, 360)]
        [Tooltip("Gravity cylinder arc's start angle.")]
        private float m_arcAngleStart;
        [SerializeField]
        [Range(0, 360)]
        [Tooltip("Gravity cylinder arc's end angle.")]
        private float m_arcAngleEnd = 180f;

        // 06003bee: both arc limits are inclusive; a NaN angle is not rejected.
        public override Vector3 GetGravity(Vector3 position)
        {
            Transform arcTransform = transform;
            Vector3 direction = (position - arcTransform.position).normalized;
            float angle = Vector3.SignedAngle(arcTransform.up, direction, arcTransform.forward);
            if (angle < 0f)
                angle += 360f;
            if (angle < m_arcAngleStart || angle > m_arcAngleEnd)
                return Vector3.zero;
            return base.GetGravity(position);
        }

        // 06003bef: the retail body is empty; GetCapPosition has no retail caller.
        protected override void DrawGizmo(float distance) { }

        // 06003bf0: the angle is negated and no transform position is added.
        private Vector3 GetCapPosition(float distance, float angle)
        {
            float radians = -angle * Mathf.Deg2Rad;
            Quaternion rotation = Quaternion.AngleAxis(90f, transform.up) * transform.rotation;
            return rotation * new Vector3(0f, Mathf.Cos(radians) * distance, Mathf.Sin(radians) * distance);
        }
    }
}
