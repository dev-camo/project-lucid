using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a4e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class DebugTargetingHomingAttack : MonoBehaviour
    {
        [SerializeField]
        private float m_projectDistance = 10f;

        [SerializeField]
        private float m_projectStepDistance = 5f;

        [SerializeField]
        private Vector2 m_projectConeAngle = new Vector2(30f, 30f);

        private ConeVolume.ConeDescription m_cone;
        private List<HomingTarget> m_targets = new List<HomingTarget>();
        private List<HomingTarget> m_validTargets = new List<HomingTarget>();

        // 06003b3c (1.10.1 RVA 0x6BE260): the homing cone is rebuilt from this transform, projected over every
        // HomingTarget in the scene, and the valid targets are drawn green with the best one larger and red.
        // The native body inlines TargetTrackingUtilities.ProjectToTargetedObject.
        private void OnDrawGizmosSelected()
        {
            m_targets.Clear();
            m_targets.AddRange(FindObjectsOfType<HomingTarget>());
            m_cone.Origin = transform.position;
            m_cone.Rotation = transform.rotation;
            m_cone.Distance = m_projectDistance;
            m_cone.Step = m_projectStepDistance;
            m_cone.Tangents = new Vector2(Mathf.Tan(m_projectConeAngle.x * Mathf.Deg2Rad), Mathf.Tan(Mathf.Deg2Rad * m_projectConeAngle.y));
            Quaternion worldToLocal = Quaternion.Inverse(m_cone.Rotation);
            Vector3 forward = m_cone.Rotation * Vector3.forward;
            TargetTrackingUtilities.ProjectToTargetedObject(m_cone, m_targets, forward, worldToLocal, ref m_validTargets, out HomingTarget bestTarget);
            Gizmos.color = Color.green;
            foreach (HomingTarget target in m_validTargets)
                Gizmos.DrawSphere(target.WorldPosition, 0.5f);
            if (bestTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(bestTarget.WorldPosition, 0.75f);
            }
        }

        // 06003b3d (1.10.1 RVA 0x6BE6D0): the field initialisers precede the MonoBehaviour base call.
        public DebugTargetingHomingAttack() { }
    }
}
