using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009fb and its iterator <ProcessActions>d__7 (0x020009fc);
    // bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class DelayedActivator : MonoBehaviour
    {
        [Tooltip("Initial delay to start activation.")]
        [SerializeField]
        private float m_timeToStart;
        [SerializeField]
        [Tooltip("Delay between each activation.")]
        private float m_timeDelay;
        [SerializeField]
        [Tooltip("Time category for activation to be applied in.")]
        private TimeCategory m_timeCategory = TimeCategory.Environment;
        [Tooltip("Game objects to be activated.  Note: They will all be deactivated on start up.")]
        [SerializeField]
        private GameObject[] m_gameObjects;
        [Tooltip("Components to be activated.  Note: They will all be deactivated on start up.")]
        [SerializeField]
        private MonoBehaviour[] m_components;
        [Tooltip("Events invoked on completion of timed activations.")]
        [SerializeField]
        private UnityEvent m_onComplete = new UnityEvent();

        // 0x06003955
        private void OnEnable()
        {
            StartCoroutine(ProcessActions());
        }

        // 0x06003956, MoveNext 0x0600395c (0x6AFFF0). After one fixed update everything is
        // deactivated; the n-th object and n-th component are then activated together. The
        // start wait needs a positive delay, while any non-zero (or NaN) gap waits.
        private IEnumerator ProcessActions()
        {
            yield return new WaitForFixedUpdate();
            int numGameObjects = m_gameObjects.Length;
            for (int i = 0; i < numGameObjects; ++i)
                SetGameObjectActive(i, false);
            int numComponents = m_components.Length;
            for (int i = 0; i < numComponents; ++i)
                SetComponentActive(i, false);
            if (m_timeToStart > 0f)
                yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(m_timeToStart, m_timeCategory);
            int numItems = Mathf.Max(numGameObjects, numComponents);
            for (int i = 0; i < numItems; ++i)
            {
                if (i < numGameObjects)
                    SetGameObjectActive(i, true);
                if (i < numComponents)
                    SetComponentActive(i, true);
                if (m_timeDelay != 0f)
                    yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(m_timeDelay, m_timeCategory);
            }
            m_onComplete.Invoke();
        }

        // 0x06003957: destroyed or missing entries are skipped.
        private void SetGameObjectActive(int index, bool active)
        {
            GameObject target = m_gameObjects[index];
            if (target == null) return;
            target.SetActive(active);
        }

        // 0x06003958
        private void SetComponentActive(int index, bool active)
        {
            MonoBehaviour target = m_components[index];
            if (target == null) return;
            target.enabled = active;
        }
    }
}
