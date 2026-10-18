using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009fe and its iterator <DelayedDestroy>d__6 (0x020009ff);
    // bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class DestroyObject : MonoBehaviour
    {
        [SerializeField] private bool m_onAwake;
        [SerializeField] private float m_delaySeconds;
        [SerializeField]
        [HideIf("m_delaySeconds", 0f)]
        private TimeCategory m_timeCategory = TimeCategory.Environment;
        private Coroutine m_delayedDestroyRoutine;

        // 0x06003964
        private void Awake()
        {
            if (m_onAwake) Destroy();
        }

        // 0x06003965 (0x6B05D0): a second request while the delayed destroy is pending
        // destroys at once. The routine field is never cleared.
        public void Destroy()
        {
            if (m_delaySeconds > 0f && m_delayedDestroyRoutine == null)
                m_delayedDestroyRoutine = StartCoroutine(DelayedDestroy());
            else
                Destroy(gameObject);
        }

        // 0x06003966, MoveNext 0x0600396a
        private IEnumerator DelayedDestroy()
        {
            yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(m_delaySeconds, m_timeCategory);
            Destroy(gameObject);
        }
    }
}
