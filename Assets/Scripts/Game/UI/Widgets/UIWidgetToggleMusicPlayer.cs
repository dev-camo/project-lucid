using Hardlight;
using Hardlight.UI.Binding;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original Game.Runtime 020009d6, four methods and two fields.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetToggleMusicPlayer : UIWidget
    {
        [SerializeField] private UIToggle m_toggle;
        public readonly Bindable<bool> LockValue = new Bindable<bool>();

        // Original 060038a1.
        public UIToggle Toggle => m_toggle;

        // Original 060038a2: virtual interactability setter before notification.
        public void Lock()
        {
            m_toggle.Interactable = false;
            LockValue.Value = true;
        }

        // Original 060038a3: reread the toggle after its setter, then transition.
        public void Unlock()
        {
            m_toggle.Interactable = true;
            m_toggle.Transition.QueueTransitionToState(UITransitionState.Normal);
            LockValue.Value = false;
        }

        // Original 060038a4: the binder initializer precedes the genuine base call.
    }
}
