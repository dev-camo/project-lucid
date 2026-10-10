using Hardlight.UI.Binding;
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Complete original Game.Runtime 02000016, three methods and two fields.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetButtonMusic : UIWidgetButton
    {
        [SerializeField] private Button m_button;
        public readonly Bindable<bool> LockValue = new Bindable<bool>();

        // Original 0600003d: the button changes before the binder notification.
        public void Lock()
        {
            m_button.interactable = false;
            LockValue.Value = true;
        }

        // Original 0600003e: preserve the same prefix if a binder callback faults.
        public void Unlock()
        {
            m_button.interactable = true;
            LockValue.Value = false;
        }

        // Original 0600003f: the binder initializer precedes the genuine base call.
    }
}
