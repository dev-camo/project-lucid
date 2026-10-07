using System;
using System.Collections;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Complete original HLUnityCore.Runtime02000249: five owner APIs and three
    // genuine coroutine records with six APIs each. Generated token/layout binding
    // remains separate from recovering the original state-machine behavior.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class TimeScaledUtilities
    {
        // Original04000783/84 and06000ecc: retain BeforeFieldInit and this order.
        private static readonly SystemRef<TimeManager> s_timeManagerRef =
            ProcessManager.GetSystemRef<TimeManager>(null, true);
        private static readonly WaitForFixedUpdate s_waitForFixedUpdate = new WaitForFixedUpdate();

        // Original06000ec8: the existing genuine CoroutineUtils host starts the
        // nested iterator. No host creation or TimeManager wait occurs here.
        public static Coroutine DelayFixedSeconds(float waitSeconds, TimeCategoryObject timeCategory,
            Action action)
        {
            return CoroutineUtils.RunCoroutine(DelayFixedSecondsCoroutine(waitSeconds, timeCategory, action));
        }

        // Original06000ec9 and generated06000ecd..ed2: always yield the real wait
        // iterator once, then invoke the action without a null guard. Callback
        // exceptions propagate after the original iterator becomes terminal.
        private static IEnumerator DelayFixedSecondsCoroutine(float waitSeconds,
            TimeCategoryObject timeCategory, Action action)
        {
            yield return WaitForFixedSeconds(waitSeconds, timeCategory);
            action();
        }

        // Original06000eca and generated06000ed3..ed8. TryGet runs once at the
        // first MoveNext, before testing duration or earlyOut. An absent manager
        // completes immediately. The captured manager is retained across yields.
        public static IEnumerator WaitForFixedSeconds(float waitSeconds, TimeCategoryObject timeCategory,
            Func<bool> earlyOut = null)
        {
            if (!s_timeManagerRef.TryGet(out TimeManager timeManager))
                yield break;
            float timer = 0f;
            bool hasEarlyOut = earlyOut != null;
            while (timer < waitSeconds)
            {
                if (hasEarlyOut && earlyOut())
                    yield break;
                // The timer advances before yielding, including a final overshoot.
                // Snapshot timer, then Unity time, then the real category timescale.
                timer += Time.fixedDeltaTime * timeManager.GetTimescale(timeCategory);
                yield return s_waitForFixedUpdate;
            }
        }

        // Original06000ecb and generated06000ed9..ede. The same original loop
        // uses frame delta time and yields null. Both architectures stop on NaN
        // through the ordered timer<duration gate; earlyOut is short-circuited.
        public static IEnumerator WaitForSeconds(float waitSeconds, TimeCategoryObject timeCategory,
            Func<bool> earlyOut = null)
        {
            if (!s_timeManagerRef.TryGet(out TimeManager timeManager))
                yield break;
            float timer = 0f;
            bool hasEarlyOut = earlyOut != null;
            while (timer < waitSeconds)
            {
                if (hasEarlyOut && earlyOut())
                    yield break;
                timer += Time.deltaTime * timeManager.GetTimescale(timeCategory);
                yield return null;
            }
        }
    }
}
