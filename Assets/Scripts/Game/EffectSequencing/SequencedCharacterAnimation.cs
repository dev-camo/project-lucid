using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000613 and its private TriggerType enum (02000614);
    // bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SequencedCharacterAnimation : SequencedEffect
    {
        [SerializeField] private AnimationParameterWrapper m_animationParameter;
        [SerializeField] private TriggerType m_animationTriggerType;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        // 0x060020a7 (0x568630). Without a resolvable parameter hash or a current
        // character nothing happens and the completion callback is not invoked.
        protected override void OnEffectTriggered(IEffectData data)
        {
            if (!m_animationParameter.TryGetAnimationHash(out int hash)
                || !m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                return;
            switch (m_animationTriggerType)
            {
                case TriggerType.BoolTrue:
                    character.Animator.SetAnimation(hash, true);
                    break;
                case TriggerType.BoolFalse:
                    character.Animator.SetAnimation(hash, false);
                    break;
                case TriggerType.Trigger:
                    character.Animator.SetAnimationTrigger(hash);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            m_onComplete?.Invoke();
        }

        private enum TriggerType
        {
            BoolTrue = 0,
            BoolFalse = 1,
            Trigger = 2
        }
    }
}
