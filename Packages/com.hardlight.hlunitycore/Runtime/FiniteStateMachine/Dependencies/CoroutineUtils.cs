using System;
using System.Collections;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Utils
{
    // Original coroutine host lifecycle/start/stop and next-frame subset. Other
    // scheduled utility callbacks and delay/predicate iterators remain unresolved.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public partial class CoroutineUtils : MonoBehaviour, ISystem
    {
        private static CoroutineUtils s_instance;

        // HLUnityCore.Runtime.dll:Hardlight.Utils.CoroutineUtils:0x06001190;
        // arm64 0x1b39358. No lazy host construction occurs.
        [Obsolete("Instance is obsolete and only exists for backwards compatability. Please use the public static methods instead.")]
        public static CoroutineUtils Instance => s_instance;

        // Original token 0x06001170; arm64 0x1b37ecc. Destroy only the
        // duplicate component; otherwise publish the host before registration.
        private void Awake()
        {
            if (s_instance != null) { Destroy(this); return; }
            s_instance = this;
            ProcessManager.RegisterSystem(this);
        }

        // Original token 0x06001171; arm64 0x1b38018. Every instance attempts
        // identity-based unregistration, including a destroyed duplicate. The
        // original leaves s_instance unchanged and relies on Unity null rules.
        private void OnDestroy() => ProcessManager.UnregisterSystem(this);

        // Original token 0x06001172; arm64 0x1b1f090.
        public static Coroutine RunCoroutine(IEnumerator coroutine) => s_instance.StartCoroutine(coroutine);

        // HLUnityCore.Runtime.dll:Hardlight.Utils.CoroutineUtils:0x06001175;
        // arm64 0x1b381c4. Capture the existing host before constructing the
        // iterator. An absent host fails here; this does not create one lazily.
        public static void OnNextFrame(Action action)
        {
            CoroutineUtils host = s_instance;
            host.StartCoroutine(host.NextFrameCoroutine(action));
        }

        // Original token 0x06001176; arm64 0x1b38278. MoveNext 0x060011b5
        // at 0x1b39bf0 yields null exactly once, then invokes the retained action
        // without a null guard. State is completed before invoking user code,
        // so reentrant MoveNext and a throwing callback cannot execute it again.
        private IEnumerator NextFrameCoroutine(Action action)
        {
            yield return null;
            action();
        }

        // Original token 0x06001181; arm64 0x1b38a20. An absent host leaves
        // the handle unchanged, as does a failed StopCoroutine call.
        public static void StopUtilCoroutine(ref Coroutine coroutine)
        {
            if (s_instance == null || coroutine == null) return;
            s_instance.StopCoroutine(coroutine);
            coroutine = null;
        }

        // Original token 0x06001194; arm64 0x1b39550 delegates MonoBehaviour.
        public CoroutineUtils() { }

        // HLUnityCore.Runtime:Hardlight.Utils.CoroutineUtils:0x06001179; arm64 0x1b383ac.
        // Original iterator 0x060011e9..11ee, MoveNext arm64 0x1b3a688.
        // Nonpositive counts invoke immediately on first MoveNext. The callback
        // runs after the state becomes terminal, without a null guard.
        public static IEnumerator WaitNumberOfFramesCoroutine(Action action, int numberOfFramesToWait)
        {
            int frameCount = 0;
            while (frameCount < numberOfFramesToWait)
            {
                yield return null;
                frameCount++;
            }
            action();
        }
    }
}
