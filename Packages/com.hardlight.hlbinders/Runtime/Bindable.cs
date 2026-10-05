using System;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.UI.Binding
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class Bindable<T> : IBindable
    {
        private T value;
        private bool initialised;
        private Bindable<T> boundTo;
        private INotifyPropertyChanged propertyChange;
        private Action onValueChanged;
        private List<Action> onValueChangedList;
        private bool useList;
        private IEqualityComparer<T> customComparer;

        // HLBinders.Runtime 0x06000026/27: virtual reads follow a binding;
        // writes to a bound instance are ignored rather than forwarded.
        public virtual T Value
        {
            get => boundTo != null ? boundTo.Value : value;
            set { if (boundTo == null) SetValue(value); }
        }

        // 0x06000028..2b: stored defaults do not initialize comparison or
        // subscribe to property notifications until the first SetValue.
        public Bindable() { value = default(T); }
        public Bindable(T defaultValue) { value = defaultValue; }
        public Bindable(IEqualityComparer<T> comparer) { customComparer = comparer; value = default(T); }
        public Bindable(T defaultValue, IEqualityComparer<T> comparer) { value = defaultValue; customComparer = comparer; }

        // 0x0600002c: virtual Value dispatch, with boxing for value instances.
        public virtual object GetValue() => Value;

        // 0x0600002d: compare only after initialization. Store and notify
        // before updating initialization/subscription, including on reentry.
        protected virtual void SetValue(T value)
        {
            if (initialised)
            {
                if (customComparer != null)
                {
                    if (customComparer.Equals(Value, value)) return;
                }
                else if (EqualityComparer<T>.Default.Equals(Value, value)) return;
            }
            if (propertyChange != null) propertyChange.PropertyChanged -= OnPropertyChanged;
            this.value = value;
            FireChangeEvent();
            initialised = true;
            propertyChange = value as INotifyPropertyChanged;
            if (propertyChange != null) propertyChange.PropertyChanged += OnPropertyChanged;
        }

        // 0x0600002e: use a single Action until a second nonempty registration;
        // retain list mode afterward, including empty/null/duplicate entries.
        public void AddListener(Action action)
        {
            if (useList) onValueChangedList.Add(action);
            else if (onValueChanged != null)
            {
                useList = true;
                var listeners = new List<Action>();
                listeners.Add(onValueChanged);
                listeners.Add(action);
                onValueChangedList = listeners;
                onValueChanged = null;
            }
            else onValueChanged = action;
        }

        // 0x0600002f: single mode clears its callback regardless of argument.
        public void RemoveListener(Action action)
        {
            if (useList) onValueChangedList.Remove(action);
            else onValueChanged = null;
        }

        // 0x06000030/31.
        public void MarkChanged() => FireChangeEvent();
        public Type GetBindableType() => typeof(T);

        // 0x06000032: raw reference identity, old unsubscribe, target store,
        // new subscription, then immediate change event; local value retained.
        public void BindTo(Bindable<T> target)
        {
            if (boundTo == target) return;
            if (boundTo != null) boundTo.RemoveListener(MarkChanged);
            boundTo = target;
            if (boundTo != null) boundTo.AddListener(MarkChanged);
            FireChangeEvent();
        }

        // 0x06000033/34.
        public void Unbind() => BindTo(null);
        public static implicit operator T(Bindable<T> bindable) => bindable.Value;

        // 0x06000035: sender/property name are intentionally unused.
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => FireChangeEvent();

        // 0x06000036: snapshot before callbacks, including null entries;
        // callbacks added/removed during dispatch affect subsequent events.
        private void FireChangeEvent()
        {
            if (useList)
            {
                Action[] listeners = onValueChangedList.ToArray();
                for (int i = 0; i < listeners.Length; i++) listeners[i]();
            }
            else onValueChanged?.Invoke();
        }
    }
}
