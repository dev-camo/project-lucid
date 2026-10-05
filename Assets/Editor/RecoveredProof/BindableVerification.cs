using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class BindableVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new InvalidOperationException(label);
        }
        private static object Field<T>(Bindable<T> value, string name) => typeof(Bindable<T>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static bool Initialised<T>(Bindable<T> value) => (bool)Field(value, "initialised");
        private sealed class Compare<T> : IEqualityComparer<T>
        {
            public int Calls;
            public T Left, Right;
            public Func<T,T,bool> Test;
            public bool Equals(T left, T right) { Calls++; Left = left; Right = right; return Test(left, right); }
            public int GetHashCode(T value) => 0;
        }
        // Full framework notification fixtures observe accessor order/failure;
        // they are not game DTOs, engine objects or stand-in game managers.
        private sealed class Notify : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler handlers;
            public readonly List<string> Events = new List<string>();
            public bool ThrowAdd, ThrowRemove;
            public int Subscribers => handlers == null ? 0 : handlers.GetInvocationList().Length;
            public event PropertyChangedEventHandler PropertyChanged
            {
                add { Events.Add("add"); if (ThrowAdd) throw new FormatException(); handlers += value; }
                remove { Events.Add("remove"); if (ThrowRemove) throw new FormatException(); handlers -= value; }
            }
            public void Raise(object sender, PropertyChangedEventArgs args) => handlers?.Invoke(sender, args);
        }
        private sealed class VirtualInt : Bindable<int>
        {
            public int Reads, Offset;
            public VirtualInt(int value) : base(value) { }
            public override int Value { get { Reads++; return base.Value + Offset; } set => base.Value = value; }
            public void Force(int value) => base.SetValue(value);
        }

        public static void Run() => Debug.Log("Project Lucid Bindable native-derived checks=" + RunManaged());
        public static int RunManaged()
        {
            checks = 0;
            Type definition = typeof(Bindable<>);
            const BindingFlags own = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Check(definition.FullName == "Hardlight.UI.Binding.Bindable`1" && definition.IsPublic && !definition.IsSealed && !definition.IsAbstract, "original open class surface");
            Check(definition.GetGenericArguments()[0].GenericParameterAttributes == GenericParameterAttributes.None && definition.GetGenericArguments()[0].GetGenericParameterConstraints().Length == 0, "original unconstrained T");
            Check(definition.GetInterfaces().Length == 1 && definition.GetInterfaces()[0] == typeof(IBindable), "exact original interface");
            FieldInfo[] fields = definition.GetFields(own);
            string[] names = { "value", "initialised", "boundTo", "propertyChange", "onValueChanged", "onValueChangedList", "useList", "customComparer" };
            Check(fields.Length == names.Length, "full eight-field graph");
            for (int i = 0; i < fields.Length; i++) Check(fields[i].Name == names[i] && fields[i].IsPrivate && !fields[i].IsStatic && !fields[i].IsInitOnly && fields[i].GetCustomAttributes(false).Length == 0, "field identity/order/attrs " + i);
            Check(definition.GetMethods(own).Length == 13 && definition.GetConstructors(own).Length == 4 && definition.GetProperties(own).Length == 1 && definition.GetEvents(own).Length == 0, "complete seventeen own methods");
            var options = (Il2CppSetOptionAttribute[])definition.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false);
            Check(options.Length == 2 && options[0].Option == Option.NullChecks && !(bool)options[0].Value && options[1].Option == Option.ArrayBoundsChecks && !(bool)options[1].Value, "original option order/payload");
            Check(typeof(IBindable).GetMethods().Length == 4 && typeof(IBindable).GetFields().Length == 0, "four genuine abstract contracts");
            Check(definition.GetProperty("Value").GetMethod.IsVirtual && definition.GetProperty("Value").SetMethod.IsVirtual && definition.GetMethod("SetValue", own).IsFamily && definition.GetMethod("SetValue", own).IsVirtual, "original virtual/property/protected surface");

            var plain = new Bindable<int>();
            Check(plain.Value == 0 && !Initialised(plain) && Field(plain,"boundTo") == null && Field(plain,"propertyChange") == null && Field(plain,"onValueChanged") == null && Field(plain,"onValueChangedList") == null && !(bool)Field(plain,"useList") && Field(plain,"customComparer") == null, "full original constructor defaults");
            var compare = new Compare<int> { Test = (left,right) => left == right };
            foreach (Bindable<int> item in new[] { new Bindable<int>(4), new Bindable<int>(compare), new Bindable<int>(4,compare) }) Check(!Initialised(item) && Field(item,"propertyChange") == null, "all constructor initialisation defaults");
            Check(new Bindable<int>(4).Value == 4 && new Bindable<int>(compare).Value == 0 && ReferenceEquals(Field(new Bindable<int>(4,compare),"customComparer"),compare), "constructor stored value/comparer");
            int calls = 0;
            plain.AddListener(() => calls++);
            plain.Value = 0;
            Check(calls == 1 && Initialised(plain), "first equal default assignment still notifies");
            plain.Value = 0; Check(calls == 1, "subsequent equal default assignment suppressed");
            plain.Value = 4; Check(calls == 2 && plain.Value == 4, "changed primitive assignment notifies");
            plain.MarkChanged(); Check(calls == 3 && Initialised(plain), "MarkChanged bypasses equality");
            Check((int)((IBindable)plain).GetValue() == 4 && plain.GetBindableType() == typeof(int), "boxed value and exact typeof T");
            Throws<NullReferenceException>(() => { int value = (Bindable<int>)null; }, "null implicit conversion is unguarded");

            calls = 0;
            var custom = new Bindable<int>(5,compare);
            custom.AddListener(() => calls++);
            custom.Value = 5;
            Check(compare.Calls == 0 && calls == 1, "first assignment bypasses custom comparer");
            custom.Value = 8;
            Check(compare.Calls == 1 && compare.Left == 5 && compare.Right == 8 && calls == 2, "custom comparer old/new operand order");
            compare.Test = (left,right) => throw new FormatException();
            Throws<FormatException>(() => custom.Value = 9, "comparer failure propagates");
            Check(custom.Value == 8 && calls == 2, "comparer failure leaves old value/listeners");
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -0.0f })
            {
                var floating = new Bindable<float>(value); int count = 0; floating.AddListener(() => count++);
                floating.Value = value; floating.Value = value;
                Check(count == 1, "EqualityComparer<float> equality " + value);
            }
            var zeros = new Bindable<float>(); calls = 0; zeros.AddListener(() => calls++); zeros.Value = 0.0f; zeros.Value = -0.0f;
            Check(calls == 1 && BitConverter.ToInt32(BitConverter.GetBytes(zeros.Value),0) == 0, "equal signed zero suppresses replacement");
            var large = new Bindable<long>(long.MinValue); large.Value = long.MaxValue;
            Check((long)large.GetValue() == long.MaxValue, "Int64 value/boxing width");
            var boolean = new Bindable<bool>(); calls = 0; boolean.AddListener(() => calls++); boolean.Value = false; boolean.Value = false;
            Check(calls == 1 && (bool)boolean.GetValue() == false, "Boolean variant first-change/comparer/boxing");
            Vector3 zero = new Vector3(0,0,0), near = new Vector3(0.000001f,0,0);
            Check(zero == near && !EqualityComparer<Vector3>.Default.Equals(zero,near), "real Vector3 operator equality differs from default comparer");
            var vector = new Bindable<Vector3>(); calls = 0; vector.AddListener(() => calls++); vector.Value = zero; vector.Value = near;
            Check(calls == 2 && vector.Value.x == near.x, "Vector3 variant uses exact default comparer, not approximate operator");
            var text = new Bindable<string>(); calls = 0; text.AddListener(() => calls++); text.Value = new string('x',1); text.Value = new string('x',1);
            Check(calls == 1, "reference default comparer uses string value equality");

            var listeners = new Bindable<int>(); var log = new List<string>();
            Action second = () => log.Add("second"), third = () => log.Add("third"); bool once = false;
            Action first = () => { log.Add("first"); if (!once) { once = true; listeners.RemoveListener(second); listeners.AddListener(third); } };
            listeners.AddListener(first); listeners.AddListener(second); listeners.MarkChanged();
            Check(String.Join(",",log) == "first,second", "dispatch snapshots removed listener and excludes added listener");
            log.Clear(); listeners.MarkChanged(); Check(String.Join(",",log) == "first,third", "next dispatch uses current listeners");
            Check((bool)Field(listeners,"useList") && Field(listeners,"onValueChanged") == null, "second registration permanently enters list mode");
            var scalar = new Bindable<int>(); calls = 0; scalar.AddListener(() => calls++); scalar.RemoveListener(() => calls += 10); scalar.MarkChanged();
            Check(calls == 0, "single-mode removal clears unrelated callback");
            scalar.AddListener(null); scalar.MarkChanged(); Check(!(bool)Field(scalar,"useList"), "null single registration stays empty");
            var duplicates = new Bindable<int>(); calls = 0; Action duplicate = () => calls++;
            duplicates.AddListener(duplicate); duplicates.AddListener(duplicate); duplicates.MarkChanged(); Check(calls == 2, "duplicates retained");
            duplicates.RemoveListener(duplicate); duplicates.MarkChanged(); Check(calls == 3, "list removes first matching duplicate only");
            duplicates.RemoveListener(duplicate); duplicates.MarkChanged(); Check(calls == 3 && (bool)Field(duplicates,"useList"), "empty list mode never collapses");
            duplicates.AddListener(null); Throws<NullReferenceException>(() => duplicates.MarkChanged(), "null list entry is invoked");
            var throwing = new Bindable<int>(); throwing.AddListener(() => throw new FormatException());
            Throws<FormatException>(() => throwing.Value = 4, "listener failure interrupts first set tail");
            Check(throwing.Value == 4 && !Initialised(throwing), "stored value retained before initialization on failure");
            throwing.RemoveListener(null); throwing.Value = 4; Check(Initialised(throwing), "retry equal value remains a first assignment");

            var parent = new Bindable<int>(1); var target = new Bindable<int>(7); calls = 0; parent.AddListener(() => calls++);
            parent.BindTo(target); Check(parent.Value == 7 && calls == 1 && !Initialised(parent), "BindTo reads target and fires immediately without init");
            parent.Value = 8; Check(parent.Value == 7 && target.Value == 7 && calls == 1, "bound setter ignores rather than forwards");
            parent.BindTo(target); Check(calls == 1, "same binding identity no-op");
            target.Value = 7; Check(calls == 2 && parent.Value == 7, "target first equal change forwards MarkChanged");
            parent.Unbind(); Check(parent.Value == 1 && calls == 3 && Field(target,"onValueChanged") == null, "Unbind restores retained local value/removes target listener");
            parent.Unbind(); Check(calls == 3, "unbound-to-null no-op");
            var virtualValue = new VirtualInt(4) { Offset = 10 };
            Check((int)virtualValue.GetValue() == 14 && (int)virtualValue == 14 && virtualValue.Reads == 2, "GetValue/implicit use virtual Value");
            virtualValue.BindTo(target); virtualValue.Force(99);
            Check(virtualValue.Value == 17 && (int)Field(virtualValue,"value") == 99, "protected SetValue stores own data while getter remains bound");
            virtualValue.Unbind(); Check(virtualValue.Value == 109, "unbound virtual getter uses retained own data");

            var oldValue = new Notify(); var newValue = new Notify(); var notified = new Bindable<Notify>(oldValue); calls = 0;
            notified.AddListener(() => calls++);
            Check(oldValue.Subscribers == 0, "default-valued constructor does not subscribe");
            notified.Value = oldValue; Check(oldValue.Subscribers == 1 && calls == 1, "first assignment subscribes after listener");
            oldValue.Raise(null,null); Check(calls == 2, "notification sender/args ignored including null");
            oldValue.Raise(new object(),new PropertyChangedEventArgs("unrelated")); Check(calls == 3, "any property forwards notification");
            notified.Value = oldValue; Check(oldValue.Events.Count == 1 && calls == 3, "equal existing notification model neither unsubscribes nor fires");
            bool before = false;
            notified.RemoveListener(null);
            notified.AddListener(() => before = oldValue.Subscribers == 0 && newValue.Subscribers == 0 && ReferenceEquals(notified.Value,newValue) && ReferenceEquals(Field(notified,"propertyChange"),oldValue));
            notified.Value = newValue;
            Check(before && oldValue.Subscribers == 0 && newValue.Subscribers == 1, "old removal/store/fire/new subscription ordering");
            oldValue.ThrowRemove = false; newValue.ThrowRemove = true;
            Throws<FormatException>(() => notified.Value = oldValue, "unsubscribe exception precedes store");
            Check(ReferenceEquals(notified.Value,newValue) && newValue.Subscribers == 1, "unsubscribe failure retains old state");
            newValue.ThrowRemove = false; oldValue.ThrowAdd = true;
            Throws<FormatException>(() => notified.Value = oldValue, "subscribe exception follows store/fire/init");
            Check(ReferenceEquals(notified.Value,oldValue) && Initialised(notified) && ReferenceEquals(Field(notified,"propertyChange"),oldValue) && newValue.Subscribers == 0 && oldValue.Subscribers == 0, "subscribe failure retains new stored/property state");

            var outer = new Notify(); var inner = new Notify(); var reentrant = new Bindable<Notify>(); bool entered = false, sawUninitialized = false;
            reentrant.AddListener(() => { if (!entered) { entered = true; sawUninitialized = !Initialised(reentrant); reentrant.Value = inner; } });
            reentrant.Value = outer;
            Check(sawUninitialized && ReferenceEquals(reentrant.Value,inner) && Initialised(reentrant), "listener reentry sees pre-initialized tail and replaces stored value");
            Check(ReferenceEquals(Field(reentrant,"propertyChange"),outer) && outer.Subscribers == 1 && inner.Subscribers == 1, "outer tail subscribes incoming value, preserving nested subscription");
            return checks;
        }
    }
}
