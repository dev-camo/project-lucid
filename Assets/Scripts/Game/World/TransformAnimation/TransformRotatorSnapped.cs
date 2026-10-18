using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000828: one endless coroutine per non-zero axis that
    // waits that axis' interval and then turns the local rotation by its step.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformRotatorSnapped : MonoBehaviour
    {
        [Header("Rotates objects instantly by a fixed amount and loops forever.", order = 0)]
        [Space(10)]
        [SerializeField]
        private Vector3 m_rotationPerInterval = Vector3.zero;
        [SerializeField]
        private Vector3 m_timePerIntervalInSeconds = Vector3.zero;
        [SerializeField]
        private TimeCategory m_timeCategory = TimeCategory.Environment;
        private Transform m_transform;

        private void Awake()
        {
            m_transform = transform;
        }

        private void OnEnable()
        {
            if (m_rotationPerInterval.x != 0f)
                StartCoroutine(ProcessRotation(m_timePerIntervalInSeconds.x, Quaternion.AngleAxis(m_rotationPerInterval.x, Vector3.right)));
            if (m_rotationPerInterval.y != 0f)
                StartCoroutine(ProcessRotation(m_timePerIntervalInSeconds.y, Quaternion.AngleAxis(m_rotationPerInterval.y, Vector3.up)));
            if (m_rotationPerInterval.z != 0f)
                StartCoroutine(ProcessRotation(m_timePerIntervalInSeconds.z, Quaternion.AngleAxis(m_rotationPerInterval.z, Vector3.forward)));
        }

        private IEnumerator ProcessRotation(float timeSeconds, Quaternion rotation)
        {
            while (true)
            {
                yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(timeSeconds, m_timeCategory);
                m_transform.localRotation *= rotation;
            }
        }
    }
}
