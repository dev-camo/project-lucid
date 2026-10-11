// Preserved Sonic Dream Team 1.10.1, HLUnityUI.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x060004c7 0x1b69954, 0x1b60e60
// 0x060004c8 0x1b69a98, 0x1b60f90
// 0x060004c9 0x1b69aa0, 0x1b60fa0
// 0x060004ca 0x1b69a08, 0x1b60f00
// 0x060004cb 0x1b69b50, 0x1b61030
// 0x060004cc 0x1b69b24, 0x1b61010
// 0x060004cd 0x1b69bac, 0x1b61080
// 0x060004ce 0x1b69bb0, 0x1b61090
// 0x060004cf 0x1b69dc8, 0x1b612a0
// 0x060004d0 0x1b69dd0, 0x1b612b0
// 0x060004d1 0x1b69e10, 0x1b612f0
// Original bloom preserves strict float loops, update-before-yield timing,
// and a mutable stall at the last rising sample. It neither forces peak alpha
// nor resets alpha at entry. FinishTransition only releases the stall.
// Awake performs the genuine singleton base call before component acquisition.
// The constructor stores max alpha 1 before the original generic base call.
// Original C# interpolation spelling is inferred from the inline clamped
// linear arithmetic; native floating/fault and future Engine parity remain held.
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(CanvasGroup))]
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(Canvas))]
    public class MenuBloomTransition : MonoSingleton<MenuBloomTransition>
    {
        [SerializeField]
        private float m_maxAlpha = 1f;
        private CanvasGroup m_canvasGroup;
        private bool m_enabled;
        private bool m_stalled;

        public void StartTransition(float halfDuration, bool stopAtWhite = false)
        {
            if (!m_enabled)
            {
                StartCoroutine(DoTransition(halfDuration, stopAtWhite));
            }
        }

        public void FinishTransition()
        {
            m_stalled = false;
        }

        protected override void Awake()
        {
            base.Awake();
            m_canvasGroup = GetComponent<CanvasGroup>();
        }

        private IEnumerator DoTransition(float halfDuration, bool stopAtWhite)
        {
            m_enabled = true;
            float transitionTime = 0f;
            m_stalled = stopAtWhite;
            while (transitionTime < halfDuration)
            {
                m_canvasGroup.alpha = Mathf.Lerp(0f, m_maxAlpha, transitionTime / halfDuration);
                transitionTime += Time.deltaTime;
                yield return null;
            }

            while (m_stalled)
            {
                yield return null;
            }

            while (transitionTime > 0f)
            {
                m_canvasGroup.alpha = Mathf.Lerp(0f, m_maxAlpha, transitionTime / halfDuration);
                transitionTime -= Time.deltaTime;
                yield return null;
            }

            m_canvasGroup.alpha = 0f;
            m_enabled = false;
        }

        public MenuBloomTransition()
        {
        }
    }
}
