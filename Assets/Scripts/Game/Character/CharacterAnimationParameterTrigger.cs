using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000734. Fires animator triggers and a bool parameter as the
    // character crosses the volume. Native code inlines PlayTrigger and SetBool into the three
    // callbacks, and none of them calls the empty base callback.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAnimationParameterTrigger : CharacterTrigger
    {
        [SerializeField]
        private AnimationParameterWrapper m_onEnterTriggerParameter;
        [SerializeField]
        private AnimationParameterWrapper m_onStayTriggerParameter;
        [SerializeField]
        private AnimationParameterWrapper m_onExitTriggerParameter;
        [Tooltip("Set true on enter and false on exit.")]
        [SerializeField]
        private AnimationParameterWrapper m_boolParameter;

        // 060028a1
        protected override void DoTriggerEnter(Character character)
        {
            PlayTrigger(character, m_onEnterTriggerParameter);
            SetBool(character, m_boolParameter, true);
        }

        // 060028a2
        protected override void DoTriggerStay(Character character)
        {
            PlayTrigger(character, m_onStayTriggerParameter);
        }

        // 060028a3
        protected override void DoTriggerExit(Character character)
        {
            PlayTrigger(character, m_onExitTriggerParameter);
            SetBool(character, m_boolParameter, false);
        }

        // 060028a4
        private void PlayTrigger(Character character, AnimationParameterWrapper parameterWrapper)
        {
            if (parameterWrapper.TryGetAnimationHash(out int hash))
                character.Animator.SetAnimationTrigger(hash);
        }

        // 060028a5
        private void SetBool(Character character, AnimationParameterWrapper parameterWrapper, bool value)
        {
            if (parameterWrapper.TryGetAnimationHash(out int hash))
                character.Animator.SetAnimation(hash, value);
        }

        // 060028a6
        public CharacterAnimationParameterTrigger() { }
    }
}
