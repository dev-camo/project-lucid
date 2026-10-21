using System.Collections.Generic;
using Cinemachine;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000268. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineAddTransformToTargetGroup : MonoBehaviour
    {
        [SerializeField] private CinemachineTargetGroup m_targetGroup;
        [SerializeField] private float m_weight = 1f;
        [SerializeField] private float m_radius = 1f;
        [SerializeField] private bool m_addOnEnable = true;
        [SerializeField] private bool m_removeOnDisable = true;
        [SerializeField] private bool m_removeAllOthers;
        [SerializeField] private bool m_restoreAllOthersOnRemove;
        private List<CinemachineTargetGroup.Target> m_clearedTargetGroup;

        // 0x06000d43: optionally park the group's existing members, last to first, before joining it.
        public void AddTransformToGroup()
        {
            if (m_removeAllOthers)
            {
                m_clearedTargetGroup = new List<CinemachineTargetGroup.Target>();
                for (int i = m_targetGroup.m_Targets.Length - 1; i >= 0; i--)
                {
                    CinemachineTargetGroup.Target target = m_targetGroup.m_Targets[i];
                    m_clearedTargetGroup.Add(target);
                    m_targetGroup.RemoveMember(target.target);
                }
            }
            m_targetGroup.AddMember(transform, m_weight, m_radius);
        }

        // 0x06000d44
        public void OverrideTargetGroup(CinemachineTargetGroup group) { m_targetGroup = group; }

        // 0x06000d45: parked members come back in their parked order.
        public void RemoveTransformFromGroup()
        {
            m_targetGroup.RemoveMember(transform);
            if (!m_restoreAllOthersOnRemove) return;

            for (int i = 0; i < m_clearedTargetGroup.Count; i++)
            {
                CinemachineTargetGroup.Target target = m_clearedTargetGroup[i];
                m_targetGroup.AddMember(target.target, target.weight, target.radius);
            }
            m_clearedTargetGroup.Clear();
        }

        // 0x06000d46
        private void OnEnable()
        {
            if (m_addOnEnable) AddTransformToGroup();
        }

        // 0x06000d47
        private void OnDisable()
        {
            if (m_removeOnDisable) RemoveTransformFromGroup();
        }
    }
}
