using System.Collections;
using System.Globalization;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000842; bodies 0x60A4C0..0x60A930 (1.10.1 x86-64).
    // Counts a bindable number from one amount to another over a fixed time.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIAnimatingCounter : MonoBehaviour
    {
        [SerializeField]
        [Min(0.1f)]
        private float m_animationTimeSeconds = 2f;
        [SerializeField]
        private UnityEvent m_onStart;
        [SerializeField]
        private UnityEvent m_onEnd;
        public readonly Bindable<int> CurrentValue = new Bindable<int>();
        public readonly Bindable<string> CurrentValueString = new Bindable<string>();
        private int m_startAmount;
        private int m_endAmount;
        private float m_elapsedSeconds;
        private string m_outputStringFormat;
        private bool m_animateWhenEnabled;

        // 06002fe2: a zero duration keeps the current animation time.
        public void SetUp(int startAmount, int endAmount, string outputStringFormat, float durationSeconds = 0f)
        {
            StopAllCoroutines();
            m_startAmount = startAmount;
            m_endAmount = endAmount;
            m_elapsedSeconds = 0f;
            m_outputStringFormat = outputStringFormat;
            if (durationSeconds != 0f)
            {
                m_animationTimeSeconds = durationSeconds;
            }
            Refresh();
        }

        // 06002fe3: the string is rewritten only when the value changes or is still empty.
        private void Refresh()
        {
            int value = Mathf.RoundToInt(Mathf.Lerp(m_startAmount, m_endAmount, m_elapsedSeconds / m_animationTimeSeconds));
            if (CurrentValue.Value != value || string.IsNullOrEmpty(CurrentValueString.Value))
            {
                CurrentValue.Value = value;
                CurrentValueString.Value = string.IsNullOrEmpty(m_outputStringFormat)
                    ? value.ToString(CultureInfo.InvariantCulture)
                    : string.Format(m_outputStringFormat, value);
            }
        }

        // 06002fe4
        private void OnEnable()
        {
            if (m_animateWhenEnabled)
            {
                StopAllCoroutines();
                StartCoroutine(AnimateCoroutine());
            }
        }

        // 06002fe5: an inactive object cannot run coroutines, so it animates once enabled.
        public void StartAnimating()
        {
            StopAllCoroutines();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(AnimateCoroutine());
            }
            else
            {
                m_animateWhenEnabled = true;
            }
        }

        // 06002fe6 / MoveNext 06002fea
        private IEnumerator AnimateCoroutine()
        {
            m_animateWhenEnabled = false;
            m_elapsedSeconds = 0f;
            Refresh();
            m_onStart?.Invoke();
            yield return null;
            while (m_animationTimeSeconds > m_elapsedSeconds)
            {
                m_elapsedSeconds += Time.deltaTime;
                Refresh();
                yield return null;
            }
            m_elapsedSeconds = m_animationTimeSeconds;
            Refresh();
            m_onEnd?.Invoke();
        }
    }
}
