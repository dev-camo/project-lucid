using System;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004a8 with nested Events 0x020004a9 and CatchPoint
    // 0x020004aa; CalculateCachedValues 0x5402D0 (an empty body), constructor 0x5404D0.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class CatchReleaseDefinition_Catch
    {
        [Min(0)]
        [Tooltip("Minimum time to remain in toy before a transition exit can be triggered.")]
        public float RemainTime;
        [Tooltip("If true, consider if the character is approaching from the opposite direction to this forward direction.")]
        public bool ReverseOnDirection;
        [Tooltip("Defines the catch point in the forward direction.")]
        public CatchPoint PointForward = new CatchPoint();
        [Tooltip("Defines the catch point in the reverse direction.")]
        public CatchPoint PointReverse = new CatchPoint();
        [Tooltip("Maintains the visual connection between pivot and catch points.")]
        public CatchConnector Connector;

        public void CalculateCachedValues()
        {
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class Events
        {
            [SerializeField]
            public UnityEvent<float> m_onEnter = new UnityEvent<float>();
            [SerializeField]
            public UnityEvent<float> m_onUpdateConstraints = new UnityEvent<float>();
            [SerializeField]
            public UnityEvent<float> m_onExit = new UnityEvent<float>();
            [SerializeField]
            public UnityEvent m_onReset = new UnityEvent();

            public void OnEnter(float speed)
            {
                m_onEnter.Invoke(speed);
            }

            public void OnUpdateConstraints(float distance)
            {
                m_onUpdateConstraints.Invoke(distance);
            }

            public void OnExit(float speed)
            {
                m_onExit.Invoke(speed);
            }

            public void OnReset()
            {
                m_onReset.Invoke();
            }
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class CatchPoint
        {
            [Tooltip("Local position offset when attaching.")]
            public Vector3 PositionOffset;
            [Tooltip("Local angle offset when attaching.")]
            public Vector3 AngleOffset;
            [Tooltip("Whether to ignore orientation of the catch transform.")]
            public bool IgnoreRotation;
            [Tooltip("Events to fire if attaching.")]
            public Events Events = new Events();
        }
    }
}
