using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000676. The original selection order and overwrite
    // behavior are retained; this visual spacing component is not actor physics.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SameSpaceObjectAvoidance : MonoBehaviour
    {
        [SerializeField] private int m_uniqueIndex;
        [SerializeField] private SmoothedTransformProxy m_proxy;
        [SerializeField] private List<SmoothedTransformProxy> m_allConcurrentProxies;
        [SerializeField] private float m_SSOADistance = 3f;
        [SerializeField] private float m_SSOADistanceSqrTolerance = .01f;
        [SerializeField] private float m_SSOARotationDetachMin = 30f;
        [SerializeField] private float m_SSOARotationSpeed = 1f;
        [SerializeField] private AnimationCurve m_SSOATranslationSpeedCurve;
        [SerializeField] private LineRenderer m_SSOALineRenderer;
        private List<SmoothedTransformProxy> m_ssoaProxyList;
        private Vector3 m_centre = Vector3.zero;
        private float m_radius;
        private float m_rotation;
        private float m_targetRotation;
        private Vector3 m_targetOffset = Vector3.zero;
        private float m_SSOADistanceSqr;
        private const float YOffsetAboveGround = .25f;
        // Original 060022f9/fa / ARM64 558c84/90, including private setter.
        public Vector3 Offset { get; private set; }
        // 060022fb / 558c9c does not reset offset or prior rotation.
        private void OnEnable()
        {
            m_ssoaProxyList = new List<SmoothedTransformProxy>();
            m_SSOADistanceSqr = m_SSOADistance * m_SSOADistance;
        }
        // 060022fc / 558d2c. Debug rendering is not part of this update.
        private void LateUpdate()
        {
            UpdateTargetActors(m_uniqueIndex);
            if (m_ssoaProxyList.Count > 0) { _ = CalculateTarget(); UpdateOffsetToTarget(); }
            else
            {
                m_centre = m_proxy.transform.position;
                m_radius = m_SSOADistance;
                UpdateOffsetToTarget();
            }
        }
        // 060022fd / 559558: later qualifying actors overwrite earlier results.
        private float ActorHasOverlap(int thisIndex)
        {
            float distanceSqr = m_SSOADistanceSqr;
            Vector3 position = m_proxy.CachedTransform.position;
            float result = 0f;
            if (thisIndex <= 0) return result;
            Vector3 predicted = position + m_targetOffset;
            for (int i = 0; i < thisIndex; i++)
            {
                float overlapSqr = distanceSqr - (m_allConcurrentProxies[i].transform.position - predicted).sqrMagnitude;
                if (overlapSqr > m_SSOADistanceSqrTolerance && overlapSqr < m_SSOADistanceSqr)
                    result = m_SSOADistance - Mathf.Sqrt(overlapSqr);
            }
            return result;
        }
        // 060022fe / 558dbc visits predecessors in reverse order, recalculating
        // after each newly added actor. Detaching can retain the prior ordered list.
        private void UpdateTargetActors(int thisIndex)
        {
            float distanceSqr = m_SSOADistanceSqr;
            int previousCount = m_ssoaProxyList.Count;
            var previous = new List<SmoothedTransformProxy>();
            for (int i = 0; i < previousCount; i++) previous.Add(m_ssoaProxyList[i]);
            m_ssoaProxyList.Clear();
            m_targetOffset = Vector3.zero;
            Vector3 predicted = m_proxy.CachedTransform.position;
            Vector3 original = m_proxy.transform.position;
            for (int i = thisIndex - 1; i >= 0; i--)
            {
                SmoothedTransformProxy candidate = m_allConcurrentProxies[i];
                if (m_proxy == candidate) continue;
                Vector3 position = candidate.transform.position;
                if ((position - predicted).sqrMagnitude < distanceSqr || (position - original).sqrMagnitude < distanceSqr)
                {
                    int before = m_ssoaProxyList.Count;
                    m_ssoaProxyList.AddUnique(candidate);
                    if (m_ssoaProxyList.Count != before)
                    {
                        CalculateCircle();
                        _ = CalculateTarget();
                        predicted = m_proxy.CachedTransform.position + m_targetOffset;
                    }
                }
            }
            if (m_ssoaProxyList.Count == 0 && previousCount >= 1 &&
                Mathf.Abs(m_targetRotation - m_rotation) > m_SSOARotationDetachMin)
            {
                for (int i = 0; i < previousCount; i++) m_ssoaProxyList.Add(previous[i]);
                CalculateCircle();
                _ = CalculateTarget();
                _ = m_proxy.CachedTransform.position;
            }
        }
        // 060022ff / 559674 retains the unguarded zero-count division.
        private void CalculateCircle()
        {
            int count = m_ssoaProxyList.Count;
            m_centre = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                SmoothedTransformProxy proxy = m_ssoaProxyList[i];
                Vector3 centre = m_centre;
                m_centre = centre + proxy.transform.position;
            }
            m_centre /= count;
            m_radius = 0f;
            for (int i = 0; i < count; i++)
            {
                float squaredDistance = (m_ssoaProxyList[i].transform.position - m_centre).sqrMagnitude;
                m_radius = Mathf.Max(m_radius, squaredDistance);
            }
            m_radius = Mathf.Sqrt(m_radius) + m_SSOADistance;
        }
        // 06002300 / 559234 returns the radial direction, not its computed offset.
        private Vector3 CalculateTarget()
        {
            float tolerance = m_SSOADistanceSqrTolerance;
            float rotationSpeed = m_SSOARotationSpeed;
            m_targetRotation = CalculateRotation();
            if (Offset.sqrMagnitude < tolerance) m_rotation = m_targetRotation;
            else
            {
                float delta = Mathf.DeltaAngle(m_rotation, m_targetRotation);
                m_targetRotation = m_rotation + MathUtilities.ShortestAngle(delta);
                m_rotation = Mathf.LerpAngle(m_rotation, m_targetRotation, rotationSpeed * Time.deltaTime);
            }
            Vector3 direction = Quaternion.AngleAxis(m_rotation, m_proxy.transform.up) * m_proxy.transform.forward;
            m_targetOffset = m_centre - m_proxy.CachedTransform.position + direction * m_radius;
            return direction;
        }
        // 06002301 / 55944c: evaluate the curve before reading interpolation ends.
        private void UpdateOffsetToTarget()
        {
            float speed = 1f;
            if (m_ssoaProxyList.Count >= 1)
                speed = m_SSOATranslationSpeedCurve.Evaluate(
                    (m_centre - m_proxy.CachedTransform.position).sqrMagnitude / m_SSOADistanceSqr);
            Vector3 current = Offset;
            Vector3 target = m_targetOffset;
            Offset = Vector3.Lerp(current, target, speed * Time.deltaTime);
        }
        // 06002302 / 559818 retains separate pose reads and position-centre sign.
        private float CalculateRotation()
        {
            Vector3 reference = m_proxy.CachedTransform.rotation * Vector3.forward;
            Vector3 position = m_proxy.CachedTransform.position;
            Vector3 other = position - m_centre;
            Vector3 normal = m_proxy.CachedTransform.rotation * Vector3.up;
            return Vector3Extensions.SignedVectorAngle(reference, other, normal);
        }
        // 06002303 / 559994: 37 inclusive ten-degree circle samples, a lower stem,
        // then three points per neighbour. Final elevation is world Y, not local up.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RenderCircle()
        {
            LineRenderer renderer = m_SSOALineRenderer;
            if (renderer == null) return;
            int count = m_ssoaProxyList.Count;
            if (count == 0) { renderer.positionCount = 0; return; }
            var points = new Vector3[count * 3 + 38];
            float angle = 0f;
            for (int index = 0; index < 37; index++, angle += 10f)
            {
                float radians = angle * Mathf.Deg2Rad;
                float sine = Mathf.Sin(radians);
                float cosine = Mathf.Cos(radians);
                Vector3 centre = m_centre;
                Vector3 point = centre + m_proxy.transform.up * .1f;
                float forwardRadius = m_radius;
                point += m_proxy.transform.forward * (cosine * (forwardRadius * .5f));
                float rightRadius = m_radius;
                point += m_proxy.transform.right * (sine * (rightRadius * .5f));
                points[index] = point;
            }
            points[37] = m_proxy.transform.position - m_proxy.transform.up * 5f;
            int pointIndex = 38;
            for (int i = 0; i < count; i++)
            {
                SmoothedTransformProxy proxy = m_ssoaProxyList[i];
                points[pointIndex++] = m_proxy.transform.position;
                points[pointIndex++] = proxy.transform.position;
                points[pointIndex++] = m_proxy.transform.position;
            }
            for (int i = 0; i < points.Length; i++) points[i].y += YOffsetAboveGround;
            renderer.widthMultiplier = .1f;
            renderer.positionCount = pointIndex;
            renderer.SetPositions(points);
        }
        // 06002304 / 559e0c retains null lists and curves until genuine OnEnable.
        public SameSpaceObjectAvoidance() { }
    }
}
