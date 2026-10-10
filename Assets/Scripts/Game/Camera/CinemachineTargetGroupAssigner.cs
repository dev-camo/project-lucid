using Cinemachine;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CinemachineTargetGroupAssigner : MonoBehaviour
    {
        [SerializeField] private CinemachineTargetGroup m_targetGroup;
        [SerializeField] protected float m_weight = 1f;
        [SerializeField] protected float m_radius = 1f;

        protected void AddMember(Transform target)
        {
            m_targetGroup.AddMember(target, m_weight, m_radius);
        }

        protected void RemoveMember(Transform target)
        {
            m_targetGroup.RemoveMember(target);
        }

        protected CinemachineTargetGroupAssigner()
        {
        }
    }
}
