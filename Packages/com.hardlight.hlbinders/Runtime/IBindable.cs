using System;

namespace Hardlight.UI.Binding
{
    // HLBinders.Runtime 0x0200001a: four original abstract contracts, no bodies.
    public interface IBindable
    {
        object GetValue();
        void AddListener(Action action);
        void RemoveListener(Action action);
        Type GetBindableType();
    }
}
