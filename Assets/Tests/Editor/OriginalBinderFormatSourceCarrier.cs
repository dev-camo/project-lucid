using System;
using System.Collections.Generic;
using Hardlight.UI.Binding;
using UnityEngine;

// Owned binding source for the format and conversion node cases. Each member is
// declared as a genuine Bindable<T> so the original BindReference lookup accepts
// it; Recording instances log every Value read, by name, in a shared list.
public sealed class OriginalBinderFormatSourceCarrier : ScriptableObject
{
    [NonSerialized] public readonly List<string> Reads = new List<string>();
    [NonSerialized] public Bindable<int> Int;
    [NonSerialized] public Bindable<ulong> Ulong;
    [NonSerialized] public Bindable<float> Float;
    [NonSerialized] public Bindable<float> SecondFloat;
    [NonSerialized] public Bindable<float> ThirdFloat;
    [NonSerialized] public Bindable<float> FourthFloat;
    [NonSerialized] public Bindable<string> Text;
    [NonSerialized] public Bindable<string> SecondText;
    [NonSerialized] public Bindable<string> ThirdText;
    [NonSerialized] public Bindable<string> FourthText;
    [NonSerialized] public Bindable<string> FifthText;
    [NonSerialized] public Bindable<string> SixthText;
    [NonSerialized] public Bindable<string> Format;
    [NonSerialized] public Bindable<DateTime> Date;
    [NonSerialized] public Bindable<DateTime> SecondDate;
    [NonSerialized] public Bindable<DateTime> ThirdDate;
    [NonSerialized] public Bindable<TimeSpan> Span;
    [NonSerialized] public Bindable<DateTimeKind> Kind;
    [NonSerialized] public Bindable<Vector2> Vector;
    [NonSerialized] public Bindable<Vector2> SecondVector;

    public Bindable<T> Recording<T>(string name, T value) => new RecordingBindable<T>(Reads, name, value);

    private sealed class RecordingBindable<T> : Bindable<T>
    {
        private readonly List<string> reads;
        private readonly string name;

        public RecordingBindable(List<string> reads, string name, T value) : base(value)
        {
            this.reads = reads;
            this.name = name;
        }

        public override T Value
        {
            get
            {
                reads.Add(name);
                return base.Value;
            }
        }
    }
}
