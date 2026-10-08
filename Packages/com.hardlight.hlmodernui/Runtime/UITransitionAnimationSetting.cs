using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime 02000034, complete original getter and base-only constructor.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UITransitionAnimationSetting : UITransitionSettingBase
    {
        [SerializeField] private AnimationClip m_animationClip;
        public AnimationClip AnimationClip => m_animationClip;
        public UITransitionAnimationSetting() { }
    }
}
