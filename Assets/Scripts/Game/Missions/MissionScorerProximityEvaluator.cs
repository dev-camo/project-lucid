using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000713: scores the distance, along the enabled local axes, between the other transform
    // and this component (or its override), using the last authored distance band that the distance exceeds.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionScorerProximityEvaluator : MissionScorerEvaluator
    {
        // Original nested class 0x02000714.
        [Serializable]
        public class TargetDistanceDefinition
        {
            [SerializeField]
            [Tooltip("Minimum distance from target.")]
            private float m_distance;
            [Tooltip("Score at this distance from target.")]
            [SerializeField]
            private float m_score;
            [SerializeField]
            [Tooltip("Animation to trigger at this distance from target.")]
            private ActorAnimationDefinition m_animationDefinition;

            public float Score => m_score;
            public float Distance => m_distance;
            public ActorAnimationDefinition AnimationDefinition => m_animationDefinition;

            public TargetDistanceDefinition() { }
        }

        [Tooltip("If set, this is used as the transform location instead of the game object the component is assigned to.")]
        [SerializeField]
        private Transform m_optionalTransformOverride;
        [Tooltip("Whether to use the local axis along the x.")]
        [SerializeField]
        private bool m_useXAxis = true;
        [Tooltip("Whether to use the local axis along the y.")]
        [SerializeField]
        private bool m_useYAxis = true;
        [Tooltip("Whether to use the local axis along the z.")]
        [SerializeField]
        private bool m_useZAxis = true;
        [Tooltip("Definition to use when scoring and triggering pfx animation at each distance.")]
        [FormerlySerializedAs("m_distanceAnimationTriggers")]
        [SerializeField]
        private List<TargetDistanceDefinition> m_targetDistanceDefinitions;

        // 06002798 (0x5C0A70): the first definition is the fallback and is read before anything else, so an empty list
        // faults there. Later definitions replace it while the distance is strictly greater than theirs; a null entry
        // or the first definition that is not exceeded ends the search.
        public override float Evaluate(MissionObjective missionObjective, Transform otherTransform,
            out ActorAnimationDefinition animationDefinition)
        {
            Transform origin = m_optionalTransformOverride != null ? m_optionalTransformOverride : transform;
            Vector3 point = origin.InverseTransformPoint(otherTransform.position);
            if (!m_useXAxis) point.x = 0f;
            if (!m_useYAxis) point.y = 0f;
            if (!m_useZAxis) point.z = 0f;
            float distance = Vector3.Distance(Vector3.zero, point);

            int count = m_targetDistanceDefinitions.Count;
            TargetDistanceDefinition selected = m_targetDistanceDefinitions[0];
            for (int i = 1; i < count; i++)
            {
                TargetDistanceDefinition candidate = m_targetDistanceDefinitions[i];
                if (candidate == null || !(distance > candidate.Distance)) break;
                selected = candidate;
            }
            animationDefinition = selected.AnimationDefinition;
            return selected.Score;
        }

        public MissionScorerProximityEvaluator() { }
    }
}
