using System;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ActorAnimator
    {
        private Animator m_animator;

        // Original 06000345 activates the supplied animator's GameObject after
        // storing it, without a null guard.
        public ActorAnimator(Animator animator)
        {
            m_animator = animator;
            m_animator.gameObject.SetActive(true);
        }

        public void SetAnimation(int id, float value)
        {
            if (m_animator == null)
                return;
            m_animator.SetFloat(id, value);
        }

        public void SetAnimation(int id, bool value)
        {
            if (m_animator == null)
                return;
            m_animator.SetBool(id, value);
        }

        public void SetAnimationTrigger(int id)
        {
            if (m_animator == null)
                return;
            m_animator.SetTrigger(id);
        }

        public void ResetAnimationTrigger(int id)
        {
            if (m_animator == null)
                return;
            m_animator.ResetTrigger(id);
        }

        public void TrySetFromAnimationTypeTrigger(ActorAnimationDefinition animationDefinition,
            ActorAnimationType animationType) =>
            IterateAnimationHashes(animationDefinition, animationType, SetAnimationTrigger);

        public void TryResetFromAnimationTypeTrigger(ActorAnimationDefinition animationDefinition,
            ActorAnimationType animationType) =>
            IterateAnimationHashes(animationDefinition, animationType, ResetAnimationTrigger);

        public void TrySetFromAnimationType(ActorAnimationDefinition animationDefinition,
            ActorAnimationType animationType, bool value) =>
            IterateAnimationHashes(animationDefinition, animationType,
                hashValue => SetAnimation(hashValue, value));

        public void TrySetFromAnimationType(ActorAnimationDefinition animationDefinition,
            ActorAnimationType animationType, float value) =>
            IterateAnimationHashes(animationDefinition, animationType,
                hashValue => SetAnimation(hashValue, value));

        // Original 0600034e gates the definition alone; its caller already
        // constructed the real delegate before this Unity.Object comparison.
        private void IterateAnimationHashes(ActorAnimationDefinition animationDefinition,
            ActorAnimationType animationType, Action<int> iterator)
        {
            if (animationDefinition == null)
                return;
            animationDefinition.IterateAnimationHashesForType(animationType, iterator);
        }

        public void Rebind()
        {
            if (m_animator != null)
                m_animator.Rebind();
        }

        // Original 06000350 restores authored parameter defaults, using names
        // rather than hashes. Unknown parameter kinds are ignored in this path.
        public void ResetAllParameters()
        {
            foreach (AnimatorControllerParameter parameter in m_animator.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        m_animator.SetFloat(parameter.name, parameter.defaultFloat);
                        break;
                    case AnimatorControllerParameterType.Int:
                        m_animator.SetInteger(parameter.name, parameter.defaultInt);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        m_animator.SetBool(parameter.name, parameter.defaultBool);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        m_animator.ResetTrigger(parameter.name);
                        break;
                }
            }
        }

        public void Close()
        {
            if (m_animator != null)
                m_animator.gameObject.SetActive(false);
        }

        public bool IsAnimator(Animator animator) => m_animator == animator;
        public bool GetAnimatorBool(string name) => m_animator.GetBool(name);
        // Original 06000354 has a genuine RET body.
        public virtual void OnUpdate(Actor actor) { }

        public void SetAnimationTrigger(string trigger)
        {
            if (m_animator == null)
                return;
            m_animator.SetTrigger(trigger);
        }

        // Original 06000356 separates active and inactive parameters. Trigger
        // values use GetBool too; unknown kinds throw in this diagnostic path.
        public void GetDebugInfo(StringBuilder stringInfoBuilder, bool activeParameters)
        {
            foreach (AnimatorControllerParameter parameter in m_animator.parameters)
            {
                string valueInfo;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        float floatValue = m_animator.GetFloat(parameter.name);
                        if (MathUtilities.WithinTolerance(floatValue, 0f, 0.0001f) == activeParameters)
                            continue;
                        valueInfo = string.Format("{0}", floatValue);
                        break;
                    case AnimatorControllerParameterType.Int:
                        int intValue = m_animator.GetInteger(parameter.name);
                        if ((intValue != 0) != activeParameters)
                            continue;
                        valueInfo = string.Format("{0}", intValue);
                        break;
                    case AnimatorControllerParameterType.Bool:
                    case AnimatorControllerParameterType.Trigger:
                        bool boolValue = m_animator.GetBool(parameter.name);
                        if (boolValue != activeParameters)
                            continue;
                        valueInfo = string.Format("{0}", boolValue);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                stringInfoBuilder.AppendLine(string.Format("{0}, Type = {1}, Value = {2}",
                    parameter.name, parameter.type, valueInfo));
            }
        }
    }
}
