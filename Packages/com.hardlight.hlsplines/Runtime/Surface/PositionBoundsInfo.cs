using UnityEngine;

namespace Hardlight
{
    public readonly struct PositionBoundsInfo
    {
        public readonly float LeftBoundSqrDistance;
        public readonly float RightBoundSqrDistance;
        public readonly bool AtEndEdge;
        public readonly Vector3 LeftPosition;
        public readonly Vector3 RightPosition;

        // HLSplines.Runtime060001aa: five original fields publish in declaration order.
        public PositionBoundsInfo(float leftBoundSqrDistance, float rightBoundSqrDistance, bool atEndEdge, Vector3 leftPosition, Vector3 rightPosition)
        {
            LeftBoundSqrDistance = leftBoundSqrDistance;
            RightBoundSqrDistance = rightBoundSqrDistance;
            AtEndEdge = atEndEdge;
            LeftPosition = leftPosition;
            RightPosition = rightPosition;
        }

        //060001ab..ad: strict ordered less-than; equality and NaN do not pass.
        public bool OnLateralBounds(float sqrDistanceThreshold)
            => LeftBoundSqrDistance < sqrDistanceThreshold || RightBoundSqrDistance < sqrDistanceThreshold;
        public bool OnLeftBounds(float sqrDistanceThreshold) => LeftBoundSqrDistance < sqrDistanceThreshold;
        public bool OnRightBounds(float sqrDistanceThreshold) => RightBoundSqrDistance < sqrDistanceThreshold;
    }
}
