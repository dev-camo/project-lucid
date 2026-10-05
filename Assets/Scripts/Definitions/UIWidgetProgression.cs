using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetProgression : UIWidget
    {
        [SerializeField] private Animation m_animation;
        [SerializeField] private AnimationClip m_animateInClip;
        [SerializeField] private AnimationClip m_animateOutClip;
        public Action<UIWidgetProgression> AnimateOutComplete;

        // 0x06003823; ARM64 0x67aad0. The original constructor allocates the
        // default Bindable<Texture> before forwarding to the genuine UIWidget.
        public readonly Bindable<Texture> MaskedTexture = new Bindable<Texture>();

        // 0x0600381f; ARM64 0x67aa3c. Virtual Bindable<Texture>.Value setter.
        public void SetMaskedImage(Texture texture) => MaskedTexture.Value = texture;

        // 0x06003820; ARM64 0x67aa4c. Reads the configured clip name, then
        // invokes Animation.Play(string); no clip/component null guards.
        public void AnimateIn() => m_animation.Play(m_animateInClip.name);

        // 0x06003821; ARM64 0x67aa7c. Same direct path with the out clip.
        public void AnimateOut() => m_animation.Play(m_animateOutClip.name);

        // 0x06003822; ARM64 0x67aab0. Captures the field once and invokes the
        // original callback with this widget; callback exceptions propagate.
        public void Action_AnimateOutComplete() => AnimateOutComplete?.Invoke(this);
    }
}
