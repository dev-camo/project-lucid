using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000628.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SequencedPromptTrigger : SequencedEffect
    {
        [SerializeField] private PromptTrigger m_promptTrigger;

        // 0x060020f6 (x86-64 0x56a960): a prompt that is shown completes the
        // effect when it is hidden; one that is refused completes it at once.
        // The completion callback is invoked unguarded.
        protected override void OnEffectTriggered(IEffectData data)
        {
            if (m_promptTrigger.TryShowWithResult())
            {
                m_promptTrigger.OnHiddenAction += OnPromptHidden;
                return;
            }
            m_onComplete();
        }

        // 0x060020f7: hidden callbacks of other triggers are ignored. Otherwise
        // the subscription is dropped and the effect completes.
        private void OnPromptHidden(PromptTrigger promptTrigger)
        {
            if (promptTrigger != m_promptTrigger) return;
            m_promptTrigger.OnHiddenAction -= OnPromptHidden;
            m_onComplete();
        }

        // 0x060020f8.
        private void Reset()
        {
            m_promptTrigger = GetComponent<PromptTrigger>();
        }

        // 0x060020f9.
        public SequencedPromptTrigger() { }
    }
}
