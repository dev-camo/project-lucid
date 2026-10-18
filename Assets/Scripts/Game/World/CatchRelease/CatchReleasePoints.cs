using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000abf; 0x6E38C0..0x6E53B0. The private search helpers
    // are original methods; the native Evaluate bodies inline copies of them.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class CatchReleasePoints
    {
        [SerializeField]
        [Tooltip("Transform points considered for release.")]
        private Transform[] m_points = Array.Empty<Transform>();
        [Tooltip("Determines the release logic for position.")]
        [SerializeField]
        private CatchReleasePositionType m_releasePositionType = CatchReleasePositionType.Free;
        [Tooltip("Determines the release logic for rotation.")]
        [SerializeField]
        private CatchReleaseRotationType m_releaseRotationType = CatchReleaseRotationType.Free;
        [SerializeField]
        [ShowIf("m_releaseRotationType", CatchReleaseRotationType.FreeLookup)]
        [Tooltip("Release angle from plane determined by the attach angle to plane.")]
        private AnimationCurve m_lookupFromAttachAngle = AnimationCurve.Linear(0f, 0f, 360f, 360f);
        [SerializeField]
        [Tooltip("Determines release angle on plane from input.")]
        private AnimationCurve m_lookupFromInputAngle = AnimationCurve.Linear(0f, 0f, 360f, 360f);
        [Tooltip("Use when the forward release direction should be flipped 180 degrees when catching from the reversed location.")]
        [SerializeField]
        private bool m_inverseRotationsWhenReversed;

        public bool HasPoints => m_points.Length != 0;

        // Without points the character position is always free.
        public bool IsPositionFree => !HasPoints || m_releasePositionType == CatchReleasePositionType.Free;

        public Vector3 EvaluatePosition(Character character, bool isReversed)
        {
            Vector3 position = character.WorldPosition;
            if (!HasPoints)
                return position;
            switch (m_releasePositionType)
            {
                case CatchReleasePositionType.Free:
                    return position;
                case CatchReleasePositionType.Last:
                    return FindNextPoint(character.ForwardDirection, position, isReversed, -1).position;
                case CatchReleasePositionType.Next:
                    return FindNextPoint(character.ForwardDirection, position, isReversed, 1).position;
                case CatchReleasePositionType.QuantizeClosestAngle:
                    return FindClosestDotProduct(character.ForwardDirection, isReversed).position;
                case CatchReleasePositionType.QuantizeClosestPosition:
                    return FindClosestPoint(position, out int _).position;
                default:
                    throw new ArgumentOutOfRangeException("m_releasePositionType", m_releasePositionType, "Is not supported in CatchReleasePoints");
            }
        }

        // FreeLookup falls back to the closest point when there is no steering input. A
        // reversed catch may flip the point rotation half a turn around the gravity axis.
        public Quaternion EvaluateRotation(Character character, bool isReversed, Vector3 attachDirection, Transform attachTransform)
        {
            Quaternion rotation = character.WorldRotation;
            if (m_releaseRotationType == CatchReleaseRotationType.Free)
                return rotation;
            Transform closest;
            if (m_releaseRotationType == CatchReleaseRotationType.FreeLookup)
            {
                if (CalculateCharacterSteeringRotation(character, attachDirection, ref rotation))
                    return rotation;
                if (!HasPoints)
                    return rotation;
                closest = FindClosestPoint(character.WorldPosition, out int _);
            }
            else
            {
                if (!HasPoints)
                    return rotation;
                switch (m_releaseRotationType)
                {
                    case CatchReleaseRotationType.QuantizeClosestPosition:
                        closest = FindClosestPoint(character.WorldPosition, out int _);
                        break;
                    case CatchReleaseRotationType.Last:
                        closest = FindNextPoint(character.ForwardDirection, character.WorldPosition, isReversed, -1);
                        break;
                    case CatchReleaseRotationType.Next:
                        closest = FindNextPoint(character.ForwardDirection, character.WorldPosition, isReversed, 1);
                        break;
                    case CatchReleaseRotationType.QuantizeClosestAngle:
                        closest = FindClosestDotProduct(character.ForwardDirection, isReversed);
                        break;
                    case CatchReleaseRotationType.ClosestRotationLookup:
                        closest = FindClosestDotProduct(CalculateTransformSteeringDirection(character, attachTransform), isReversed);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("m_releaseRotationType", m_releaseRotationType, "Is not supported in CatchReleasePoints");
                }
            }
            if (isReversed && m_inverseRotationsWhenReversed)
                return closest.rotation * Quaternion.AngleAxis(180f, character.GravityNormalised);
            return closest.rotation;
        }

        // Closest active point by squared distance; the first of equals wins.
        private Transform FindClosestPoint(Vector3 position, out int closestIndex)
        {
            closestIndex = -1;
            Transform closest = null;
            float closestSqrDistance = float.MaxValue;
            for (int i = 0; i < m_points.Length; i++)
            {
                Transform point = m_points[i];
                if (!point.gameObject.activeInHierarchy)
                    continue;
                float sqrDistance = (point.position - position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestIndex = i;
                    closestSqrDistance = sqrDistance;
                    closest = point;
                }
            }
            return closest;
        }

        // Active point whose forward best matches the direction; the last of equals wins. A
        // reversed catch with inverted rotations looks for the best opposite match instead.
        private Transform FindClosestDotProduct(Vector3 forward, bool isReversed)
        {
            Transform best = null;
            float bestDot = float.MinValue;
            bool invert = isReversed && m_inverseRotationsWhenReversed;
            for (int i = 0; i < m_points.Length; i++)
            {
                Transform point = m_points[i];
                if (!point.gameObject.activeInHierarchy)
                    continue;
                float dot = Vector3.Dot(point.forward, forward);
                if (invert)
                    dot = -dot;
                if (!(bestDot > dot))
                {
                    bestDot = dot;
                    best = point;
                }
            }
            return best;
        }

        // Stays on the closest point unless the position has passed it in the travel
        // direction, then steps along the points (reversed flips the direction).
        private Transform FindNextPoint(Vector3 forward, Vector3 position, bool isReversed, int indexOffset)
        {
            Transform closest = FindClosestPoint(position, out int closestIndex);
            float side = Vector3.Dot(position - closest.position, forward);
            if (indexOffset < 0)
                side = -side;
            if (side < 0f)
                return closest;
            int step = isReversed ? -indexOffset : indexOffset;
            Transform next = m_points[Mathf.Clamp(closestIndex + step, 0, m_points.Length - 1)];
            if (next.gameObject.activeInHierarchy)
                closest = next;
            return closest;
        }

        // Turns the character by the steering lookup relative to the camera heading and tilts it by
        // the lookup from the angle between the attach direction and that heading. Returns false
        // without writing the rotation when there is no steering input.
        private bool CalculateCharacterSteeringRotation(Character character, Vector3 attachDirection, ref Quaternion rotation)
        {
            character.ActiveTracking.GetSteeringInput(out Vector3 inputForward, out float inputTurn);
            if (Mathf.Approximately(inputForward.sqrMagnitude, 0f))
                return false;
            float steeringAngle = m_lookupFromInputAngle.Evaluate(inputTurn);
            Vector3 up = character.UpDirection;
            Vector3 forwardOnPlane = character.ForwardDirection - up * Vector3.Dot(up, character.ForwardDirection);
            Vector3 cameraForward = character.CameraForwardOnCharacterPlane;
            float deltaAngle = Mathf.DeltaAngle(Vector3.SignedAngle(cameraForward, forwardOnPlane, up), steeringAngle);
            Quaternion steering = Quaternion.AngleAxis(deltaAngle, Vector3.up);
            float attachAngle = Vector3.Angle(attachDirection, cameraForward);
            Quaternion tilt = Quaternion.AngleAxis(m_lookupFromAttachAngle.Evaluate(attachAngle), -Vector3.right);
            rotation = character.WorldRotation * steering * tilt;
            return true;
        }

        // The camera heading turned by the steering lookup, expressed in the attach
        // transform's up frame. Without steering input the character up is returned.
        private Vector3 CalculateTransformSteeringDirection(Character character, Transform attachTransform)
        {
            character.ActiveTracking.GetSteeringInput(out Vector3 inputForward, out float inputTurn);
            if (Mathf.Approximately(inputForward.sqrMagnitude, 0f))
                return character.UpDirection;
            Quaternion steering = Quaternion.AngleAxis(m_lookupFromInputAngle.Evaluate(inputTurn), Vector3.up);
            Quaternion heading = Quaternion.LookRotation(character.CameraForwardOnCharacterPlane, attachTransform.up);
            return heading * steering * Vector3.forward;
        }
    }
}
