using System.Collections;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a0e: eases the bound colour towards each new
    // target, restarting from the current value when the target changes.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LerpColour : MonoBehaviour
    {
        [SerializeField]
        private float m_lerpSpeedMultiplier = 1f;
        [SerializeField]
        [Tooltip("First non-empty target colour assigned will be directly set rather than lerped to.")]
        private bool m_setFirstValue;
        public readonly Bindable<Color> Colour = new Bindable<Color>();
        private Coroutine m_lerpRoutine;
        private bool m_valueAssigned;

        // Color's == is approximate, so a target within 1e-5 of the current
        // value is ignored.
        public void SetTargetColour(Color targetColour)
        {
            if (Colour.Value == targetColour) return;
            if (m_setFirstValue && !m_valueAssigned)
            {
                m_valueAssigned = true;
                Colour.Value = targetColour;
                return;
            }
            this.SafeStopCoroutine(ref m_lerpRoutine);
            m_lerpRoutine = StartCoroutine(LerpStartColour(Colour.Value, targetColour));
            m_valueAssigned = true;
        }

        private IEnumerator LerpStartColour(Color startColour, Color targetColour)
        {
            float lerpValue = 0f;
            while (lerpValue < 1f)
            {
                lerpValue += Time.deltaTime * m_lerpSpeedMultiplier;
                Colour.Value = Color.Lerp(startColour, targetColour, lerpValue);
                yield return null;
            }
            Colour.Value = targetColour;
        }
    }
}
