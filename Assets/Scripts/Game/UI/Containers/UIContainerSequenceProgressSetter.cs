using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000991; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSequenceProgressSetter : UIContainerValueSetter
    {
        [SerializeField]
        private SequencedEffect m_sequencedEffect;

        // 0600374a (1.10.1 RVA 0x696E40): the effect is only read while the container is open.
        private void Update()
        {
            UIContainer?.SetProgress(m_sequencedEffect.Progress);
        }

        // 0600374b (1.10.1 RVA 0x696F50)
        public UIContainerSequenceProgressSetter()
        {
        }
    }
}
