using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.Localisation
{
    // Original HLLocalisation.Runtime; bodies translated from the 1.10.1 x86-64 binary.
    // Reverses the child order once on Awake for right-to-left languages.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ReverseOrderOnLanguage : MonoBehaviour
    {
        // 0600003c
        private void Awake()
        {
            Evaluate();
        }

        // 0600003d
        private void ReverseChildren()
        {
            for (int i = 1; i < transform.childCount; i++)
                transform.GetChild(0).SetSiblingIndex(transform.childCount - i);
        }

        // 0600003e
        private void Evaluate()
        {
            if (StringTable.GetCurrentLanguageData() != null && StringTable.GetCurrentLanguageData().IsRTL)
                ReverseChildren();
        }
    }
}
