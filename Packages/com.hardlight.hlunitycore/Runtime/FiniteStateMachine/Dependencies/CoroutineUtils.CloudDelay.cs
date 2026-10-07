using System;
using System.Collections;
using UnityEngine;

namespace Hardlight.Utils
{
    public partial class CoroutineUtils
    {
        // Original 0x0600117c, ARM 0x1b3863c. Reuses the supplied wait object.
        public static Coroutine Delay(Action action, WaitForSeconds waitForSeconds)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.DelayCoroutine(action, waitForSeconds));
        }

        // Original 0x0600117d, natural <DelayCoroutine>d__14. No wait allocation.
        private IEnumerator DelayCoroutine(Action action, WaitForSeconds waitForSeconds)
        {
            yield return waitForSeconds;
            action();
        }
    }
}
