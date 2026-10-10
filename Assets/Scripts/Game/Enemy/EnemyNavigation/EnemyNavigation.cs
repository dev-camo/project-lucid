using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class EnemyNavigation : MonoBehaviour
    {
        [SerializeField] private bool m_constrain;
        [ShowIf("m_constrain", null), SerializeField]
        private ConstrainType m_constrainType = ConstrainType.Distance;
        [SerializeField, ShowIf("m_constrainType", ConstrainType.Distance), Min(0f)]
        private float m_constrainDistance;
        [ShowIf("m_constrainType", ConstrainType.Volume), SerializeField, Min(0f)]
        private Collider m_constrainCollider;

        public bool IsInitialised { get; protected set; }
        public bool Constrain => m_constrain;
        protected ConstrainType ConstrainMethod => m_constrainType;
        protected float ConstrainDistance => m_constrainDistance;
        protected Collider ConstrainCollider => m_constrainCollider;

        private void Awake()
        {
            SetUp();
        }

        private void OnValidate()
        {
            if (gameObject.CanValidate(false))
                SetUp();
        }

        protected abstract void SetUp();
        public abstract void StartNavigation();
        public abstract void InterruptNavigation();
        public abstract Vector3 GetDestination();
        public abstract Vector3 ConstrainPosition(Vector3 position);
        public abstract bool CanReachPosition(Vector3 position);

        protected static Vector3 ConstrainToPoint(Vector3 position, float withinDistance, Vector3 point)
        {
            Vector3 offset = point - position;
            return point - offset * (withinDistance / offset.magnitude);
        }

        protected static Vector3 ConstrainToVolume(Collider withinCollider, Vector3 point)
        {
            return withinCollider.ClosestPoint(point);
        }

        protected static bool CanReachPoint(Vector3 position, float withinDistance, Vector3 point)
        {
            return (point - position).sqrMagnitude <= withinDistance * withinDistance;
        }

        protected static bool CanReachPoint(Collider withinCollider, Vector3 point)
        {
            return withinCollider.ClosestPoint(point) == point;
        }

        protected static bool CanReachLine(Vector3 position, float withinDistance, Vector3 point, Quaternion rotation)
        {
            Vector3 closest = MathUtilities.GetClosestPointOnLine(point, rotation * Vector3.forward, position);
            return (closest - position).sqrMagnitude <= withinDistance * withinDistance;
        }

        protected EnemyNavigation() { }

        protected enum ConstrainType
        {
            Distance = 1,
            Volume = 2
        }
    }
}
