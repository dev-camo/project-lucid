using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000837; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenControlGroupListenerButton : OnScreenControlGroupListener
    {
        [SerializeField] private OnScreenButton m_onScreenButton;

        // 06002fad (1.10.1 RVA 0x6085A0): the base hide waits until the button has finished any held press.
        protected override void Hide(Action completed)
        {
            m_onScreenButton.DisableWhenReady(() => base.Hide(completed));
        }

        // 06002fae (RVA 0x608680)
        public OnScreenControlGroupListenerButton()
        {
        }
    }
}
