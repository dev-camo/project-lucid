using System;
using System.Collections;
using Hardlight.Utils;
using UnityEngine;

namespace Hardlight
{
    public static class MonoBehaviourExtensions
    {
        // HLUnityCore.Runtime:Hardlight.MonoBehaviourExtensions:0x060002c7; arm64 0x1a9d6e4.
        // A null handle skips even the receiver check. A failed engine stop retains
        // the supplied handle; clear it only after the original overload returns.
        public static void SafeStopCoroutine(this MonoBehaviour behaviour, ref Coroutine coroutine)
        {
            if (coroutine == null) return;
            behaviour.StopCoroutine(coroutine);
            coroutine = null;
        }

        // Original 0x060002c8; arm64 0x1ab56b8. Uses the IEnumerator overload.
        public static void SafeStopCoroutine(this MonoBehaviour behaviour, ref IEnumerator coroutine)
        {
            if (coroutine == null) return;
            behaviour.StopCoroutine(coroutine);
            coroutine = null;
        }

        // Original 0x060002c9 + compiler iterator 0x060002ca..2cf; arm64 0x1ab5704.
        // The receiver is unused and the action is passed to the genuine nested
        // two-frame iterator on first MoveNext, rather than run by this wrapper.
        public static IEnumerator WaitForUI(this MonoBehaviour _, Action action)
        {
            yield return CoroutineUtils.WaitNumberOfFramesCoroutine(action, 2);
        }
    }
}
