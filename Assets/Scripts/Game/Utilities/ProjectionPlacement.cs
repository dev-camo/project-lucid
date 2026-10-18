using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000765: advances a point along a ballistic arc from one end
    // of a line. A raycast along each step latches RaycastHit, which then keeps the point
    // visible for m_raycastSnapTime; the whole arc can also be traced into a line renderer.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class ProjectionPlacement
    {
        [Tooltip("Whether the projection is from the start location else use the end.")]
        [SerializeField]
        private bool m_fromStart;
        [SerializeField]
        [Tooltip("Initial distance offset to start projection.")]
        private int m_distanceOffset;
        [SerializeField]
        [Tooltip("Distance interval to perform raycast for collision hit.")]
        private float m_raycastInterval = 10f;
        [SerializeField]
        [Tooltip("Time to leave raycast hit snapped to location.")]
        private float m_raycastSnapTime = 1f;
        [SerializeField]
        [Tooltip("Time to step raycast along projection.")]
        private float m_raycastStepTime = 0.1f;
        [Tooltip("Maximum velocity to forward deviation to trigger a raycast check.")]
        [SerializeField]
        private float m_velocityToForward = 0.1f;
        private float m_gravityMultiplier;
        private float m_speed;
        private float m_timeTotal;
        private float m_distance;
        private Vector3 m_positionOffset;
        public bool RaycastHit { get; private set; }
        private float m_raycastHitTimer;
        public Vector3 Position { get; private set; }

        public void Initialise(float startSpeed, float endSpeed, float startGravityMultiplier, float endGravityMultiplier)
        {
            m_speed = m_fromStart ? startSpeed : endSpeed;
            m_gravityMultiplier = m_fromStart ? startGravityMultiplier : endGravityMultiplier;
        }

        // 06002970: Position keeps its last value.
        public void Reset()
        {
            m_timeTotal = 0f;
            m_distance = 0f;
            m_positionOffset = Vector3.zero;
            RaycastHit = false;
            m_raycastHitTimer = 0f;
        }

        // 06002971: returns whether the point should still be shown. The raycast starts
        // from the previous Position, and the line's direction is divided by its length
        // without a zero-length guard.
        public bool Update(Vector3 startPosition, Vector3 endPosition, float projectionSpeed, Vector3 gravity, LayerMask collisionMask, float deltaTime)
        {
            Vector3 line = endPosition - startPosition;
            float lineLength = line.magnitude;
            Vector3 direction = line / lineLength;
            float timeStep = projectionSpeed / m_speed * deltaTime;
            m_timeTotal += timeStep;
            Vector3 origin = m_fromStart ? startPosition : endPosition;
            Vector3 positionOffset = direction * m_distanceOffset
                + (direction * m_speed + 0.5f * m_timeTotal * (gravity * m_gravityMultiplier)) * m_timeTotal;
            Vector3 step = positionOffset - m_positionOffset;
            float stepLength = step.magnitude;
            CheckForRaycastHit(step, collisionMask, timeStep);
            m_distance += stepLength;
            m_positionOffset = positionOffset;
            Position = origin + positionOffset;
            if (RaycastHit)
                return m_raycastHitTimer > 0f;
            return lineLength > m_distance;
        }

        // 06002972: while snapped, the timer only counts down; RaycastHit stays set.
        private void CheckForRaycastHit(Vector3 stepDirection, LayerMask collisionMask, float deltaTime)
        {
            if (RaycastHit)
            {
                m_raycastHitTimer = Mathf.Max(m_raycastHitTimer - deltaTime, 0f);
                return;
            }
            RaycastHit = Physics.Raycast(Position, stepDirection, m_raycastInterval, collisionMask, QueryTriggerInteraction.Ignore);
            if (RaycastHit)
                m_raycastHitTimer = m_raycastSnapTime;
        }

        // 06002973: traces the arc from endPosition in fixed time steps, adding each
        // previous point to the renderer. Once the summed turn (1 - cos) since the last
        // raycast exceeds m_velocityToForward, the chord since that raycast is tested.
        public bool ProjectForRaycastHit(Vector3 startPosition, Vector3 endPosition, Vector3 gravity, LayerMask collisionMask, LineRendererContainer locationRenderer, ref RaycastHit hitInfo, ref float distance)
        {
            Vector3 line = endPosition - startPosition;
            float lineLength = line.magnitude;
            if (m_gravityMultiplier == 0f)
            {
                locationRenderer.AddPosition(startPosition);
                distance += lineLength - m_distanceOffset;
                return false;
            }
            Vector3 gravityStep = gravity * (m_gravityMultiplier * m_raycastStepTime);
            Vector3 position = endPosition;
            Vector3 rayStart = endPosition;
            Vector3 rayDirection = Vector3.zero;
            float deviation = 0f;
            Vector3 direction = line / lineLength;
            Vector3 velocity = direction * m_speed;
            while (distance < lineLength)
            {
                velocity += gravityStep;
                Vector3 step = velocity * m_raycastStepTime;
                rayDirection += step;
                position += step;
                float stepLength = step.magnitude;
                Vector3 stepDirection = step / stepLength;
                distance += stepLength;
                deviation += 1f - Vector3.Dot(direction, stepDirection);
                if (deviation > m_velocityToForward)
                {
                    if (Physics.Raycast(rayStart, rayDirection, out hitInfo, rayDirection.magnitude, collisionMask, QueryTriggerInteraction.Ignore))
                    {
                        locationRenderer.AddPosition(hitInfo.point);
                        distance += hitInfo.distance - stepLength;
                        return true;
                    }
                    rayDirection = Vector3.zero;
                    deviation = 0f;
                    rayStart = position;
                }
                locationRenderer.AddPosition(position - stepDirection * stepLength);
                direction = stepDirection;
            }
            float rayLength = rayDirection.magnitude;
            if (Physics.Raycast(rayStart, rayDirection, out hitInfo, rayLength, collisionMask, QueryTriggerInteraction.Ignore))
            {
                locationRenderer.AddPosition(hitInfo.point);
                distance += hitInfo.distance - rayLength;
                return true;
            }
            locationRenderer.AddPosition(position);
            return false;
        }
    }
}
